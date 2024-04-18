using Arbitrum.AssetBridgerModule;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Utils;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Org.BouncyCastle.Bcpg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Arbitrum.AssetBridger
{
    public class TokenApproveParams
    {
        public string? Erc20L1Address { get; set; }
        public BigInteger? Amount { get; set; }
        public PayableOverrides? overrides { get; set; }
    }
    public class Erc20DepositParams : EthDepositParams
    {
        public Web3? L2Provider { get; set; }
        public string? Erc20L1Address { get; set; }
        public string? DestinationAddress { get; set; }
        public BigInteger? MaxSubmissionCost { get; set; }
        public string? ExcessFeeRefundAddress { get; set; }
        public string? CallValueRefundAddress { get; set; }
        public GasOverrides? RetryableGasOverrides { get; set; }
        public new PayableOverrides? Overrides { get; set; }
    }

    public class Erc20WithdrawParams : EthWithdrawParams
    {
        public string? Erc20L1Address { get; set; }
    }

    public class L1ToL2TxReqAndSignerProvider : L1ToL2TransactionRequest
    {
        public Account? L1Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }

    public class L2ToL1TxReqAndSigner : L2ToL1TransactionRequest
    {
        public Account? L2Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }
    public class SignerTokenApproveParams : TokenApproveParams
    {
        public Account? L1Signer { get; set; }
    }

    public class ProviderTokenApproveParams : TokenApproveParams
    {
        public Web3? L1Provider { get; set; }
    }
    public class ApproveParamsOrTxRequest
    {
        public TransactionRequest? TxRequest { get; set; }
        public Account? L1Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }

    public class DepositRequest : Erc20DepositParams
    {
        public Web3? L1Provider { get; set; }
        public string? From { get; set; }
    }
    public class DefaultedDepositRequest : DepositRequest
    {
        public new string? CallValueRefundAddress { get; set; }
        public new string? ExcessFeeRefundAddress { get; set; }
        public new string? DestinationAddress { get; set; }
    }
    public class Erc20Bridger : AssetBridger<Erc20DepositParams, Erc20WithdrawParams>
    {
        public static BigInteger MAX_APPROVAL { get; set; } = BigInteger.Parse("18446744073709551615");
        public static BigInteger MIN_CUSTOM_DEPOSIT_GAS_LIMIT { get; set; } = BigInteger.Parse("275000");

        public Erc20Bridger(L2Network l2Network) : base(l2Network)
        {
        }

        public static async Task<Erc20Bridger> FromProvider(Web3 l2Provider)
        {
            L2Network l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);
            return new Erc20Bridger(l2Network);
        }

        public async Task<string> GetL1GatewayAddress(string erc20L1Address, Web3 l1Provider)
        {
            await CheckL1Network(new SignerOrProvider(l1Provider));

            // Load the L1GatewayRouter contract
            var l1GatewayRouter = await LoadContractUtils.LoadContract(
                contractName: "L1GatewayRouter",
                address: L2Network.TokenBridge!.L1GatewayRouter,
                provider: l1Provider,
                isClassic: true
            );

            // Call the getGateway function
            var getGatewayFunction = l1GatewayRouter.GetFunction("getGateway");
            return await getGatewayFunction.CallAsync<string>(erc20L1Address);
        }

        public async Task<string> GetL2GatewayAddress(string erc20L1Address, Web3 l2Provider)
        {
            await CheckL2Network(new SignerOrProvider(l2Provider));

            // Load the L2GatewayRouter contract using the LoadContract method
            var l2GatewayRouterContract = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "L2GatewayRouter",
                address: L2Network.TokenBridge!.L2GatewayRouter,
                isClassic: true
            );

            // Retrieve the getGateway function
            var getGatewayFunction = l2GatewayRouterContract.GetFunction("getGateway");

            // Call the function with erc20L1Address and return the result as a string
            return await getGatewayFunction.CallAsync<string>(erc20L1Address);
        }

        public async Task<TransactionRequest> GetApproveGasTokenRequest(ProviderTokenApproveParams parameters)
        {
            if (NativeTokenIsEth)
            {
                throw new InvalidOperationException("Chain uses ETH as its native/gas token");
            }

            // Call the existing method to get the approve token request
            var txRequest = await GetApproveTokenRequest(parameters);

            // Modify the transaction request to direct it towards the native token contract
            txRequest.To = NativeToken;

            return txRequest;
        }

        public async Task<TransactionReceipt> ApproveGasToken(ApproveParamsOrTxRequest parameters)
        {
            if (NativeTokenIsEth)
            {
                throw new InvalidOperationException("Chain uses ETH as its native/gas token");
            }

            await CheckL1Network(new SignerOrProvider(parameters.L1Signer!));

            TransactionRequest approveGasTokenRequest;

            if (IsApproveParams(parameters))
            {
                var providerTokenApproveParams = HelperMethods.CopyMatchingProperties<ProviderTokenApproveParams, ApproveParamsOrTxRequest>(parameters);

                providerTokenApproveParams.L1Provider = SignerProviderUtils.GetProviderOrThrow(parameters.L1Signer!);

                approveGasTokenRequest = await GetApproveGasTokenRequest(providerTokenApproveParams);
            }

            else
            {
                approveGasTokenRequest = parameters.TxRequest!;
            }

           // //Combine the approveGasTokenRequest with any provided overrides
           //var combinedTxRequest = new TransactionRequest
           //{
           //    From = parameters.L1Signer!.Address,
           //    To = approveGasTokenRequest!.To,
           //    Value = approveGasTokenRequest.Value,
           //    Data = approveGasTokenRequest.Data,
           //    GasPrice = new HexBigInteger(parameters.Overrides?.GasPrice.ToString()),
           //    Nonce = new HexBigInteger(parameters.Overrides?.Nonce.ToString()),
           //    AccessList = approveGasTokenRequest.AccessList,
           //    ChainId = approveGasTokenRequest?.ChainId,
           //    Gas = approveGasTokenRequest?.Gas,
           //    MaxFeePerGas = approveGasTokenRequest?.MaxFeePerGas,
           //    MaxPriorityFeePerGas = approveGasTokenRequest?.MaxPriorityFeePerGas,
           //    Type = approveGasTokenRequest?.Type
           //};

            return await parameters.L1Signer!.TransactionManager.SendTransactionAndWaitForReceiptAsync(approveGasTokenRequest);
        }

        public async Task<TransactionRequest> GetApproveTokenRequest(ProviderTokenApproveParams parameters)
        {
            // Approving tokens to the gateway that the router will use
            var gatewayAddress = await GetL1GatewayAddress(
                parameters.Erc20L1Address!,
                SignerProviderUtils.GetProviderOrThrow(parameters.L1Provider!)
            );

            // Create the ERC20 interface
            Contract erc20Interface = await LoadContractUtils.LoadContract(
                                                        contractName: "ERC20",
                                                        provider: parameters.L1Provider!,
                                                        isClassic: true
                                                        );


            var functionAbi = erc20Interface.ContractBuilder.GetFunctionAbi("approve");

            // Define the base directory (assumed to be the root directory of the project)    //////
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Construct the path to the ABI file dynamically
            string abiFilePath = Path.Combine(baseDirectory, "src", "abi", "classic", "ERC20.json");

            //string contractByteCode = "0x60806040523480156200001157600080fd5b5060405162000ca538038062000ca5833981810160405260408110156200003757600080fd5b81019080805160405193929190846401000000008211156200005857600080fd5b9083019060208201858111156200006e57600080fd5b82516401000000008111828201881017156200008957600080fd5b82525081516020918201929091019080838360005b83811015620000b85781810151838201526020016200009e565b50505050905090810190601f168015620000e65780820380516001836020036101000a031916815260200191505b50604052602001805160405193929190846401000000008211156200010a57600080fd5b9083019060208201858111156200012057600080fd5b82516401000000008111828201881017156200013b57600080fd5b82525081516020918201929091019080838360005b838110156200016a57818101518382015260200162000150565b50505050905090810190601f168015620001985780820380516001836020036101000a031916815260200191505b5060405250508251620001b491506003906020850190620001e0565b508051620001ca906004906020840190620001e0565b50506005805460ff191660121790555062000285565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106200022357805160ff191683800117855562000253565b8280016001018555821562000253579182015b828111156200025357825182559160200191906001019062000236565b506200026192915062000265565b5090565b6200028291905b808211156200026157600081556001016200026c565b90565b610a1080620002956000396000f3fe608060405234801561001057600080fd5b50600436106100a95760003560e01c8063395093511161007157806339509351146101d957806370a082311461020557806395d89b411461022b578063a457c2d714610233578063a9059cbb1461025f578063dd62ed3e1461028b576100a9565b806306fdde03146100ae578063095ea7b31461012b57806318160ddd1461016b57806323b872dd14610185578063313ce567146101bb575b600080fd5b6100b66102b9565b6040805160208082528351818301528351919283929083019185019080838360005b838110156100f05781810151838201526020016100d8565b50505050905090810190601f16801561011d5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b6101576004803603604081101561014157600080fd5b506001600160a01b03813516906020013561034f565b604080519115158252519081900360200190f35b61017361036c565b60408051918252519081900360200190f35b6101576004803603606081101561019b57600080fd5b506001600160a01b03813581169160208101359091169060400135610372565b6101c36103ff565b6040805160ff9092168252519081900360200190f35b610157600480360360408110156101ef57600080fd5b506001600160a01b038135169060200135610408565b6101736004803603602081101561021b57600080fd5b50356001600160a01b031661045c565b6100b6610477565b6101576004803603604081101561024957600080fd5b506001600160a01b0381351690602001356104d8565b6101576004803603604081101561027557600080fd5b506001600160a01b038135169060200135610546565b610173600480360360408110156102a157600080fd5b506001600160a01b038135811691602001351661055a565b60038054604080516020601f60026000196101006001881615020190951694909404938401819004810282018101909252828152606093909290918301828280156103455780601f1061031a57610100808354040283529160200191610345565b820191906000526020600020905b81548152906001019060200180831161032857829003601f168201915b5050505050905090565b600061036361035c610585565b8484610589565b50600192915050565b60025490565b600061037f848484610675565b6103f58461038b610585565b6103f085604051806060016040528060288152602001610945602891396001600160a01b038a166000908152600160205260408120906103c9610585565b6001600160a01b03168152602081019190915260400160002054919063ffffffff6107dc16565b610589565b5060019392505050565b60055460ff1690565b6000610363610415610585565b846103f08560016000610426610585565b6001600160a01b03908116825260208083019390935260409182016000908120918c16815292529020549063ffffffff61087316565b6001600160a01b031660009081526020819052604090205490565b60048054604080516020601f60026000196101006001881615020190951694909404938401819004810282018101909252828152606093909290918301828280156103455780601f1061031a57610100808354040283529160200191610345565b60006103636104e5610585565b846103f0856040518060600160405280602581526020016109b6602591396001600061050f610585565b6001600160a01b03908116825260208083019390935260409182016000908120918d1681529252902054919063ffffffff6107dc16565b6000610363610553610585565b8484610675565b6001600160a01b03918216600090815260016020908152604080832093909416825291909152205490565b3390565b6001600160a01b0383166105ce5760405162461bcd60e51b81526004018080602001828103825260248152602001806109926024913960400191505060405180910390fd5b6001600160a01b0382166106135760405162461bcd60e51b81526004018080602001828103825260228152602001806108fd6022913960400191505060405180910390fd5b6001600160a01b03808416600081815260016020908152604080832094871680845294825291829020859055815185815291517f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b9259281900390910190a3505050565b6001600160a01b0383166106ba5760405162461bcd60e51b815260040180806020018281038252602581526020018061096d6025913960400191505060405180910390fd5b6001600160a01b0382166106ff5760405162461bcd60e51b81526004018080602001828103825260238152602001806108da6023913960400191505060405180910390fd5b61070a8383836108d4565b61074d8160405180606001604052806026815260200161091f602691396001600160a01b038616600090815260208190526040902054919063ffffffff6107dc16565b6001600160a01b038085166000908152602081905260408082209390935590841681522054610782908263ffffffff61087316565b6001600160a01b038084166000818152602081815260409182902094909455805185815290519193928716927fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef92918290030190a3505050565b6000818484111561086b5760405162461bcd60e51b81526004018080602001828103825283818151815260200191508051906020019080838360005b83811015610830578181015183820152602001610818565b50505050905090810190601f16801561085d5780820380516001836020036101000a031916815260200191505b509250505060405180910390fd5b505050900390565b6000828201838110156108cd576040805162461bcd60e51b815260206004820152601b60248201527f536166654d6174683a206164646974696f6e206f766572666c6f770000000000604482015290519081900360640190fd5b9392505050565b50505056fe45524332303a207472616e7366657220746f20746865207a65726f206164647265737345524332303a20617070726f766520746f20746865207a65726f206164647265737345524332303a207472616e7366657220616d6f756e7420657863656564732062616c616e636545524332303a207472616e7366657220616d6f756e74206578636565647320616c6c6f77616e636545524332303a207472616e736665722066726f6d20746865207a65726f206164647265737345524332303a20617070726f76652066726f6d20746865207a65726f206164647265737345524332303a2064656372656173656420616c6c6f77616e63652062656c6f77207a65726fa26469706673582212209f9b5b12792bdc1e6928c54586708bee2f74b32c3b7af812c0eb1c605569ab8d64736f6c634300060b0033";
            //var contractAbi = erc20Interface.ContractBuilder.ContractABI;
            //string functionData = new ConstructorCallEncoder().EncodeRequest(
            //    contractByteCode: contractByteCode,
            //    parameters: contractAbi.Constructor.InputParameters,
            //    values: new object[] { gatewayAddress, parameters.Amount ?? MAX_APPROVAL }
            //    );

            string functionData = new ConstructorCallEncoder().EncodeRequest(
                contractByteCode: HelperMethods.GetBytecodeFromABI(abiFilePath) ?? null,
                parameters: functionAbi.InputParameters,
                values: new object[] { gatewayAddress, parameters.Amount?? MAX_APPROVAL}
                );

            // Return a new TransactionRequest
            return new TransactionRequest
            {
                To = parameters.Erc20L1Address,
                Data = functionData,
                Value = BigInteger.Zero
            };
        }

        public async Task<TransactionReceipt> ApproveToken(ApproveParamsOrTxRequest parameters)
        {
            // Check if the signer is connected to the correct L1 network
            await CheckL1Network(new SignerOrProvider(parameters.L1Signer!));

            TransactionRequest approveRequest;

            // Determine whether the parameters represent a SignerTokenApproveParams type
            if (IsApproveParams(parameters))
            {
                // Call GetApproveTokenRequest with appropriate parameters
                var provider = SignerProviderUtils.GetProviderOrThrow(parameters.L1Signer!);
                var signerTokenApproveParams = parameters as SignerTokenApproveParams;

                approveRequest = await GetApproveTokenRequest(signerTokenApproveParams);
            }
            else
            {
                // If the parameters are not of type SignerTokenApproveParams, use the existing transaction request
                approveRequest = parameters.TxRequest!;
            }


            // Send the transaction and return the transaction receipt
            return await parameters.L1Signer!.TransactionManager.SendTransactionAndWaitForReceiptAsync(approveRequest);
        }

        private bool IsApproveParams(ApproveParamsOrTxRequest parameters)
        {
            // Check if parameters is of type SignerTokenApproveParams by checking if the erc20L1Address property is set
            return parameters is SignerTokenApproveParams signerParams && !string.IsNullOrEmpty(signerParams.Erc20L1Address);
        }

        public async Task<List<(WithdrawalInitiatedEvent EventArgs, string TxHash)>> GetL2WithdrawalEvents(
        Web3 l2Provider,
        string gatewayAddress,
        (BlockTag FromBlock, BlockTag ToBlock) filter,
        string? l1TokenAddress = null,
        string? fromAddress = null,
        string? toAddress = null)
        {
            await CheckL2Network(new SignerOrProvider(l2Provider));

            EventFetcher eventFetcher = new EventFetcher(l2Provider);
            var events = (await eventFetcher.GetEventsAsync(
                gatewayAddress,
                eventArgs => eventArgs.WithdrawalInitiated(
                    null,
                    fromAddress ?? null,
                    toAddress ?? null),
                filter.FromBlock,
                filter.ToBlock)).ToList();

            List<(WithdrawalInitiatedEvent, string)> eventList = events
                .Select(e => (e.Event, e.TransactionHash))
                .ToList();

            if (!string.IsNullOrEmpty(l1TokenAddress))
            {
                eventList = eventList
                    .Where(log => log.Event.L1Token.Equals(l1TokenAddress, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return eventList;
        }

        private async Task<bool> LooksLikeWethGateway(string potentialWethGatewayAddress, Web3 l1Provider)
        {
            try
            {
                //L1WethGateway potentialWethGateway = new L1WethGateway(potentialWethGatewayAddress, l1Provider);
                //await potentialWethGateway.CallStatic.L1Weth();
                return true;
            }
            catch (Exception ex)
            {
                if (ex is RpcException rpcException &&
                    rpcException.RpcError.Code == -32000) // ErrorCode.CALL_EXCEPTION
                {
                    return false;
                }
                throw;
            }
        }

        private async Task<bool> IsWethGateway(string gatewayAddress, Web3 l1Provider)
        {
            string wethAddress = L2Network.TokenBridge!.L1WethGateway!;

            if (L2Network.IsCustom)
            {
                // For custom networks, check if it's a WETH gateway
                if (await LooksLikeWethGateway(gatewayAddress, l1Provider))
                {
                    return true;
                }
            }
            else if (wethAddress.Equals(gatewayAddress, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public async Task<Contract> GetL2TokenContract(Web3 l2Provider, string l2TokenAddr)
        {
            return await LoadContractUtils.LoadContract(
                                                provider: l2Provider,
                                                contractName: "L2GatewayToken",
                                                address: l2TokenAddr,
                                                isClassic: true
                                                );
        }

        public async Task<Contract> GetL1TokenContract(Web3 l1Provider, string l1TokenAddr)
        {
            return await LoadContractUtils.LoadContract(
                                                provider: l1Provider,
                                                contractName: "ERC20",
                                                address: l1TokenAddr,
                                                isClassic: true
                                                );
        }

        public async Task<string> GetL2ERC20Address(string erc20L1Address, Web3 l1Provider)
        {
            await CheckL1Network(new SignerOrProvider(l1Provider));

            var l1GatewayRouter = await LoadContractUtils.LoadContract(
                                                provider: l1Provider,
                                                contractName: "L1GatewayRouter",
                                                address: erc20L1Address,
                                                isClassic: true
                                                );

            return await l1GatewayRouter.GetFunction("calculateL2TokenAddress").CallAsync<string>(erc20L1Address);
        }

        public async Task<string> GetL1ERC20Address(string erc20L2Address, Web3 l2Provider)
        {
            await CheckL2Network(new SignerOrProvider(l2Provider));

            if (erc20L2Address.Equals(L2Network.TokenBridge!.L2Weth, StringComparison.OrdinalIgnoreCase))
            {
                return L2Network.TokenBridge.L1Weth!;
            }

            var arbERC20 = await LoadContractUtils.LoadContract(
                                                provider: l2Provider,
                                                contractName: "L2GatewayToken",
                                                address: erc20L2Address,
                                                isClassic: true
                                                );

            var l1Address = await arbERC20.GetFunction("l1Address").CallAsync<string>();

            var l2GatewayRouter = await LoadContractUtils.LoadContract(
                                                provider: l2Provider,
                                                contractName: "L1GatewayRouter",
                                                address: erc20L2Address,
                                                isClassic: true
                                                );

            var l2Address = await arbERC20.GetFunction("calculateL2TokenAddress").CallAsync<string>(l1Address);

            if (!l2Address.Equals(erc20L2Address, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArbSdkError($"Unexpected L1 address. L1 address from token is not registered to the provided L2 address. {l1Address} {l2Address} {erc20L2Address}");
            }

            return l1Address;
        }

        public async Task<bool> L1TokenIsDisabled(string l1TokenAddress, Web3 l1Provider)
        {
            await CheckL1Network(l1Provider);

            L1GatewayRouter l1GatewayRouter = new L1GatewayRouter(l2Network.TokenBridge.L1GatewayRouter, l1Provider);
            var gatewayAddress = await l1GatewayRouter.L1TokenToGatewayAsync(l1TokenAddress);
            return gatewayAddress == Constants.DISABLED_GATEWAY;
        }

        private DefaultedDepositRequest ApplyDefaults<T>(T params) where T : DepositRequest
        {
            return new DefaultedDepositRequest
            {
                // Apply default values
                ExcessFeeRefundAddress = params.ExcessFeeRefundAddress ?? params.From,
                CallValueRefundAddress = params.CallValueRefundAddress ?? params.From,
                DestinationAddress = params.DestinationAddress ?? params.From,
            };
        }

        private BigInteger? GetDepositRequestCallValue(L1ToL2MessageGasParams depositParams)
        {
            if (!this.NativeTokenIsEth)
            {
                return BigInteger.Zero;
            }

            // Calculate call value
            return depositParams.GasLimit * depositParams.MaxFeePerGas + depositParams.MaxSubmissionCost;
        }

        private byte[] GetDepositRequestOutboundTransferInnerData(L1ToL2MessageGasParams depositParams)
        {
            ABIEncoder encoder = new ABIEncoder();

            if (!this.NativeTokenIsEth)
            {
                return encoder.Encode(new object[]
                {
                depositParams.MaxSubmissionCost,
                new byte[0],
                depositParams.GasLimit * depositParams.MaxFeePerGas + depositParams.MaxSubmissionCost
                });
            }

            return encoder.Encode(new object[]
            {
            depositParams.MaxSubmissionCost,
            new byte[0]
            });
        }

        public async Task<L1ToL2TransactionRequest> GetDepositRequest(DepositRequest params)
        {
            await CheckL1Network(params.L1Provider);
            await CheckL2Network(params.L2Provider);

            DefaultedDepositRequest defaultedParams = ApplyDefaults (params);

            // Extracted variables
            var amount = defaultedParams.Amount;
            var destinationAddress = defaultedParams.DestinationAddress;
            var erc20L1Address = defaultedParams.Erc20L1Address;
            var l1Provider = defaultedParams.L1Provider;
            var l2Provider = defaultedParams.L2Provider;
            var retryableGasOverrides = defaultedParams.RetryableGasOverrides;

            // Get gateway address
            string l1GatewayAddress = await GetL1GatewayAddressAsync(erc20L1Address, l1Provider);

            // Modify tokenGasOverrides if necessary
            GasOverrides tokenGasOverrides = retryableGasOverrides;
            if (l1GatewayAddress == l2Network.TokenBridge.L1CustomGateway)
            {
                if (tokenGasOverrides == null)
                {
                    tokenGasOverrides = new GasOverrides();
                }
                if (tokenGasOverrides.GasLimit == null)
                {
                    tokenGasOverrides.GasLimit = new GasLimit();
                }
                if (tokenGasOverrides.GasLimit.Min == null)
                {
                    tokenGasOverrides.GasLimit.Min = Erc20Bridger.MinCustomDepositGasLimit;
                }
            }

            // Define deposit function
            Func<DepositParams, L1ToL2TransactionRequest> depositFunc = (depositParams) =>
            {
                // Add missing maxSubmissionCost if necessary
                depositParams.MaxSubmissionCost = params.MaxSubmissionCost ?? depositParams.MaxSubmissionCost;

                L1GatewayRouterInterface iGatewayRouter = new L1GatewayRouterInterface();
                byte[] innerData = GetDepositRequestOutboundTransferInnerData(depositParams);

                byte[] functionData;
                if (defaultedParams.ExcessFeeRefundAddress != defaultedParams.From)
                {
                    functionData = iGatewayRouter.EncodeFunctionData("outboundTransferCustomRefund", new object[]
                    {
                    erc20L1Address,
                    defaultedParams.ExcessFeeRefundAddress,
                    destinationAddress,
                    amount,
                    depositParams.GasLimit,
                    depositParams.MaxFeePerGas,
                    innerData
                    });
                }
                else
                {
                    functionData = iGatewayRouter.EncodeFunctionData("outboundTransfer", new object[]
                    {
                    erc20L1Address,
                    destinationAddress,
                    amount,
                    depositParams.GasLimit,
                    depositParams.MaxFeePerGas,
                    innerData
                    });
                }

                return new L1ToL2TransactionRequest
                {
                    Data = functionData,
                    To = l2Network.TokenBridge.L1GatewayRouter,
                    From = defaultedParams.From,
                    Value = GetDepositRequestCallValue(depositParams)
                };
            };

            L1ToL2MessageGasEstimator gasEstimator = new L1ToL2MessageGasEstimator(l2Provider);
            var estimates = await gasEstimator.PopulateFunctionParamsAsync(depositFunc, l1Provider, tokenGasOverrides);

            return new L1ToL2TransactionRequest
            {
                TxRequest = new L1TransactionRequest
                {
                    To = l2Network.TokenBridge.L1GatewayRouter,
                    Data = estimates.Data,
                    Value = estimates.Value,
                    From = params.From,
                },
                RetryableData = new RetryableData
                {
                    RetryableGas = estimates.Retryable,
                    Estimates = estimates.Estimates,
                },
                IsValid = async () => L1ToL2MessageGasEstimator.IsValid(estimates.Estimates, await gasEstimator.PopulateFunctionParamsAsync(depositFunc, l1Provider, tokenGasOverrides).Estimates)
            };
        }

        public async Task<L1ContractCallTransaction> Deposit(Erc20DepositParams params)
        {
            await CheckL1Network(params.L1Signer);

            // Check for unexpected values
            if (params.Overrides?.Value != null)
        {
                throw new ArbSdkError("L1 call value should be set through l1CallValue param.");
            }

            IProvider l1Provider = SignerProviderUtils.GetProviderOrThrow (params.L1Signer);
            L1ToL2TransactionRequest tokenDeposit = params is L1ToL2TransactionRequest l1ToL2TxReq
                ? l1ToL2TxReq
                : await GetDepositRequest(new DepositRequest
                {
                    Erc20L1Address = params.Erc20L1Address,
                    L1Provider = l1Provider,
                    From = await params.L1Signer.GetAddress(),
                    Amount = params.Amount,
                    DestinationAddress = params.DestinationAddress,
                    L2Provider = params.L2Provider,
                    RetryableGasOverrides = params.RetryableGasOverrides
                });

            var tx = await params.L1Signer.SendTransactionAsync(tokenDeposit.TxRequest);

            // Apply monkey patching
            return L1TransactionReceipt.MonkeyPatchContractCallWait(tx);
        }

        public async Task<L2ToL1TransactionRequest> GetWithdrawalRequest(Erc20WithdrawParams @params)
        {
            string to = @params.DestinationAddress;

            // Create the router interface
            L2GatewayRouter routerInterface = new L2GatewayRouter();
            byte[] functionData = routerInterface.EncodeFunctionData("outboundTransfer(address,address,uint256,bytes)", new object[]
            {
            @params.Erc20L1Address,
            to,
            @params.Amount,
            "0x"
            });

            // Create the transaction request
            L2ToL1TransactionRequest request = new L2ToL1TransactionRequest
            {
                TxRequest = new L2TransactionRequest
                {
                    Data = functionData,
                    To = l2Network.TokenBridge.L2GatewayRouter,
                    Value = BigInteger.Zero,
                    From = @params.From
                },
                EstimateL1GasLimit = async (l1Provider) =>
                {
                    if (await IsArbitrumChain(l1Provider))
                    {
                        // Estimate L1 gas limit
                        return BigInteger.Parse("8000000");
                    }

                    // Get L1 Gateway Address
                    string l1GatewayAddress = await GetL1GatewayAddressAsync(@params.Erc20L1Address, l1Provider);

                    // Check if this is a WETH deposit
                    bool isWeth = await IsWethGatewayAsync(l1GatewayAddress, l1Provider);

                    // Return estimated gas limit with padding
                    return isWeth ? BigInteger.Parse("190000") : BigInteger.Parse("160000");
                }
            };

            return request;
        }

        // Function to withdraw tokens from L2 to L1
        public async Task<L2ContractTransaction> Withdraw(L2WithdrawParams params)
        {
            if (!SignerProviderUtils.SignerHasProvider(params.L2Signer))
            {
                throw new MissingProviderArbSdkError("l2Signer");
            }

            await CheckL2Network(params.L2Signer);

            L2ToL1TransactionRequest withdrawalRequest = await (IsL2ToL1TransactionRequest (params)
    
                ? Task.FromResult (params)
    
                : GetWithdrawalRequest(new Erc20WithdrawParams
                {
                    From = await params.L2Signer.GetAddress(),
                    DestinationAddress = params.DestinationAddress,
                    Erc20L1Address = params.Erc20L1Address,
                    Amount = params.Amount
                }));

            var tx = await params.L2Signer.SendTransactionAsync(withdrawalRequest.TxRequest);
            return L2TransactionReceipt.MonkeyPatchWait(tx);
        }

        public class AdminErc20Bridger : Erc20Bridger
        {
            public async Task<L1ContractTransaction> RegisterCustomTokenAsync(
                string l1TokenAddress,
                string l2TokenAddress,
                IAccount l1Signer,
                IWeb3 l2Provider)
            {
                if (!SignerProviderUtils.SignerHasProvider(l1Signer))
                {
                    throw new MissingProviderArbSdkError("l1Signer");
                }

                await CheckL1NetworkAsync(l1Signer);
                await CheckL2NetworkAsync(l2Provider);

                string l1SenderAddress = await l1Signer.GetAddressAsync();

                var l1Token = ICustomToken__factory.Connect(l1TokenAddress, l1Signer);
                var l2Token = IArbToken__factory.Connect(l2TokenAddress, l2Provider);

                // Sanity checks
                await l1Token.DeployedAsync();
                await l2Token.DeployedAsync();

                string l1AddressFromL2 = await l2Token.L1AddressAsync();
                if (l1AddressFromL2 != l1TokenAddress)
                {
                    throw new ArbSdkError(
                        $"L2 token does not have l1 address set. Set address: {l1AddressFromL2}, expected address: {l1TokenAddress}."
                    );
                }

                var l1Provider = l1Signer.Client;
                var gEstimator = new L1ToL2MessageGasEstimator(l2Provider);

                // Define encodeFuncData function for setting gas parameters
                Func<GasParams, GasParams, BigInteger, L1ContractTransaction> encodeFuncData = (setTokenGas, setGatewayGas, maxFeePerGas) =>
                {
                    BigInteger doubleFeePerGas = maxFeePerGas == RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas
                        ? RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas * 2
                        : maxFeePerGas;

                    BigInteger setTokenDeposit = setTokenGas.GasLimit * doubleFeePerGas + setTokenGas.MaxSubmissionCost;
                    BigInteger setGatewayDeposit = setGatewayGas.GasLimit * doubleFeePerGas + setGatewayGas.MaxSubmissionCost;

                    var data = l1Token.EncodeFunctionData("registerTokenOnL2", new object[]
                    {
                l2TokenAddress,
                setTokenGas.MaxSubmissionCost,
                setGatewayGas.MaxSubmissionCost,
                setTokenGas.GasLimit,
                setGatewayGas.GasLimit,
                doubleFeePerGas,
                setTokenDeposit,
                setGatewayDeposit,
                l1SenderAddress
                    });

                    return new L1ContractTransaction
                    {
                        Data = data,
                        To = l1Token.Address,
                        Value = setTokenDeposit + setGatewayDeposit,
                        From = l1SenderAddress
                    };
                };

                // Estimate gas parameters
                var setTokenEstimates = await gEstimator.PopulateFunctionParamsAsync(
                    (params) => encodeFuncData(
                        new GasParams
                        {
                            GasLimit = params.GasLimit,
                            MaxSubmissionCost = params.MaxSubmissionCost
                        },
                        new GasParams
                        {
                            GasLimit = RetryableDataTools.ErrorTriggeringParams.GasLimit,
                            MaxSubmissionCost = BigInteger.One
                        },
                        params.MaxFeePerGas
                    ),
                    l1Provider
                );

                // Perform the transaction
                var registerTx = await l1Signer.SendTransactionAsync(new Nethereum.RPC.Eth.DTOs.TransactionInput
                {
                    To = l1Token.Address,
                    Data = setTokenEstimates.Data,
                    Value = setTokenEstimates.Value,
                    From = l1SenderAddress
                });

                // Return the transaction receipt
                return L1TransactionReceipt.MonkeyPatchWait(registerTx);
            }

            public async Task<List<GatewaySetEvent>> GetL1GatewaySetEventsAsync(IWeb3 l1Provider, BlockTag fromBlock, BlockTag toBlock)
            {
                await CheckL1NetworkAsync(l1Provider);

                string l1GatewayRouterAddress = l2Network.TokenBridge.L1GatewayRouter;
                var eventFetcher = new EventFetcher(l1Provider);

                var events = await eventFetcher.GetEventsAsync<L1GatewayRouter__factory>(
                    (contract) => contract.FilterGatewaySet(),
                    new FilterInput
                    {
                        FromBlock = fromBlock,
                        ToBlock = toBlock,
                        Address = l1GatewayRouterAddress
                    }
                );

                return events;
            }

            public async Task<List<GatewaySetEvent>> GetL2GatewaySetEventsAsync(
                IWeb3 l2Provider,
                BlockTag fromBlock,
                BlockTag toBlock,
                string customNetworkL2GatewayRouter = null)
            {
                if (l2Network.IsCustom && customNetworkL2GatewayRouter == null)
                {
                    throw new ArbSdkError("Must supply customNetworkL2GatewayRouter for custom network");
                }

                await CheckL2NetworkAsync(l2Provider);

                string l2GatewayRouterAddress = customNetworkL2GatewayRouter ?? l2Network.TokenBridge.L2GatewayRouter;

                var eventFetcher = new EventFetcher(l2Provider);

                var events = await eventFetcher.GetEventsAsync<L2GatewayRouter__factory>(
                    (contract) => contract.FilterGatewaySet(),
                    new FilterInput
                    {
                        FromBlock = fromBlock,
                        ToBlock = toBlock,
                        Address = l2GatewayRouterAddress
                    }
                );

                return events;
            }

            public async Task<L1ContractCallTransaction> SetGatewaysAsync(
                IAccount l1Signer,
                IWeb3 l2Provider,
                List<TokenAndGateway> tokenGateways,
                GasOverrides options = null)
            {
                if (!SignerProviderUtils.SignerHasProvider(l1Signer))
                {
                    throw new MissingProviderArbSdkError("l1Signer");
                }

                await CheckL1NetworkAsync(l1Signer);
                await CheckL2NetworkAsync(l2Provider);

                string from = await l1Signer.GetAddressAsync();

                var l1GatewayRouter = L1GatewayRouter__factory.Connect(
                    l2Network.TokenBridge.L1GatewayRouter,
                    l1Signer
                );

                // Define function for setting gateways
                Func<L1ToL2MessageGasParams, L1ContractCallTransaction> setGatewaysFunc = (params) =>
                {
                    var data = l1GatewayRouter.EncodeFunctionData("setGateways", new object[]
                    {
                tokenGateways.ConvertAll(tg => tg.TokenAddr),
                tokenGateways.ConvertAll(tg => tg.GatewayAddr),
                params.GasLimit,
                params.MaxFeePerGas,
                params.MaxSubmissionCost
                    });

                    BigInteger value = params.GasLimit * params.MaxFeePerGas + params.MaxSubmissionCost;

                    return new L1ContractCallTransaction
                    {
                        Data = data,
                        From = from,
                        Value = value,
                        To = l1GatewayRouter.Address
                    };
                };

                var gEstimator = new L1ToL2MessageGasEstimator(l2Provider);
                var estimates = await gEstimator.PopulateFunctionParamsAsync(setGatewaysFunc, l1Signer.Client, options);

                var res = await l1Signer.SendTransactionAsync(new Nethereum.RPC.Eth.DTOs.TransactionInput
                {
                    To = estimates.To,
                    Data = estimates.Data,
                    Value = estimates.Estimates.Deposit,
                    From = from
                });

                return L1TransactionReceipt.MonkeyPatchContractCallWait(res);
            }
        }


    }
}
