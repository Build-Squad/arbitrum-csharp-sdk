using Arbitrum.AssetBridgerModule;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Utils;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Org.BouncyCastle.Asn1.Ocsp;
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
        public PayableOverrides? Overrides { get; set; }
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

    public class L1ToL2TxReqAndSignerProvider : L1ToL2TxReqAndSigner
    {
        public new Account? L1Signer { get; set; }
        public new PayableOverrides? Overrides { get; set; }
        public new Web3? L2Provider { get; set; }

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
    public class ApproveParamsOrTxRequest : SignerTokenApproveParams
    {
        public TransactionRequest? TxRequest { get; set; }
        public new Account? L1Signer { get; set; }
        public new PayableOverrides? Overrides { get; set; }
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
    public class WithdrawalInitiatedEvent : L2ToL1TransactionEvent
    {
        public string? L1Token { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public BigInteger? L2ToL1Id { get; set; }
        public BigInteger? ExitNum { get; set; }
        public BigInteger? Amount { get; set; }
    }

    public class GatewaySetEvent
    {
        public string? L1Token { get; set; }
        public string? Gateway { get; set; }
    }
    public class TokenAndGateway
    {
        public string? TokenAddr { get; set; }
        public string? GatewayAddr { get; set; }
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
                address: L2Network?.TokenBridge?.L1GatewayRouter,
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
                address: L2Network?.TokenBridge?.L2GatewayRouter,
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

            await CheckL1Network(new SignerOrProvider(parameters?.L1Signer));

            TransactionRequest approveGasTokenRequest;

            if (IsApproveParams(parameters))
            {
                var providerTokenApproveParams = HelperMethods.CopyMatchingProperties<ProviderTokenApproveParams, ApproveParamsOrTxRequest>(parameters);

                providerTokenApproveParams.L1Provider = SignerProviderUtils.GetProviderOrThrow(parameters?.L1Signer);

                approveGasTokenRequest = await GetApproveGasTokenRequest(providerTokenApproveParams);
            }

            else
            {
                approveGasTokenRequest = parameters?.TxRequest!;
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

            return await parameters?.L1Signer?.TransactionManager?.SendTransactionAndWaitForReceiptAsync(approveGasTokenRequest)!;
        }

        public async Task<TransactionRequest> GetApproveTokenRequest(ProviderTokenApproveParams parameters)
        {
            // Approving tokens to the gateway that the router will use
            var gatewayAddress = await GetL1GatewayAddress(
                parameters?.Erc20L1Address!,
                SignerProviderUtils.GetProviderOrThrow(parameters?.L1Provider!)
            );

            // Create the ERC20 interface
            Contract erc20Interface = await LoadContractUtils.LoadContract(
                                                        contractName: "ERC20",
                                                        provider: parameters?.L1Provider,
                                                        isClassic: true
                                                        );


            var functionAbi = erc20Interface?.ContractBuilder?.GetFunctionAbi("approve");
            var contractByteCode = await parameters?.L1Provider?.Eth?.GetCode.SendRequestAsync(erc20Interface?.Address)!;

            // Define the base directory (assumed to be the root directory of the project)    //////
            //string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Construct the path to the ABI file dynamically
            //string abiFilePath = Path.Combine(baseDirectory, "src", "abi", "classic", "ERC20.json");

            //string contractByteCode = "0x60806040523480156200001157600080fd5b5060405162000ca538038062000ca5833981810160405260408110156200003757600080fd5b81019080805160405193929190846401000000008211156200005857600080fd5b9083019060208201858111156200006e57600080fd5b82516401000000008111828201881017156200008957600080fd5b82525081516020918201929091019080838360005b83811015620000b85781810151838201526020016200009e565b50505050905090810190601f168015620000e65780820380516001836020036101000a031916815260200191505b50604052602001805160405193929190846401000000008211156200010a57600080fd5b9083019060208201858111156200012057600080fd5b82516401000000008111828201881017156200013b57600080fd5b82525081516020918201929091019080838360005b838110156200016a57818101518382015260200162000150565b50505050905090810190601f168015620001985780820380516001836020036101000a031916815260200191505b5060405250508251620001b491506003906020850190620001e0565b508051620001ca906004906020840190620001e0565b50506005805460ff191660121790555062000285565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106200022357805160ff191683800117855562000253565b8280016001018555821562000253579182015b828111156200025357825182559160200191906001019062000236565b506200026192915062000265565b5090565b6200028291905b808211156200026157600081556001016200026c565b90565b610a1080620002956000396000f3fe608060405234801561001057600080fd5b50600436106100a95760003560e01c8063395093511161007157806339509351146101d957806370a082311461020557806395d89b411461022b578063a457c2d714610233578063a9059cbb1461025f578063dd62ed3e1461028b576100a9565b806306fdde03146100ae578063095ea7b31461012b57806318160ddd1461016b57806323b872dd14610185578063313ce567146101bb575b600080fd5b6100b66102b9565b6040805160208082528351818301528351919283929083019185019080838360005b838110156100f05781810151838201526020016100d8565b50505050905090810190601f16801561011d5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b6101576004803603604081101561014157600080fd5b506001600160a01b03813516906020013561034f565b604080519115158252519081900360200190f35b61017361036c565b60408051918252519081900360200190f35b6101576004803603606081101561019b57600080fd5b506001600160a01b03813581169160208101359091169060400135610372565b6101c36103ff565b6040805160ff9092168252519081900360200190f35b610157600480360360408110156101ef57600080fd5b506001600160a01b038135169060200135610408565b6101736004803603602081101561021b57600080fd5b50356001600160a01b031661045c565b6100b6610477565b6101576004803603604081101561024957600080fd5b506001600160a01b0381351690602001356104d8565b6101576004803603604081101561027557600080fd5b506001600160a01b038135169060200135610546565b610173600480360360408110156102a157600080fd5b506001600160a01b038135811691602001351661055a565b60038054604080516020601f60026000196101006001881615020190951694909404938401819004810282018101909252828152606093909290918301828280156103455780601f1061031a57610100808354040283529160200191610345565b820191906000526020600020905b81548152906001019060200180831161032857829003601f168201915b5050505050905090565b600061036361035c610585565b8484610589565b50600192915050565b60025490565b600061037f848484610675565b6103f58461038b610585565b6103f085604051806060016040528060288152602001610945602891396001600160a01b038a166000908152600160205260408120906103c9610585565b6001600160a01b03168152602081019190915260400160002054919063ffffffff6107dc16565b610589565b5060019392505050565b60055460ff1690565b6000610363610415610585565b846103f08560016000610426610585565b6001600160a01b03908116825260208083019390935260409182016000908120918c16815292529020549063ffffffff61087316565b6001600160a01b031660009081526020819052604090205490565b60048054604080516020601f60026000196101006001881615020190951694909404938401819004810282018101909252828152606093909290918301828280156103455780601f1061031a57610100808354040283529160200191610345565b60006103636104e5610585565b846103f0856040518060600160405280602581526020016109b6602591396001600061050f610585565b6001600160a01b03908116825260208083019390935260409182016000908120918d1681529252902054919063ffffffff6107dc16565b6000610363610553610585565b8484610675565b6001600160a01b03918216600090815260016020908152604080832093909416825291909152205490565b3390565b6001600160a01b0383166105ce5760405162461bcd60e51b81526004018080602001828103825260248152602001806109926024913960400191505060405180910390fd5b6001600160a01b0382166106135760405162461bcd60e51b81526004018080602001828103825260228152602001806108fd6022913960400191505060405180910390fd5b6001600160a01b03808416600081815260016020908152604080832094871680845294825291829020859055815185815291517f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b9259281900390910190a3505050565b6001600160a01b0383166106ba5760405162461bcd60e51b815260040180806020018281038252602581526020018061096d6025913960400191505060405180910390fd5b6001600160a01b0382166106ff5760405162461bcd60e51b81526004018080602001828103825260238152602001806108da6023913960400191505060405180910390fd5b61070a8383836108d4565b61074d8160405180606001604052806026815260200161091f602691396001600160a01b038616600090815260208190526040902054919063ffffffff6107dc16565b6001600160a01b038085166000908152602081905260408082209390935590841681522054610782908263ffffffff61087316565b6001600160a01b038084166000818152602081815260409182902094909455805185815290519193928716927fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef92918290030190a3505050565b6000818484111561086b5760405162461bcd60e51b81526004018080602001828103825283818151815260200191508051906020019080838360005b83811015610830578181015183820152602001610818565b50505050905090810190601f16801561085d5780820380516001836020036101000a031916815260200191505b509250505060405180910390fd5b505050900390565b6000828201838110156108cd576040805162461bcd60e51b815260206004820152601b60248201527f536166654d6174683a206164646974696f6e206f766572666c6f770000000000604482015290519081900360640190fd5b9392505050565b50505056fe45524332303a207472616e7366657220746f20746865207a65726f206164647265737345524332303a20617070726f766520746f20746865207a65726f206164647265737345524332303a207472616e7366657220616d6f756e7420657863656564732062616c616e636545524332303a207472616e7366657220616d6f756e74206578636565647320616c6c6f77616e636545524332303a207472616e736665722066726f6d20746865207a65726f206164647265737345524332303a20617070726f76652066726f6d20746865207a65726f206164647265737345524332303a2064656372656173656420616c6c6f77616e63652062656c6f77207a65726fa26469706673582212209f9b5b12792bdc1e6928c54586708bee2f74b32c3b7af812c0eb1c605569ab8d64736f6c634300060b0033";
            //var contractAbi = erc20Interface.ContractBuilder.ContractABI;
            //string functionData = new ConstructorCallEncoder().EncodeRequest(
            //    contractByteCode: contractByteCode,
            //    parameters: contractAbi.Constructor.InputParameters,
            //    values: new object[] { gatewayAddress, parameters.Amount ?? MAX_APPROVAL }
            //    );

            string functionData = new ConstructorCallEncoder().EncodeRequest(
                contractByteCode: contractByteCode,
                //contractByteCode: HelperMethods.GetBytecodeFromABI(abiFilePath) ?? null,
                parameters: functionAbi?.InputParameters,
                values: new object[] { gatewayAddress, parameters?.Amount ?? MAX_APPROVAL }
                );

            // Return a new TransactionRequest
            return new TransactionRequest
            {
                To = parameters?.Erc20L1Address,
                Data = functionData,
                Value = BigInteger.Zero
            };
        }

        private bool IsApproveParams(ApproveParamsOrTxRequest parameters)
        {
            // Check if parameters is of type SignerTokenApproveParams by checking if the erc20L1Address property is set
            return parameters is SignerTokenApproveParams signerParams && !string.IsNullOrEmpty(signerParams.Erc20L1Address);
        }

        public async Task<TransactionReceipt> ApproveToken(ApproveParamsOrTxRequest parameters)
        {
            // Check if the signer is connected to the correct L1 network
            await CheckL1Network(new SignerOrProvider(parameters?.L1Signer));

            TransactionRequest approveRequest;

            // Determine whether the parameters represent a SignerTokenApproveParams type
            if (IsApproveParams(parameters))
            {
                // Call GetApproveTokenRequest with appropriate parameters
                var provider = SignerProviderUtils.GetProviderOrThrow(parameters?.L1Signer);

                var signerTokenApproveParams = HelperMethods.CopyMatchingProperties<ProviderTokenApproveParams, ApproveParamsOrTxRequest>(parameters);

                approveRequest = await GetApproveTokenRequest(signerTokenApproveParams);
            }
            else
            {
                // If the parameters are not of type SignerTokenApproveParams, use the existing transaction request
                approveRequest = parameters?.TxRequest;
            }

            // Send the transaction and return the transaction receipt
            return await parameters?.L1Signer?.TransactionManager?.SendTransactionAndWaitForReceiptAsync(approveRequest);
        }

        public async Task<List<(WithdrawalInitiatedEvent? EventArgs, string? TxHash)>> GetL2WithdrawalEvents(
        Web3 l2Provider,
        string gatewayAddress,
        NewFilterInput filter,
        string? l1TokenAddress = null,
        string? fromAddress = null,
        string? toAddress = null)
        {
            await CheckL2Network(new SignerOrProvider(l2Provider));

            var argumentFilters = new Dictionary<string, object>();

            EventFetcher eventFetcher = new EventFetcher(l2Provider);

            var eventList = await eventFetcher.GetEventsAsync<WithdrawalInitiatedEvent>(
                contractFactory: "L2ArbitrumGateway",
                eventName: "WithdrawalInitiated",
                argumentFilters: argumentFilters,
                filter: new NewFilterInput()
                {
                    FromBlock = filter?.FromBlock,
                    ToBlock = filter?.ToBlock,
                    Address = new string[] { gatewayAddress },
                    Topics = filter?.Topics
                },
                isClassic: true);


            var formattedEvents = eventList.Select(a => (a?.Event, a?.TransactionHash)).ToList();

            if (!string.IsNullOrEmpty(l1TokenAddress))
            {
                eventList = eventList
                    .Where(log => log.Event.L1Token.Equals(l1TokenAddress, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return formattedEvents;
        }

        private async Task<bool> LooksLikeWethGateway(string potentialWethGatewayAddress, Web3 l1Provider)
        {
            try
            {
                var potentialWethGateway = await LoadContractUtils.LoadContract(
                                                        contractName: "L1WethGateway",
                                                        provider: l1Provider!,
                                                        address: potentialWethGatewayAddress,
                                                        isClassic: true
                                                        );
                await potentialWethGateway.GetFunction("l1Weth").CallAsync<string>();
                return true;
            }
            catch (Exception err)
            {
                if (err is Nethereum.JsonRpc.Client.RpcResponseException rpcErr && rpcErr.RpcError != null && rpcErr.RpcError.Message == "CALL_EXCEPTION")
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task<bool> IsWethGateway(string gatewayAddress, Web3 l1Provider)
        {
            string wethAddress = L2Network?.TokenBridge?.L1WethGateway;

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

            if (erc20L2Address.Equals(L2Network?.TokenBridge?.L2Weth, StringComparison.OrdinalIgnoreCase))
            {
                return L2Network?.TokenBridge?.L1Weth;
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
                                                contractName: "L2GatewayRouter",
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
            await CheckL1Network(new SignerOrProvider(l1Provider));

            var l1GatewayRouter = await LoadContractUtils.LoadContract(
                                                provider: l1Provider,
                                                contractName: "L1GatewayRouter",
                                                address: L2Network?.TokenBridge?.L2GatewayRouter,
                                                isClassic: true
                                                );

            var gatewayAddress = await l1GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1TokenAddress);
            return gatewayAddress == Constants.DISABLED_GATEWAY;
        }

        private DefaultedDepositRequest ApplyDefaults<T>(T parameters) where T : DepositRequest
        {
            //copying matching properties
            var defaultedParams = HelperMethods.CopyMatchingProperties<DefaultedDepositRequest, DepositRequest>(parameters);   //////

            // Assign default values if parameters are null
            defaultedParams.ExcessFeeRefundAddress ??= parameters?.From;
            defaultedParams.CallValueRefundAddress ??= parameters?.From;
            defaultedParams.DestinationAddress ??= parameters?.From;

            return defaultedParams;
        }

        private BigInteger? GetDepositRequestCallValue(L1ToL2MessageGasParams depositParams)
        {
            if (!this.NativeTokenIsEth)
            {
                return BigInteger.Zero;
            }

            // Calculate call value
            return depositParams?.GasLimit * depositParams?.MaxFeePerGas + depositParams?.MaxSubmissionCost;
        }

        private byte[] GetDepositRequestOutboundTransferInnerData(L1ToL2MessageGasParams depositParams)
        {
            ABIEncode encoder = new ABIEncode();

            if (!this.NativeTokenIsEth)
            {
                return encoder.GetABIEncoded(
                depositParams?.MaxSubmissionCost,
                "0x",
                depositParams?.GasLimit * depositParams?.MaxFeePerGas + depositParams?.MaxSubmissionCost
                );
            }
            else
            {
                return encoder.GetABIEncoded(
                    depositParams?.MaxSubmissionCost,
                    "0x"  ///////
                    );
            }
        }

        public async Task<L1ToL2TransactionRequest> GetDepositRequest(DepositRequest parameters)
        {
            await CheckL1Network(new SignerOrProvider(parameters?.L1Provider));
            await CheckL2Network(new SignerOrProvider(parameters?.L2Provider));

            DefaultedDepositRequest defaultedParams = ApplyDefaults(parameters);

            // Extracted variables
            var amount = defaultedParams?.Amount;
            var destinationAddress = defaultedParams?.DestinationAddress;
            var erc20L1Address = defaultedParams?.Erc20L1Address;
            var l1Provider = defaultedParams?.L1Provider;
            var l2Provider = defaultedParams?.L2Provider;
            var retryableGasOverrides = defaultedParams?.RetryableGasOverrides;

            // Get gateway address
            string l1GatewayAddress = await GetL1GatewayAddress(erc20L1Address, l1Provider);

            // Modify tokenGasOverrides if necessary
            GasOverrides tokenGasOverrides = retryableGasOverrides;

            if (l1GatewayAddress == L2Network?.TokenBridge?.L1CustomGateway)
            {
                if (tokenGasOverrides == null)
                {
                    tokenGasOverrides = new GasOverrides();
                }
                if (tokenGasOverrides.GasLimit == null)
                {
                    tokenGasOverrides.GasLimit = new GasOverrides()?.GasLimit;
                }
                if (tokenGasOverrides.GasLimit.Min == null)
                {
                    tokenGasOverrides.GasLimit.Min = Erc20Bridger.MIN_CUSTOM_DEPOSIT_GAS_LIMIT;
                }
            }

            var iGatewayRouter = await LoadContractUtils.LoadContract(
                        contractName: "L1GatewayRouter",
                        address: L2Network?.TokenBridge?.L1GatewayRouter,
                        provider: l1Provider,
                        isClassic: true
                    );
            var contractByteCode = await l1Provider?.Eth?.GetCode?.SendRequestAsync(iGatewayRouter.Address);

            // Define deposit function
            Func<L1ToL2MessageGasParams, TransactionRequest> depositFunc = (depositParams) =>
            {
                // Add missing maxSubmissionCost if necessary
                depositParams.MaxSubmissionCost = parameters?.MaxSubmissionCost ?? depositParams?.MaxSubmissionCost;

                byte[] innerData = GetDepositRequestOutboundTransferInnerData(depositParams);
                string functionData;
                if (defaultedParams.ExcessFeeRefundAddress != defaultedParams.From)
                {
                    var functionAbi = iGatewayRouter.ContractBuilder.GetFunctionAbi("outboundTransferCustomRefund");

                    // Define the base directory (assumed to be the root directory of the project)    //////
                    //string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    // Construct the path to the ABI file dynamically
                    //string abiFilePath = Path.Combine(baseDirectory, "src", "abi", "classic", "L1GatewayRouter.json");

                    //string contractByteCode = "0x608060405234801561001057600080fd5b50611f1d806100206000396000f3fe60806040526004361061011f5760003560e01c806393e59dc1116100a0578063d2ce7d6511610064578063d2ce7d651461066a578063dd61456914610704578063ed08fdc61461073c578063f887ea401461076f578063fb0e722b146107845761011f565b806393e59dc11461048c57806395fcea78146104a1578063a0c76a96146104b6578063a7e28d4814610604578063bda009fe146106375761011f565b80632e567b36116100e75780632e567b361461024857806347466f98146102de5780635625a95214610311578063658b53f4146103495780638da5cb5b146104775761011f565b8063032958021461012457806313af4035146101555780631459457a1461018a5780632d67b72d146101df5780632db09c1c14610233575b600080fd5b34801561013057600080fd5b50610139610799565b604080516001600160a01b039092168252519081900360200190f35b34801561016157600080fd5b506101886004803603602081101561017857600080fd5b50356001600160a01b03166107a8565b005b34801561019657600080fd5b50610188600480360360a08110156101ad57600080fd5b506001600160a01b03813581169160208101358216916040820135811691606081013582169160809091013516610861565b610221600480360360a08110156101f557600080fd5b506001600160a01b038135811691602081013591604082013591606081013591608090910135166108af565b60408051918252519081900360200190f35b34801561023f57600080fd5b50610139610b0c565b610188600480360360a081101561025e57600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b8111156102a057600080fd5b8201836020820111156102b257600080fd5b803590602001918460018302840111600160201b831117156102d357600080fd5b509092509050610b1b565b3480156102ea57600080fd5b506101886004803603602081101561030157600080fd5b50356001600160a01b0316610b5f565b6102216004803603608081101561032757600080fd5b506001600160a01b038135169060208101359060408101359060600135610c02565b610221600480360360a081101561035f57600080fd5b810190602081018135600160201b81111561037957600080fd5b82018360208201111561038b57600080fd5b803590602001918460208302840111600160201b831117156103ac57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295949360208101935035915050600160201b8111156103fb57600080fd5b82018360208201111561040d57600080fd5b803590602001918460208302840111600160201b8311171561042e57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295505082359350505060208101359060400135610d9f565b34801561048357600080fd5b50610139610e06565b34801561049857600080fd5b50610139610e15565b3480156104ad57600080fd5b50610188610e24565b3480156104c257600080fd5b5061058f600480360360a08110156104d957600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b81111561051b57600080fd5b82018360208201111561052d57600080fd5b803590602001918460018302840111600160201b8311171561054e57600080fd5b91908080601f016020809104026020016040519081016040528093929190818152602001838380828437600092019190915250929550610e81945050505050565b6040805160208082528351818301528351919283929083019185019080838360005b838110156105c95781810151838201526020016105b1565b50505050905090810190601f1680156105f65780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561061057600080fd5b506101396004803603602081101561062757600080fd5b50356001600160a01b0316611083565b34801561064357600080fd5b506101396004803603602081101561065a57600080fd5b50356001600160a01b0316611134565b61058f600480360360c081101561068057600080fd5b6001600160a01b0382358116926020810135909116916040820135916060810135916080820135919081019060c0810160a0820135600160201b8111156106c657600080fd5b8201836020820111156106d857600080fd5b803590602001918460018302840111600160201b831117156106f957600080fd5b509092509050611196565b6102216004803603608081101561071a57600080fd5b506001600160a01b0381351690602081013590604081013590606001356113ba565b34801561074857600080fd5b506101396004803603602081101561075f57600080fd5b50356001600160a01b03166113d2565b34801561077b57600080fd5b506101396113ed565b34801561079057600080fd5b506101396113fc565b6004546001600160a01b031681565b6005546001600160a01b031633146107f4576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b6001600160a01b03811661083f576040805162461bcd60e51b815260206004820152600d60248201526c24a72b20a624a22fa7aba722a960991b604482015290519081900360640190fd5b600580546001600160a01b0319166001600160a01b0392909216919091179055565b61086d8260008661140b565b600580546001600160a01b03199081166001600160a01b0397881617909155600080548216948716949094179093556006805490931694169390931790555050565b600061a4b160ff16336001600160a01b0316638e5f5ad16040518163ffffffff1660e01b815260040160206040518083038186803b1580156108f057600080fd5b505afa158015610904573d6000803e3d6000fd5b505050506040513d602081101561091a57600080fd5b505160ff1614610963576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d0549097d1539050931151608a1b604482015290519081900360640190fd5b610975866001600160a01b0316611482565b6109b8576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d513d7d0d3d395149050d5608a1b604482015290519081900360640190fd5b60006109c333611134565b90506001600160a01b038116158015906109eb57506004546001600160a01b03828116911614155b15610a5657866001600160a01b0316816001600160a01b031614610a56576040805162461bcd60e51b815260206004820152601b60248201527f4e4f5f5550444154455f544f5f444946464552454e545f414444520000000000604482015290519081900360640190fd5b604080516001808252818301909252606091602080830190803683370190505090503381600081518110610a8657fe5b6001600160a01b0392909216602092830291909101909101526040805160018082528183019092526060918160200160208202803683370190505090508881600081518110610ad157fe5b60200260200101906001600160a01b031690816001600160a01b031681525050610aff82828a8a8a8a611488565b9998505050505050505050565b6001546001600160a01b031681565b6040805162461bcd60e51b815260206004820152601460248201527327a7262cafa7aaaa2127aaa7222fa927aaaa22a960611b604482015290519081900360640190fd5b6000546001600160a01b03163314610bae576040805162461bcd60e51b815260206004820152600d60248201526c1393d517d19493d357d31254d5609a1b604482015290519081900360640190fd5b600080546001600160a01b0383166001600160a01b0319909116811790915560408051918252517f37389c47920d5cc3229678a0205d0455002c07541a4139ebdce91ac2274657779181900360200190a150565b6005546000906001600160a01b03163314610c51576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b600480546001600160a01b0387166001600160a01b0319909116811790915560408051918252517f3a8f8eb961383a94d41d193e16a3af73eaddfd5764a4c640257323a1603ac3319181900360200190a160006001600160a01b03861615610d1b57856001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b158015610cec57600080fd5b505afa158015610d00573d6000803e3d6000fd5b505050506040513d6020811015610d1657600080fd5b505190505b604080516001600160a01b038084166024808401919091528351808403909101815260449092018352602082810180516001600160e01b031663f7c9362f60e01b17905260065460015485516060810187528981529283018b90529482018990529293610d949383169216903390349060009087611905565b979650505050505050565b6005546000906001600160a01b03163314610dee576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b610dfc868686868633611488565b9695505050505050565b6005546001600160a01b031681565b6000546001600160a01b031681565b6000610e2e611924565b9050336001600160a01b03821614610e7e576040805162461bcd60e51b815260206004820152600e60248201526d2727aa2fa32927a6afa0a226a4a760911b604482015290519081900360640190fd5b50565b60606000610e8e87611134565b9050806001600160a01b031663a0c76a9688888888886040518663ffffffff1660e01b815260040180866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b03168152602001846001600160a01b03166001600160a01b0316815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015610f3e578181015183820152602001610f26565b50505050905090810190601f168015610f6b5780820380516001836020036101000a031916815260200191505b50965050505050505060006040518083038186803b158015610f8c57600080fd5b505afa158015610fa0573d6000803e3d6000fd5b505050506040513d6000823e601f3d908101601f191682016040526020811015610fc957600080fd5b8101908080516040519392919084600160201b821115610fe857600080fd5b908301906020820185811115610ffd57600080fd5b8251600160201b81118282018810171561101657600080fd5b82525081516020918201929091019080838360005b8381101561104357818101518382015260200161102b565b50505050905090810190601f1680156110705780820380516001836020036101000a031916815260200191505b5060405250505091505095945050505050565b60008061108f83611134565b90506001600160a01b0381166110a957600091505061112f565b806001600160a01b031663a7e28d48846040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b1580156110ff57600080fd5b505afa158015611113573d6000803e3d6000fd5b505050506040513d602081101561112957600080fd5b50519150505b919050565b6001600160a01b03808216600090815260036020526040902054168061116257506004546001600160a01b03165b6001600160a01b038116600114806111895750611187816001600160a01b0316611482565b155b1561112f5750600061112f565b6000546060906001600160a01b031615611264576000546040805163babcc53960e01b815233600482015290516001600160a01b039092169163babcc53991602480820192602092909190829003018186803b1580156111f557600080fd5b505afa158015611209573d6000803e3d6000fd5b505050506040513d602081101561121f57600080fd5b5051611264576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d5d2125511531254d51151608a1b604482015290519081900360640190fd5b60008383604081101561127657600080fd5b81359190810190604081016020820135600160201b81111561129757600080fd5b8201836020820111156112a957600080fd5b803590602001918460018302840111600160201b831117156112ca57600080fd5b91908080601f0160208091040260200160405190810160405280939291908181526020018383808284376000920191909152509697505050508989028501935050508215159050611357576040805162461bcd60e51b81526020600482015260126024820152711393d7d4d550935254d4d253d397d0d3d4d560721b604482015290519081900360640190fd5b80341461139d576040805162461bcd60e51b815260206004820152600f60248201526e57524f4e475f4554485f56414c554560881b604482015290519081900360640190fd5b6113ac8a8a8a8a8a8a8a611949565b9a9950505050505050505050565b60006113c985858585336108af565b95945050505050565b6003602052600090815260409020546001600160a01b031681565b6002546001600160a01b031681565b6006546001600160a01b031681565b6001600160a01b03821615611454576040805162461bcd60e51b815260206004820152600a6024820152692120a22fa927aaaa22a960b11b604482015290519081900360640190fd5b61145e8383611b9e565b600480546001600160a01b0319166001600160a01b03929092169190911790555050565b3b151590565b600085518751146114cf576040805162461bcd60e51b815260206004820152600c60248201526b0aea49e9c8ebe988a9c8ea8960a31b604482015290519081900360640190fd5b60005b87518110156117d0578681815181106114e757fe5b6020026020010151600360008a84815181106114ff57fe5b60200260200101516001600160a01b03166001600160a01b0316815260200190815260200160002060006101000a8154816001600160a01b0302191690836001600160a01b0316021790555086818151811061155757fe5b60200260200101516001600160a01b031688828151811061157457fe5b60200260200101516001600160a01b03167f812ca95fe4492a9e2d1f2723c2c40c03a60a27b059581ae20ac4e4d73bfba35460405160405180910390a360006001600160a01b03168782815181106115c857fe5b60200260200101516001600160a01b03161415801561160d575060016001600160a01b03168782815181106115f957fe5b60200260200101516001600160a01b031614155b156117c85760006001600160a01b031687828151811061162957fe5b60200260200101516001600160a01b031663a7e28d488a848151811061164b57fe5b60200260200101516040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b15801561169957600080fd5b505afa1580156116ad573d6000803e3d6000fd5b505050506040513d60208110156116c357600080fd5b50516001600160a01b03161415611721576040805162461bcd60e51b815260206004820152601c60248201527f544f4b454e5f4e4f545f48414e444c45445f42595f4741544557415900000000604482015290519081900360640190fd5b86818151811061172d57fe5b60200260200101516001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b15801561176d57600080fd5b505afa158015611781573d6000803e3d6000fd5b505050506040513d602081101561179757600080fd5b505187518890839081106117a757fe5b60200260200101906001600160a01b031690816001600160a01b0316815250505b6001016114d2565b506060634201f98560e01b8888604051602401808060200180602001838103835285818151815260200191508051906020019060200280838360005b8381101561182457818101518382015260200161180c565b50505050905001838103825284818151815260200191508051906020019060200280838360005b8381101561186357818101518382015260200161184b565b50505050905001945050505050604051602081830303815290604052906001600160e01b0319166020820180516001600160e01b03838183161783525050505090506118f9600660009054906101000a90046001600160a01b0316600160009054906101000a90046001600160a01b03168534600060405180606001604052808b81526020018d81526020018c81525087611905565b98975050505050505050565b60006118f98888888888886000015189602001518a604001518a611c6a565b7fb53127684a568b3173ae13b9f8a6016e243e63b6e8ee1178d6a717850b5d61035490565b6060600061195689611134565b90506060611965338686611e7d565b604080516001600160a01b0385811682529151929350818c169233928e16917f85291dff2161a93c2f12c819d31889c96c63042116f5bc5a205aa701c2c429f5919081900360200190a4816001600160a01b031663d2ce7d65348c8c8c8c8c886040518863ffffffff1660e01b815260040180876001600160a01b03166001600160a01b03168152602001866001600160a01b03166001600160a01b0316815260200185815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611a53578181015183820152602001611a3b565b50505050905090810190601f168015611a805780820380516001836020036101000a031916815260200191505b509750505050505050506000604051808303818588803b158015611aa357600080fd5b505af1158015611ab7573d6000803e3d6000fd5b50505050506040513d6000823e601f3d908101601f191682016040526020811015611ae157600080fd5b8101908080516040519392919084600160201b821115611b0057600080fd5b908301906020820185811115611b1557600080fd5b8251600160201b811182820188101715611b2e57600080fd5b82525081516020918201929091019080838360005b83811015611b5b578181015183820152602001611b43565b50505050905090810190601f168015611b885780820380516001836020036101000a031916815260200191505b5060405250505092505050979650505050505050565b6001600160a01b038216611bef576040805162461bcd60e51b81526020600482015260136024820152721253959053125117d0d3d55395115494105495606a1b604482015290519081900360640190fd5b6001546001600160a01b031615611c3c576040805162461bcd60e51b815260206004820152600c60248201526b1053149150511657d253925560a21b604482015290519081900360640190fd5b600180546001600160a01b039384166001600160a01b03199182161790915560028054929093169116179055565b6000808a6001600160a01b031663679b6ded898c8a8a8e8f8c8c8c6040518a63ffffffff1660e01b815260040180896001600160a01b03166001600160a01b03168152602001888152602001878152602001866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b0316815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611d31578181015183820152602001611d19565b50505050905090810190601f168015611d5e5780820380516001836020036101000a031916815260200191505b5099505050505050505050506020604051808303818588803b158015611d8357600080fd5b505af1158015611d97573d6000803e3d6000fd5b50505050506040513d6020811015611dae57600080fd5b81019080805190602001909291905050509050808a6001600160a01b03168a6001600160a01b03167fc1d1490cf25c3b40d600dfb27c7680340ed1ab901b7e8f3551280968a3b372b0866040518080602001828103825283818151815260200191508051906020019080838360005b83811015611e35578181015183820152602001611e1d565b50505050905090810190601f168015611e625780820380516001836020036101000a031916815260200191505b509250505060405180910390a49a9950505050505050505050565b606083838360405160200180846001600160a01b03166001600160a01b0316815260200180602001828103825284848281815260200192508082843760008184015260408051601f19601f909301831690940184810390920184525250999850505050505050505056fea2646970667358221220209a87e0b2b16371b8726764d296199cdef67a4e7dbc1a7892b6be8216dc205164736f6c634300060b0033"
                    //var contractAbi = iGatewayRouter.ContractBuilder.ContractABI;
                    //string functionData = new ConstructorCallEncoder().EncodeRequest(
                    //    contractByteCode: contractByteCode,
                    //    parameters: contractAbi.Constructor.InputParameters,
                    //    values: new object[] { gatewayAddress, parameters.Amount ?? MAX_APPROVAL }
                    //    );

                    functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: contractByteCode,
                        //contractByteCode: HelperMethods.GetBytecodeFromABI(abiFilePath) ?? null,
                        parameters: functionAbi?.InputParameters,
                        values: new object[]
                        {
                            AddressUtil.Current.ConvertToChecksumAddress(erc20L1Address),
                            AddressUtil.Current.ConvertToChecksumAddress(defaultedParams?.ExcessFeeRefundAddress),
                            AddressUtil.Current.ConvertToChecksumAddress(destinationAddress),
                            (int)amount!,
                            (int)depositParams?.GasLimit!,
                            (int)depositParams?.MaxFeePerGas!,
                            innerData
                        });
                }
                else
                {
                    var functionAbi = iGatewayRouter.ContractBuilder.GetFunctionAbi("outboundTransfer");

                    // Define the base directory (assumed to be the root directory of the project)    //////
                    //string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    // Construct the path to the ABI file dynamically
                    //string abiFilePath = Path.Combine(baseDirectory, "src", "abi", "classic", "L1GatewayRouter.json");

                    //string contractByteCode = "0x608060405234801561001057600080fd5b50611f1d806100206000396000f3fe60806040526004361061011f5760003560e01c806393e59dc1116100a0578063d2ce7d6511610064578063d2ce7d651461066a578063dd61456914610704578063ed08fdc61461073c578063f887ea401461076f578063fb0e722b146107845761011f565b806393e59dc11461048c57806395fcea78146104a1578063a0c76a96146104b6578063a7e28d4814610604578063bda009fe146106375761011f565b80632e567b36116100e75780632e567b361461024857806347466f98146102de5780635625a95214610311578063658b53f4146103495780638da5cb5b146104775761011f565b8063032958021461012457806313af4035146101555780631459457a1461018a5780632d67b72d146101df5780632db09c1c14610233575b600080fd5b34801561013057600080fd5b50610139610799565b604080516001600160a01b039092168252519081900360200190f35b34801561016157600080fd5b506101886004803603602081101561017857600080fd5b50356001600160a01b03166107a8565b005b34801561019657600080fd5b50610188600480360360a08110156101ad57600080fd5b506001600160a01b03813581169160208101358216916040820135811691606081013582169160809091013516610861565b610221600480360360a08110156101f557600080fd5b506001600160a01b038135811691602081013591604082013591606081013591608090910135166108af565b60408051918252519081900360200190f35b34801561023f57600080fd5b50610139610b0c565b610188600480360360a081101561025e57600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b8111156102a057600080fd5b8201836020820111156102b257600080fd5b803590602001918460018302840111600160201b831117156102d357600080fd5b509092509050610b1b565b3480156102ea57600080fd5b506101886004803603602081101561030157600080fd5b50356001600160a01b0316610b5f565b6102216004803603608081101561032757600080fd5b506001600160a01b038135169060208101359060408101359060600135610c02565b610221600480360360a081101561035f57600080fd5b810190602081018135600160201b81111561037957600080fd5b82018360208201111561038b57600080fd5b803590602001918460208302840111600160201b831117156103ac57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295949360208101935035915050600160201b8111156103fb57600080fd5b82018360208201111561040d57600080fd5b803590602001918460208302840111600160201b8311171561042e57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295505082359350505060208101359060400135610d9f565b34801561048357600080fd5b50610139610e06565b34801561049857600080fd5b50610139610e15565b3480156104ad57600080fd5b50610188610e24565b3480156104c257600080fd5b5061058f600480360360a08110156104d957600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b81111561051b57600080fd5b82018360208201111561052d57600080fd5b803590602001918460018302840111600160201b8311171561054e57600080fd5b91908080601f016020809104026020016040519081016040528093929190818152602001838380828437600092019190915250929550610e81945050505050565b6040805160208082528351818301528351919283929083019185019080838360005b838110156105c95781810151838201526020016105b1565b50505050905090810190601f1680156105f65780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561061057600080fd5b506101396004803603602081101561062757600080fd5b50356001600160a01b0316611083565b34801561064357600080fd5b506101396004803603602081101561065a57600080fd5b50356001600160a01b0316611134565b61058f600480360360c081101561068057600080fd5b6001600160a01b0382358116926020810135909116916040820135916060810135916080820135919081019060c0810160a0820135600160201b8111156106c657600080fd5b8201836020820111156106d857600080fd5b803590602001918460018302840111600160201b831117156106f957600080fd5b509092509050611196565b6102216004803603608081101561071a57600080fd5b506001600160a01b0381351690602081013590604081013590606001356113ba565b34801561074857600080fd5b506101396004803603602081101561075f57600080fd5b50356001600160a01b03166113d2565b34801561077b57600080fd5b506101396113ed565b34801561079057600080fd5b506101396113fc565b6004546001600160a01b031681565b6005546001600160a01b031633146107f4576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b6001600160a01b03811661083f576040805162461bcd60e51b815260206004820152600d60248201526c24a72b20a624a22fa7aba722a960991b604482015290519081900360640190fd5b600580546001600160a01b0319166001600160a01b0392909216919091179055565b61086d8260008661140b565b600580546001600160a01b03199081166001600160a01b0397881617909155600080548216948716949094179093556006805490931694169390931790555050565b600061a4b160ff16336001600160a01b0316638e5f5ad16040518163ffffffff1660e01b815260040160206040518083038186803b1580156108f057600080fd5b505afa158015610904573d6000803e3d6000fd5b505050506040513d602081101561091a57600080fd5b505160ff1614610963576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d0549097d1539050931151608a1b604482015290519081900360640190fd5b610975866001600160a01b0316611482565b6109b8576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d513d7d0d3d395149050d5608a1b604482015290519081900360640190fd5b60006109c333611134565b90506001600160a01b038116158015906109eb57506004546001600160a01b03828116911614155b15610a5657866001600160a01b0316816001600160a01b031614610a56576040805162461bcd60e51b815260206004820152601b60248201527f4e4f5f5550444154455f544f5f444946464552454e545f414444520000000000604482015290519081900360640190fd5b604080516001808252818301909252606091602080830190803683370190505090503381600081518110610a8657fe5b6001600160a01b0392909216602092830291909101909101526040805160018082528183019092526060918160200160208202803683370190505090508881600081518110610ad157fe5b60200260200101906001600160a01b031690816001600160a01b031681525050610aff82828a8a8a8a611488565b9998505050505050505050565b6001546001600160a01b031681565b6040805162461bcd60e51b815260206004820152601460248201527327a7262cafa7aaaa2127aaa7222fa927aaaa22a960611b604482015290519081900360640190fd5b6000546001600160a01b03163314610bae576040805162461bcd60e51b815260206004820152600d60248201526c1393d517d19493d357d31254d5609a1b604482015290519081900360640190fd5b600080546001600160a01b0383166001600160a01b0319909116811790915560408051918252517f37389c47920d5cc3229678a0205d0455002c07541a4139ebdce91ac2274657779181900360200190a150565b6005546000906001600160a01b03163314610c51576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b600480546001600160a01b0387166001600160a01b0319909116811790915560408051918252517f3a8f8eb961383a94d41d193e16a3af73eaddfd5764a4c640257323a1603ac3319181900360200190a160006001600160a01b03861615610d1b57856001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b158015610cec57600080fd5b505afa158015610d00573d6000803e3d6000fd5b505050506040513d6020811015610d1657600080fd5b505190505b604080516001600160a01b038084166024808401919091528351808403909101815260449092018352602082810180516001600160e01b031663f7c9362f60e01b17905260065460015485516060810187528981529283018b90529482018990529293610d949383169216903390349060009087611905565b979650505050505050565b6005546000906001600160a01b03163314610dee576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b610dfc868686868633611488565b9695505050505050565b6005546001600160a01b031681565b6000546001600160a01b031681565b6000610e2e611924565b9050336001600160a01b03821614610e7e576040805162461bcd60e51b815260206004820152600e60248201526d2727aa2fa32927a6afa0a226a4a760911b604482015290519081900360640190fd5b50565b60606000610e8e87611134565b9050806001600160a01b031663a0c76a9688888888886040518663ffffffff1660e01b815260040180866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b03168152602001846001600160a01b03166001600160a01b0316815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015610f3e578181015183820152602001610f26565b50505050905090810190601f168015610f6b5780820380516001836020036101000a031916815260200191505b50965050505050505060006040518083038186803b158015610f8c57600080fd5b505afa158015610fa0573d6000803e3d6000fd5b505050506040513d6000823e601f3d908101601f191682016040526020811015610fc957600080fd5b8101908080516040519392919084600160201b821115610fe857600080fd5b908301906020820185811115610ffd57600080fd5b8251600160201b81118282018810171561101657600080fd5b82525081516020918201929091019080838360005b8381101561104357818101518382015260200161102b565b50505050905090810190601f1680156110705780820380516001836020036101000a031916815260200191505b5060405250505091505095945050505050565b60008061108f83611134565b90506001600160a01b0381166110a957600091505061112f565b806001600160a01b031663a7e28d48846040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b1580156110ff57600080fd5b505afa158015611113573d6000803e3d6000fd5b505050506040513d602081101561112957600080fd5b50519150505b919050565b6001600160a01b03808216600090815260036020526040902054168061116257506004546001600160a01b03165b6001600160a01b038116600114806111895750611187816001600160a01b0316611482565b155b1561112f5750600061112f565b6000546060906001600160a01b031615611264576000546040805163babcc53960e01b815233600482015290516001600160a01b039092169163babcc53991602480820192602092909190829003018186803b1580156111f557600080fd5b505afa158015611209573d6000803e3d6000fd5b505050506040513d602081101561121f57600080fd5b5051611264576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d5d2125511531254d51151608a1b604482015290519081900360640190fd5b60008383604081101561127657600080fd5b81359190810190604081016020820135600160201b81111561129757600080fd5b8201836020820111156112a957600080fd5b803590602001918460018302840111600160201b831117156112ca57600080fd5b91908080601f0160208091040260200160405190810160405280939291908181526020018383808284376000920191909152509697505050508989028501935050508215159050611357576040805162461bcd60e51b81526020600482015260126024820152711393d7d4d550935254d4d253d397d0d3d4d560721b604482015290519081900360640190fd5b80341461139d576040805162461bcd60e51b815260206004820152600f60248201526e57524f4e475f4554485f56414c554560881b604482015290519081900360640190fd5b6113ac8a8a8a8a8a8a8a611949565b9a9950505050505050505050565b60006113c985858585336108af565b95945050505050565b6003602052600090815260409020546001600160a01b031681565b6002546001600160a01b031681565b6006546001600160a01b031681565b6001600160a01b03821615611454576040805162461bcd60e51b815260206004820152600a6024820152692120a22fa927aaaa22a960b11b604482015290519081900360640190fd5b61145e8383611b9e565b600480546001600160a01b0319166001600160a01b03929092169190911790555050565b3b151590565b600085518751146114cf576040805162461bcd60e51b815260206004820152600c60248201526b0aea49e9c8ebe988a9c8ea8960a31b604482015290519081900360640190fd5b60005b87518110156117d0578681815181106114e757fe5b6020026020010151600360008a84815181106114ff57fe5b60200260200101516001600160a01b03166001600160a01b0316815260200190815260200160002060006101000a8154816001600160a01b0302191690836001600160a01b0316021790555086818151811061155757fe5b60200260200101516001600160a01b031688828151811061157457fe5b60200260200101516001600160a01b03167f812ca95fe4492a9e2d1f2723c2c40c03a60a27b059581ae20ac4e4d73bfba35460405160405180910390a360006001600160a01b03168782815181106115c857fe5b60200260200101516001600160a01b03161415801561160d575060016001600160a01b03168782815181106115f957fe5b60200260200101516001600160a01b031614155b156117c85760006001600160a01b031687828151811061162957fe5b60200260200101516001600160a01b031663a7e28d488a848151811061164b57fe5b60200260200101516040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b15801561169957600080fd5b505afa1580156116ad573d6000803e3d6000fd5b505050506040513d60208110156116c357600080fd5b50516001600160a01b03161415611721576040805162461bcd60e51b815260206004820152601c60248201527f544f4b454e5f4e4f545f48414e444c45445f42595f4741544557415900000000604482015290519081900360640190fd5b86818151811061172d57fe5b60200260200101516001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b15801561176d57600080fd5b505afa158015611781573d6000803e3d6000fd5b505050506040513d602081101561179757600080fd5b505187518890839081106117a757fe5b60200260200101906001600160a01b031690816001600160a01b0316815250505b6001016114d2565b506060634201f98560e01b8888604051602401808060200180602001838103835285818151815260200191508051906020019060200280838360005b8381101561182457818101518382015260200161180c565b50505050905001838103825284818151815260200191508051906020019060200280838360005b8381101561186357818101518382015260200161184b565b50505050905001945050505050604051602081830303815290604052906001600160e01b0319166020820180516001600160e01b03838183161783525050505090506118f9600660009054906101000a90046001600160a01b0316600160009054906101000a90046001600160a01b03168534600060405180606001604052808b81526020018d81526020018c81525087611905565b98975050505050505050565b60006118f98888888888886000015189602001518a604001518a611c6a565b7fb53127684a568b3173ae13b9f8a6016e243e63b6e8ee1178d6a717850b5d61035490565b6060600061195689611134565b90506060611965338686611e7d565b604080516001600160a01b0385811682529151929350818c169233928e16917f85291dff2161a93c2f12c819d31889c96c63042116f5bc5a205aa701c2c429f5919081900360200190a4816001600160a01b031663d2ce7d65348c8c8c8c8c886040518863ffffffff1660e01b815260040180876001600160a01b03166001600160a01b03168152602001866001600160a01b03166001600160a01b0316815260200185815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611a53578181015183820152602001611a3b565b50505050905090810190601f168015611a805780820380516001836020036101000a031916815260200191505b509750505050505050506000604051808303818588803b158015611aa357600080fd5b505af1158015611ab7573d6000803e3d6000fd5b50505050506040513d6000823e601f3d908101601f191682016040526020811015611ae157600080fd5b8101908080516040519392919084600160201b821115611b0057600080fd5b908301906020820185811115611b1557600080fd5b8251600160201b811182820188101715611b2e57600080fd5b82525081516020918201929091019080838360005b83811015611b5b578181015183820152602001611b43565b50505050905090810190601f168015611b885780820380516001836020036101000a031916815260200191505b5060405250505092505050979650505050505050565b6001600160a01b038216611bef576040805162461bcd60e51b81526020600482015260136024820152721253959053125117d0d3d55395115494105495606a1b604482015290519081900360640190fd5b6001546001600160a01b031615611c3c576040805162461bcd60e51b815260206004820152600c60248201526b1053149150511657d253925560a21b604482015290519081900360640190fd5b600180546001600160a01b039384166001600160a01b03199182161790915560028054929093169116179055565b6000808a6001600160a01b031663679b6ded898c8a8a8e8f8c8c8c6040518a63ffffffff1660e01b815260040180896001600160a01b03166001600160a01b03168152602001888152602001878152602001866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b0316815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611d31578181015183820152602001611d19565b50505050905090810190601f168015611d5e5780820380516001836020036101000a031916815260200191505b5099505050505050505050506020604051808303818588803b158015611d8357600080fd5b505af1158015611d97573d6000803e3d6000fd5b50505050506040513d6020811015611dae57600080fd5b81019080805190602001909291905050509050808a6001600160a01b03168a6001600160a01b03167fc1d1490cf25c3b40d600dfb27c7680340ed1ab901b7e8f3551280968a3b372b0866040518080602001828103825283818151815260200191508051906020019080838360005b83811015611e35578181015183820152602001611e1d565b50505050905090810190601f168015611e625780820380516001836020036101000a031916815260200191505b509250505060405180910390a49a9950505050505050505050565b606083838360405160200180846001600160a01b03166001600160a01b0316815260200180602001828103825284848281815260200192508082843760008184015260408051601f19601f909301831690940184810390920184525250999850505050505050505056fea2646970667358221220209a87e0b2b16371b8726764d296199cdef67a4e7dbc1a7892b6be8216dc205164736f6c634300060b0033"
                    //var contractAbi = iGatewayRouter.ContractBuilder.ContractABI;
                    //string functionData = new ConstructorCallEncoder().EncodeRequest(
                    //    contractByteCode: contractByteCode,
                    //    parameters: contractAbi.Constructor.InputParameters,
                    //    values: new object[] { gatewayAddress, parameters.Amount ?? MAX_APPROVAL }
                    //    );

                    functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: contractByteCode,
                        //contractByteCode: HelperMethods.GetBytecodeFromABI(abiFilePath) ?? null,
                        parameters: functionAbi?.InputParameters,
                        values: new object[]
                        {
                            AddressUtil.Current.ConvertToChecksumAddress(erc20L1Address),
                            AddressUtil.Current.ConvertToChecksumAddress(destinationAddress),
                            (int)amount!,
                            (int)depositParams?.GasLimit!,
                            (int)depositParams?.MaxFeePerGas!,
                            innerData
                        });
                }

                return new TransactionRequest
                {
                    Data = functionData,
                    To = L2Network?.TokenBridge?.L1GatewayRouter,
                    From = defaultedParams?.From,
                    Value = GetDepositRequestCallValue(depositParams)
                };
            };

            L1ToL2MessageGasEstimator gasEstimator = new L1ToL2MessageGasEstimator(l2Provider);

            PopulateFunctionParamsResult estimates = await gasEstimator.PopulateFunctionParams(depositFunc, l1Provider, tokenGasOverrides);

            return new L1ToL2TransactionRequest
            {
                TxRequest = new TransactionRequest
                {
                    To = L2Network?.TokenBridge?.L1GatewayRouter,
                    Data = estimates?.Data,
                    Value = estimates?.Value,
                    From = parameters?.From,
                },
                RetryableData = new RetryableData
                {
                    Data = Encoding.UTF8.GetBytes(estimates?.Data!),
                    To = estimates?.To,
                    Deposit = estimates?.Retryable?.Deposit,
                    From = estimates?.Retryable?.From,
                    ExcessFeeRefundAddress = estimates?.Retryable?.ExcessFeeRefundAddress,
                    CallValueRefundAddress = estimates?.Retryable?.CallValueRefundAddress,
                    GasLimit = estimates?.Retryable?.GasLimit,
                    L2CallValue = estimates?.Retryable?.L2CallValue,
                    MaxFeePerGas = estimates?.Retryable?.MaxFeePerGas,
                    MaxSubmissionCost = estimates?.Retryable?.MaxSubmissionCost
                },
                IsValid = async () => await L1ToL2MessageGasEstimator.IsValid(estimates?.Estimates, (await gasEstimator.PopulateFunctionParams(depositFunc, l1Provider, tokenGasOverrides))?.Estimates)
            };
        }

        public override async Task<L1TransactionReceipt> Deposit(dynamic parameters)
        {
            await CheckL1Network(new SignerOrProvider(parameters?.L1Signer));

            // Check for unexpected values
            if (parameters?.Overrides?.Value != null)
            {
                throw new ArbSdkError("L1 call value should be set through l1CallValue param.");
            }
            dynamic tokenDeposit;

            Web3 l1Provider = SignerProviderUtils.GetProviderOrThrow(parameters?.L1Signer);

            if (DataEntities.TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                tokenDeposit = parameters;
            }

            else if (parameters is Erc20DepositParams)
            {
                tokenDeposit = await GetDepositRequest(new DepositRequest
                {
                    Erc20L1Address = parameters?.Erc20L1Address,
                    L1Provider = l1Provider,
                    From = parameters?.L1Signer?.Address,
                    Amount = parameters?.Amount,
                    DestinationAddress = parameters?.DestinationAddress,
                    L2Provider = parameters?.L2Provider,
                    RetryableGasOverrides = parameters?.RetryableGasOverrides,
                    CallValueRefundAddress = parameters?.CallValueRefundAddress,
                    ExcessFeeRefundAddress = parameters?.ExcessFeeRefundAddress,
                    MaxSubmissionCost = parameters?.MaxSubmissionCost,
                    Overrides = parameters?.Overrides,
                    L1Signer = parameters?.L1Signer
                });
            }
            else if(parameters is L1ToL2TxReqAndSignerProvider)
            {
                tokenDeposit = await GetDepositRequest(new DepositRequest
                {
                    L1Provider = l1Provider,
                    From = parameters?.L1Signer?.Address,
                    L2Provider = parameters?.L2Provider,
                    Overrides = parameters?.Overrides,
                    L1Signer = parameters?.L1Signer
                });
            }

            else
            {
                throw new ArgumentException("Invalid parameter type. Expected Erc20DepositParams or L1ToL2TxReqAndSignerProvider.");
            }

            var tx = new TransactionRequest
            {
                To = tokenDeposit?.TxRequest?.To,
                Value = tokenDeposit?.TxRequest?.Value ?? BigInteger.Zero,
                Data = tokenDeposit?.TxRequest?.Data,
                From = tokenDeposit?.TxRequest?.From,
                AccessList = tokenDeposit?.TxRequest?.AccessList,
                ChainId = tokenDeposit?.TxRequest?.ChainId,
                Gas = tokenDeposit?.TxRequest?.Gas,
                GasPrice = tokenDeposit?.TxRequest?.GasPrice,
                MaxFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxFeePerGas.ToString()),
                MaxPriorityFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxPriorityFeePerGas.ToString()),
                Nonce = tokenDeposit?.TxRequest?.Nonce,
                Type = tokenDeposit?.TxRequest?.Type,
            };

            if (tx.From == null)
            {
                tx.From = parameters?.L1Signer?.Address;
            }

            var txReceipt = await parameters?.L1Signer?.TransactionManager.SendTransactionAndWaitForReceiptAsync(tx);

            // Apply monkey patching
            return L1TransactionReceipt.MonkeyPatchContractCallWait(txReceipt);
        }

        public async Task<L2ToL1TransactionRequest> GetWithdrawalRequest(Erc20WithdrawParams parameters)
        {
            string toAddress = parameters.DestinationAddress!;

            var provider = new Web3(parameters?.L2Signer?.TransactionManager?.Client);

            // Create the router interface
            var routerInterface = await LoadContractUtils.LoadContract(
                        contractName: "L2GatewayRouter",
                        provider: provider,
                        isClassic: true
                    );

            var functionAbi = routerInterface.ContractBuilder.GetFunctionAbi("outboundTransfer");

            // Define the base directory (assumed to be the root directory of the project)    //////
            //string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Construct the path to the ABI file dynamically
            //string abiFilePath = Path.Combine(baseDirectory, "src", "abi", "classic", "L2GatewayRouter.json");

            //string contractByteCode = "0x608060405234801561001057600080fd5b50611f1d806100206000396000f3fe60806040526004361061011f5760003560e01c806393e59dc1116100a0578063d2ce7d6511610064578063d2ce7d651461066a578063dd61456914610704578063ed08fdc61461073c578063f887ea401461076f578063fb0e722b146107845761011f565b806393e59dc11461048c57806395fcea78146104a1578063a0c76a96146104b6578063a7e28d4814610604578063bda009fe146106375761011f565b80632e567b36116100e75780632e567b361461024857806347466f98146102de5780635625a95214610311578063658b53f4146103495780638da5cb5b146104775761011f565b8063032958021461012457806313af4035146101555780631459457a1461018a5780632d67b72d146101df5780632db09c1c14610233575b600080fd5b34801561013057600080fd5b50610139610799565b604080516001600160a01b039092168252519081900360200190f35b34801561016157600080fd5b506101886004803603602081101561017857600080fd5b50356001600160a01b03166107a8565b005b34801561019657600080fd5b50610188600480360360a08110156101ad57600080fd5b506001600160a01b03813581169160208101358216916040820135811691606081013582169160809091013516610861565b610221600480360360a08110156101f557600080fd5b506001600160a01b038135811691602081013591604082013591606081013591608090910135166108af565b60408051918252519081900360200190f35b34801561023f57600080fd5b50610139610b0c565b610188600480360360a081101561025e57600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b8111156102a057600080fd5b8201836020820111156102b257600080fd5b803590602001918460018302840111600160201b831117156102d357600080fd5b509092509050610b1b565b3480156102ea57600080fd5b506101886004803603602081101561030157600080fd5b50356001600160a01b0316610b5f565b6102216004803603608081101561032757600080fd5b506001600160a01b038135169060208101359060408101359060600135610c02565b610221600480360360a081101561035f57600080fd5b810190602081018135600160201b81111561037957600080fd5b82018360208201111561038b57600080fd5b803590602001918460208302840111600160201b831117156103ac57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295949360208101935035915050600160201b8111156103fb57600080fd5b82018360208201111561040d57600080fd5b803590602001918460208302840111600160201b8311171561042e57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295505082359350505060208101359060400135610d9f565b34801561048357600080fd5b50610139610e06565b34801561049857600080fd5b50610139610e15565b3480156104ad57600080fd5b50610188610e24565b3480156104c257600080fd5b5061058f600480360360a08110156104d957600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b81111561051b57600080fd5b82018360208201111561052d57600080fd5b803590602001918460018302840111600160201b8311171561054e57600080fd5b91908080601f016020809104026020016040519081016040528093929190818152602001838380828437600092019190915250929550610e81945050505050565b6040805160208082528351818301528351919283929083019185019080838360005b838110156105c95781810151838201526020016105b1565b50505050905090810190601f1680156105f65780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561061057600080fd5b506101396004803603602081101561062757600080fd5b50356001600160a01b0316611083565b34801561064357600080fd5b506101396004803603602081101561065a57600080fd5b50356001600160a01b0316611134565b61058f600480360360c081101561068057600080fd5b6001600160a01b0382358116926020810135909116916040820135916060810135916080820135919081019060c0810160a0820135600160201b8111156106c657600080fd5b8201836020820111156106d857600080fd5b803590602001918460018302840111600160201b831117156106f957600080fd5b509092509050611196565b6102216004803603608081101561071a57600080fd5b506001600160a01b0381351690602081013590604081013590606001356113ba565b34801561074857600080fd5b506101396004803603602081101561075f57600080fd5b50356001600160a01b03166113d2565b34801561077b57600080fd5b506101396113ed565b34801561079057600080fd5b506101396113fc565b6004546001600160a01b031681565b6005546001600160a01b031633146107f4576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b6001600160a01b03811661083f576040805162461bcd60e51b815260206004820152600d60248201526c24a72b20a624a22fa7aba722a960991b604482015290519081900360640190fd5b600580546001600160a01b0319166001600160a01b0392909216919091179055565b61086d8260008661140b565b600580546001600160a01b03199081166001600160a01b0397881617909155600080548216948716949094179093556006805490931694169390931790555050565b600061a4b160ff16336001600160a01b0316638e5f5ad16040518163ffffffff1660e01b815260040160206040518083038186803b1580156108f057600080fd5b505afa158015610904573d6000803e3d6000fd5b505050506040513d602081101561091a57600080fd5b505160ff1614610963576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d0549097d1539050931151608a1b604482015290519081900360640190fd5b610975866001600160a01b0316611482565b6109b8576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d513d7d0d3d395149050d5608a1b604482015290519081900360640190fd5b60006109c333611134565b90506001600160a01b038116158015906109eb57506004546001600160a01b03828116911614155b15610a5657866001600160a01b0316816001600160a01b031614610a56576040805162461bcd60e51b815260206004820152601b60248201527f4e4f5f5550444154455f544f5f444946464552454e545f414444520000000000604482015290519081900360640190fd5b604080516001808252818301909252606091602080830190803683370190505090503381600081518110610a8657fe5b6001600160a01b0392909216602092830291909101909101526040805160018082528183019092526060918160200160208202803683370190505090508881600081518110610ad157fe5b60200260200101906001600160a01b031690816001600160a01b031681525050610aff82828a8a8a8a611488565b9998505050505050505050565b6001546001600160a01b031681565b6040805162461bcd60e51b815260206004820152601460248201527327a7262cafa7aaaa2127aaa7222fa927aaaa22a960611b604482015290519081900360640190fd5b6000546001600160a01b03163314610bae576040805162461bcd60e51b815260206004820152600d60248201526c1393d517d19493d357d31254d5609a1b604482015290519081900360640190fd5b600080546001600160a01b0383166001600160a01b0319909116811790915560408051918252517f37389c47920d5cc3229678a0205d0455002c07541a4139ebdce91ac2274657779181900360200190a150565b6005546000906001600160a01b03163314610c51576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b600480546001600160a01b0387166001600160a01b0319909116811790915560408051918252517f3a8f8eb961383a94d41d193e16a3af73eaddfd5764a4c640257323a1603ac3319181900360200190a160006001600160a01b03861615610d1b57856001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b158015610cec57600080fd5b505afa158015610d00573d6000803e3d6000fd5b505050506040513d6020811015610d1657600080fd5b505190505b604080516001600160a01b038084166024808401919091528351808403909101815260449092018352602082810180516001600160e01b031663f7c9362f60e01b17905260065460015485516060810187528981529283018b90529482018990529293610d949383169216903390349060009087611905565b979650505050505050565b6005546000906001600160a01b03163314610dee576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b610dfc868686868633611488565b9695505050505050565b6005546001600160a01b031681565b6000546001600160a01b031681565b6000610e2e611924565b9050336001600160a01b03821614610e7e576040805162461bcd60e51b815260206004820152600e60248201526d2727aa2fa32927a6afa0a226a4a760911b604482015290519081900360640190fd5b50565b60606000610e8e87611134565b9050806001600160a01b031663a0c76a9688888888886040518663ffffffff1660e01b815260040180866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b03168152602001846001600160a01b03166001600160a01b0316815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015610f3e578181015183820152602001610f26565b50505050905090810190601f168015610f6b5780820380516001836020036101000a031916815260200191505b50965050505050505060006040518083038186803b158015610f8c57600080fd5b505afa158015610fa0573d6000803e3d6000fd5b505050506040513d6000823e601f3d908101601f191682016040526020811015610fc957600080fd5b8101908080516040519392919084600160201b821115610fe857600080fd5b908301906020820185811115610ffd57600080fd5b8251600160201b81118282018810171561101657600080fd5b82525081516020918201929091019080838360005b8381101561104357818101518382015260200161102b565b50505050905090810190601f1680156110705780820380516001836020036101000a031916815260200191505b5060405250505091505095945050505050565b60008061108f83611134565b90506001600160a01b0381166110a957600091505061112f565b806001600160a01b031663a7e28d48846040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b1580156110ff57600080fd5b505afa158015611113573d6000803e3d6000fd5b505050506040513d602081101561112957600080fd5b50519150505b919050565b6001600160a01b03808216600090815260036020526040902054168061116257506004546001600160a01b03165b6001600160a01b038116600114806111895750611187816001600160a01b0316611482565b155b1561112f5750600061112f565b6000546060906001600160a01b031615611264576000546040805163babcc53960e01b815233600482015290516001600160a01b039092169163babcc53991602480820192602092909190829003018186803b1580156111f557600080fd5b505afa158015611209573d6000803e3d6000fd5b505050506040513d602081101561121f57600080fd5b5051611264576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d5d2125511531254d51151608a1b604482015290519081900360640190fd5b60008383604081101561127657600080fd5b81359190810190604081016020820135600160201b81111561129757600080fd5b8201836020820111156112a957600080fd5b803590602001918460018302840111600160201b831117156112ca57600080fd5b91908080601f0160208091040260200160405190810160405280939291908181526020018383808284376000920191909152509697505050508989028501935050508215159050611357576040805162461bcd60e51b81526020600482015260126024820152711393d7d4d550935254d4d253d397d0d3d4d560721b604482015290519081900360640190fd5b80341461139d576040805162461bcd60e51b815260206004820152600f60248201526e57524f4e475f4554485f56414c554560881b604482015290519081900360640190fd5b6113ac8a8a8a8a8a8a8a611949565b9a9950505050505050505050565b60006113c985858585336108af565b95945050505050565b6003602052600090815260409020546001600160a01b031681565b6002546001600160a01b031681565b6006546001600160a01b031681565b6001600160a01b03821615611454576040805162461bcd60e51b815260206004820152600a6024820152692120a22fa927aaaa22a960b11b604482015290519081900360640190fd5b61145e8383611b9e565b600480546001600160a01b0319166001600160a01b03929092169190911790555050565b3b151590565b600085518751146114cf576040805162461bcd60e51b815260206004820152600c60248201526b0aea49e9c8ebe988a9c8ea8960a31b604482015290519081900360640190fd5b60005b87518110156117d0578681815181106114e757fe5b6020026020010151600360008a84815181106114ff57fe5b60200260200101516001600160a01b03166001600160a01b0316815260200190815260200160002060006101000a8154816001600160a01b0302191690836001600160a01b0316021790555086818151811061155757fe5b60200260200101516001600160a01b031688828151811061157457fe5b60200260200101516001600160a01b03167f812ca95fe4492a9e2d1f2723c2c40c03a60a27b059581ae20ac4e4d73bfba35460405160405180910390a360006001600160a01b03168782815181106115c857fe5b60200260200101516001600160a01b03161415801561160d575060016001600160a01b03168782815181106115f957fe5b60200260200101516001600160a01b031614155b156117c85760006001600160a01b031687828151811061162957fe5b60200260200101516001600160a01b031663a7e28d488a848151811061164b57fe5b60200260200101516040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b15801561169957600080fd5b505afa1580156116ad573d6000803e3d6000fd5b505050506040513d60208110156116c357600080fd5b50516001600160a01b03161415611721576040805162461bcd60e51b815260206004820152601c60248201527f544f4b454e5f4e4f545f48414e444c45445f42595f4741544557415900000000604482015290519081900360640190fd5b86818151811061172d57fe5b60200260200101516001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b15801561176d57600080fd5b505afa158015611781573d6000803e3d6000fd5b505050506040513d602081101561179757600080fd5b505187518890839081106117a757fe5b60200260200101906001600160a01b031690816001600160a01b0316815250505b6001016114d2565b506060634201f98560e01b8888604051602401808060200180602001838103835285818151815260200191508051906020019060200280838360005b8381101561182457818101518382015260200161180c565b50505050905001838103825284818151815260200191508051906020019060200280838360005b8381101561186357818101518382015260200161184b565b50505050905001945050505050604051602081830303815290604052906001600160e01b0319166020820180516001600160e01b03838183161783525050505090506118f9600660009054906101000a90046001600160a01b0316600160009054906101000a90046001600160a01b03168534600060405180606001604052808b81526020018d81526020018c81525087611905565b98975050505050505050565b60006118f98888888888886000015189602001518a604001518a611c6a565b7fb53127684a568b3173ae13b9f8a6016e243e63b6e8ee1178d6a717850b5d61035490565b6060600061195689611134565b90506060611965338686611e7d565b604080516001600160a01b0385811682529151929350818c169233928e16917f85291dff2161a93c2f12c819d31889c96c63042116f5bc5a205aa701c2c429f5919081900360200190a4816001600160a01b031663d2ce7d65348c8c8c8c8c886040518863ffffffff1660e01b815260040180876001600160a01b03166001600160a01b03168152602001866001600160a01b03166001600160a01b0316815260200185815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611a53578181015183820152602001611a3b565b50505050905090810190601f168015611a805780820380516001836020036101000a031916815260200191505b509750505050505050506000604051808303818588803b158015611aa357600080fd5b505af1158015611ab7573d6000803e3d6000fd5b50505050506040513d6000823e601f3d908101601f191682016040526020811015611ae157600080fd5b8101908080516040519392919084600160201b821115611b0057600080fd5b908301906020820185811115611b1557600080fd5b8251600160201b811182820188101715611b2e57600080fd5b82525081516020918201929091019080838360005b83811015611b5b578181015183820152602001611b43565b50505050905090810190601f168015611b885780820380516001836020036101000a031916815260200191505b5060405250505092505050979650505050505050565b6001600160a01b038216611bef576040805162461bcd60e51b81526020600482015260136024820152721253959053125117d0d3d55395115494105495606a1b604482015290519081900360640190fd5b6001546001600160a01b031615611c3c576040805162461bcd60e51b815260206004820152600c60248201526b1053149150511657d253925560a21b604482015290519081900360640190fd5b600180546001600160a01b039384166001600160a01b03199182161790915560028054929093169116179055565b6000808a6001600160a01b031663679b6ded898c8a8a8e8f8c8c8c6040518a63ffffffff1660e01b815260040180896001600160a01b03166001600160a01b03168152602001888152602001878152602001866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b0316815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611d31578181015183820152602001611d19565b50505050905090810190601f168015611d5e5780820380516001836020036101000a031916815260200191505b5099505050505050505050506020604051808303818588803b158015611d8357600080fd5b505af1158015611d97573d6000803e3d6000fd5b50505050506040513d6020811015611dae57600080fd5b81019080805190602001909291905050509050808a6001600160a01b03168a6001600160a01b03167fc1d1490cf25c3b40d600dfb27c7680340ed1ab901b7e8f3551280968a3b372b0866040518080602001828103825283818151815260200191508051906020019080838360005b83811015611e35578181015183820152602001611e1d565b50505050905090810190601f168015611e625780820380516001836020036101000a031916815260200191505b509250505060405180910390a49a9950505050505050505050565b606083838360405160200180846001600160a01b03166001600160a01b0316815260200180602001828103825284848281815260200192508082843760008184015260408051601f19601f909301831690940184810390920184525250999850505050505050505056fea2646970667358221220209a87e0b2b16371b8726764d296199cdef67a4e7dbc1a7892b6be8216dc205164736f6c634300060b0033"
            //var contractAbi = routerInterface.ContractBuilder.ContractABI;
            //string functionData = new ConstructorCallEncoder().EncodeRequest(
            //    contractByteCode: contractByteCode,
            //    parameters: contractAbi.Constructor.InputParameters,
            //    values: new object[] { gatewayAddress, parameters.Amount ?? MAX_APPROVAL }
            //    );

            string functionData = new ConstructorCallEncoder().EncodeRequest(
                contractByteCode: await provider.Eth.GetCode.SendRequestAsync(routerInterface.Address),
                //contractByteCode: HelperMethods.GetBytecodeFromABI(abiFilePath) ?? null,
                parameters: functionAbi?.InputParameters,
                values: new object[]
                {
                    parameters?.Erc20L1Address!,
                    toAddress,
                    parameters?.Amount!,
                    "0x"
                });

            // Create the transaction request
            L2ToL1TransactionRequest request = new L2ToL1TransactionRequest
            {
                TxRequest = new TransactionRequest
                {
                    Data = functionData,
                    To = L2Network?.TokenBridge?.L2GatewayRouter,
                    Value = BigInteger.Zero,
                    From = parameters?.From
                },
                EstimateL1GasLimit = async (l1Provider) =>
                {
                    if (await Lib.IsArbitrumChain(new Web3(l1Provider)))
                    {
                        // Estimate L1 gas limit
                        return BigInteger.Parse("8000000");
                    }

                    // Get L1 Gateway Address
                    string l1GatewayAddress = await GetL1GatewayAddress(parameters?.Erc20L1Address, new Web3(l1Provider));

                    // Check if this is a WETH deposit
                    bool isWeth = await IsWethGateway(l1GatewayAddress, new Web3(l1Provider));

                    // Return estimated gas limit with padding
                    return isWeth ? BigInteger.Parse("190000") : BigInteger.Parse("160000");
                }
            };

            return request;
        }

        // Function to withdraw tokens from L2 to L1
        public override async Task<L2TransactionReceipt> Withdraw(dynamic parameters)
        {
            if (!SignerProviderUtils.SignerHasProvider(parameters?.L2Signer))
            {
                throw new MissingProviderArbSdkError("l2Signer");
            }

            await CheckL2Network(new SignerOrProvider(parameters?.L2Signer));

            dynamic withdrawalRequest;

            if(DataEntities.TransactionUtils.IsL2ToL1TransactionRequest(parameters))
            {
                withdrawalRequest = parameters;
            }

            else if(parameters is Erc20WithdrawParams)
            {
                withdrawalRequest = await GetWithdrawalRequest(parameters);
            }
            else if(parameters is L2ToL1TxReqAndSigner)
            {
                withdrawalRequest = await GetWithdrawalRequest(new Erc20WithdrawParams
                {
                    From = parameters?.L2Signer?.Address,
                    //DestinationAddress = parameters.DestinationAddress,
                    //Erc20L1Address = parameters?.Erc20L1Address,
                    //Amount = parameters?.Amount,
                    L2Signer = parameters?.L2Signer,
                    Overrides = parameters?.Overrides,
                });
            }

            else
            {
                throw new ArgumentException("Invalid parameter type. Expected Erc20DepositParams or L1ToL2TxReqAndSignerProvider.");
            }

            var tx = new TransactionRequest
            {
                To = withdrawalRequest?.TxRequest?.To,
                Value = withdrawalRequest?.TxRequest?.Value ?? BigInteger.Zero,
                Data = withdrawalRequest?.TxRequest?.Data,
                From = withdrawalRequest?.TxRequest?.From,
                AccessList = withdrawalRequest?.TxRequest?.AccessList,
                ChainId = withdrawalRequest?.TxRequest?.ChainId,
                Gas = withdrawalRequest?.TxRequest?.Gas,
                GasPrice = withdrawalRequest?.TxRequest?.GasPrice,
                MaxFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxFeePerGas.ToString()),
                MaxPriorityFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxPriorityFeePerGas.ToString()),
                Nonce = withdrawalRequest?.TxRequest?.Nonce,
                Type = withdrawalRequest?.TxRequest?.Type,
            };

            if (tx.From == null)
            {
                tx.From = parameters?.L2Signer?.Address;
            }

            var txReceipt = await parameters?.L2Signer?.TransactionManager?.SendTransactionAndWaitForReceiptAsync(tx);
            return L2TransactionReceipt.MonkeyPatchWait(txReceipt);
        }
        public class GasParams
        {
            public BigInteger? MaxSubmissionCost { get; set; }
            public BigInteger? GasLimit { get; set; }
        }

        public class AdminErc20Bridger : Erc20Bridger
        {
            public AdminErc20Bridger(L2Network l2Network) : base(l2Network)
            {
            }
            public async Task<L1TransactionReceipt> RegisterCustomToken(
                string l1TokenAddress,
                string l2TokenAddress,
                Account l1Signer,
                Web3 l2Provider)
            { 
                if (!SignerProviderUtils.SignerHasProvider(l1Signer))
                {
                    throw new MissingProviderArbSdkError("l1Signer");
                }

                await CheckL1Network(new SignerOrProvider(new Web3(l1Signer?.TransactionManager?.Client)));
                await CheckL2Network(new SignerOrProvider(l2Provider));

                string l1SenderAddress = l1Signer?.Address;

                var l1Token = await LoadContractUtils.LoadContract(
                        contractName: "ICustomToken",
                        address: l1TokenAddress,
                        provider: new Web3(l1Signer?.TransactionManager?.Client),
                        isClassic: true
                    );
                var l1TokenContractByteCode = await new Web3(l1Signer?.TransactionManager?.Client).Eth.GetCode.SendRequestAsync(l1Token.Address);

                var l2Token = await LoadContractUtils.LoadContract(
                        contractName: "IArbToken",
                        address: l2TokenAddress,
                        provider: l2Provider,
                        isClassic: true
                    );

                // Sanity checks
                if (!await LoadContractUtils.IsContractDeployed(new Web3(l1Signer?.TransactionManager.Client), l1Token.Address))
                {
                    throw new Exception("L1 token is not deployed.");
                }
                if (!await LoadContractUtils.IsContractDeployed(l2Provider, l2Token.Address))
                {
                    throw new Exception("L2 token is not deployed.");
                }

                string l1AddressFromL2 = await l2Token.GetFunction("l1Address").CallAsync<string>();

                if (l1AddressFromL2 != l1TokenAddress)
                {
                    throw new ArbSdkError(
                        $"L2 token does not have l1 address set. Set address: {l1AddressFromL2}, expected address: {l1TokenAddress}."
                    );
                }


                // Define encodeFuncData function for setting gas parameters
                Func<GasParams, GasParams, BigInteger?, TransactionRequest> encodeFuncData = (setTokenGas, setGatewayGas, maxFeePerGas) =>
                {
                    BigInteger? doubleFeePerGas = maxFeePerGas == RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas
                        ? RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas * 2
                        : maxFeePerGas;

                    BigInteger? setTokenDeposit = setTokenGas.GasLimit * doubleFeePerGas + setTokenGas.MaxSubmissionCost;
                    BigInteger? setGatewayDeposit = setGatewayGas.GasLimit * doubleFeePerGas + setGatewayGas.MaxSubmissionCost;

                    var functionAbi = l1Token.ContractBuilder.GetFunctionAbi("registerTokenOnL2");

                    // Define the base directory (assumed to be the root directory of the project)    //////
                    //string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    // Construct the path to the ABI file dynamically
                    //string abiFilePath = Path.Combine(baseDirectory, "src", "abi", "classic", "ICustomToken.json");

                    //string contractByteCode = "0x608060405234801561001057600080fd5b50611f1d806100206000396000f3fe60806040526004361061011f5760003560e01c806393e59dc1116100a0578063d2ce7d6511610064578063d2ce7d651461066a578063dd61456914610704578063ed08fdc61461073c578063f887ea401461076f578063fb0e722b146107845761011f565b806393e59dc11461048c57806395fcea78146104a1578063a0c76a96146104b6578063a7e28d4814610604578063bda009fe146106375761011f565b80632e567b36116100e75780632e567b361461024857806347466f98146102de5780635625a95214610311578063658b53f4146103495780638da5cb5b146104775761011f565b8063032958021461012457806313af4035146101555780631459457a1461018a5780632d67b72d146101df5780632db09c1c14610233575b600080fd5b34801561013057600080fd5b50610139610799565b604080516001600160a01b039092168252519081900360200190f35b34801561016157600080fd5b506101886004803603602081101561017857600080fd5b50356001600160a01b03166107a8565b005b34801561019657600080fd5b50610188600480360360a08110156101ad57600080fd5b506001600160a01b03813581169160208101358216916040820135811691606081013582169160809091013516610861565b610221600480360360a08110156101f557600080fd5b506001600160a01b038135811691602081013591604082013591606081013591608090910135166108af565b60408051918252519081900360200190f35b34801561023f57600080fd5b50610139610b0c565b610188600480360360a081101561025e57600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b8111156102a057600080fd5b8201836020820111156102b257600080fd5b803590602001918460018302840111600160201b831117156102d357600080fd5b509092509050610b1b565b3480156102ea57600080fd5b506101886004803603602081101561030157600080fd5b50356001600160a01b0316610b5f565b6102216004803603608081101561032757600080fd5b506001600160a01b038135169060208101359060408101359060600135610c02565b610221600480360360a081101561035f57600080fd5b810190602081018135600160201b81111561037957600080fd5b82018360208201111561038b57600080fd5b803590602001918460208302840111600160201b831117156103ac57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295949360208101935035915050600160201b8111156103fb57600080fd5b82018360208201111561040d57600080fd5b803590602001918460208302840111600160201b8311171561042e57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295505082359350505060208101359060400135610d9f565b34801561048357600080fd5b50610139610e06565b34801561049857600080fd5b50610139610e15565b3480156104ad57600080fd5b50610188610e24565b3480156104c257600080fd5b5061058f600480360360a08110156104d957600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b81111561051b57600080fd5b82018360208201111561052d57600080fd5b803590602001918460018302840111600160201b8311171561054e57600080fd5b91908080601f016020809104026020016040519081016040528093929190818152602001838380828437600092019190915250929550610e81945050505050565b6040805160208082528351818301528351919283929083019185019080838360005b838110156105c95781810151838201526020016105b1565b50505050905090810190601f1680156105f65780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561061057600080fd5b506101396004803603602081101561062757600080fd5b50356001600160a01b0316611083565b34801561064357600080fd5b506101396004803603602081101561065a57600080fd5b50356001600160a01b0316611134565b61058f600480360360c081101561068057600080fd5b6001600160a01b0382358116926020810135909116916040820135916060810135916080820135919081019060c0810160a0820135600160201b8111156106c657600080fd5b8201836020820111156106d857600080fd5b803590602001918460018302840111600160201b831117156106f957600080fd5b509092509050611196565b6102216004803603608081101561071a57600080fd5b506001600160a01b0381351690602081013590604081013590606001356113ba565b34801561074857600080fd5b506101396004803603602081101561075f57600080fd5b50356001600160a01b03166113d2565b34801561077b57600080fd5b506101396113ed565b34801561079057600080fd5b506101396113fc565b6004546001600160a01b031681565b6005546001600160a01b031633146107f4576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b6001600160a01b03811661083f576040805162461bcd60e51b815260206004820152600d60248201526c24a72b20a624a22fa7aba722a960991b604482015290519081900360640190fd5b600580546001600160a01b0319166001600160a01b0392909216919091179055565b61086d8260008661140b565b600580546001600160a01b03199081166001600160a01b0397881617909155600080548216948716949094179093556006805490931694169390931790555050565b600061a4b160ff16336001600160a01b0316638e5f5ad16040518163ffffffff1660e01b815260040160206040518083038186803b1580156108f057600080fd5b505afa158015610904573d6000803e3d6000fd5b505050506040513d602081101561091a57600080fd5b505160ff1614610963576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d0549097d1539050931151608a1b604482015290519081900360640190fd5b610975866001600160a01b0316611482565b6109b8576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d513d7d0d3d395149050d5608a1b604482015290519081900360640190fd5b60006109c333611134565b90506001600160a01b038116158015906109eb57506004546001600160a01b03828116911614155b15610a5657866001600160a01b0316816001600160a01b031614610a56576040805162461bcd60e51b815260206004820152601b60248201527f4e4f5f5550444154455f544f5f444946464552454e545f414444520000000000604482015290519081900360640190fd5b604080516001808252818301909252606091602080830190803683370190505090503381600081518110610a8657fe5b6001600160a01b0392909216602092830291909101909101526040805160018082528183019092526060918160200160208202803683370190505090508881600081518110610ad157fe5b60200260200101906001600160a01b031690816001600160a01b031681525050610aff82828a8a8a8a611488565b9998505050505050505050565b6001546001600160a01b031681565b6040805162461bcd60e51b815260206004820152601460248201527327a7262cafa7aaaa2127aaa7222fa927aaaa22a960611b604482015290519081900360640190fd5b6000546001600160a01b03163314610bae576040805162461bcd60e51b815260206004820152600d60248201526c1393d517d19493d357d31254d5609a1b604482015290519081900360640190fd5b600080546001600160a01b0383166001600160a01b0319909116811790915560408051918252517f37389c47920d5cc3229678a0205d0455002c07541a4139ebdce91ac2274657779181900360200190a150565b6005546000906001600160a01b03163314610c51576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b600480546001600160a01b0387166001600160a01b0319909116811790915560408051918252517f3a8f8eb961383a94d41d193e16a3af73eaddfd5764a4c640257323a1603ac3319181900360200190a160006001600160a01b03861615610d1b57856001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b158015610cec57600080fd5b505afa158015610d00573d6000803e3d6000fd5b505050506040513d6020811015610d1657600080fd5b505190505b604080516001600160a01b038084166024808401919091528351808403909101815260449092018352602082810180516001600160e01b031663f7c9362f60e01b17905260065460015485516060810187528981529283018b90529482018990529293610d949383169216903390349060009087611905565b979650505050505050565b6005546000906001600160a01b03163314610dee576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b610dfc868686868633611488565b9695505050505050565b6005546001600160a01b031681565b6000546001600160a01b031681565b6000610e2e611924565b9050336001600160a01b03821614610e7e576040805162461bcd60e51b815260206004820152600e60248201526d2727aa2fa32927a6afa0a226a4a760911b604482015290519081900360640190fd5b50565b60606000610e8e87611134565b9050806001600160a01b031663a0c76a9688888888886040518663ffffffff1660e01b815260040180866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b03168152602001846001600160a01b03166001600160a01b0316815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015610f3e578181015183820152602001610f26565b50505050905090810190601f168015610f6b5780820380516001836020036101000a031916815260200191505b50965050505050505060006040518083038186803b158015610f8c57600080fd5b505afa158015610fa0573d6000803e3d6000fd5b505050506040513d6000823e601f3d908101601f191682016040526020811015610fc957600080fd5b8101908080516040519392919084600160201b821115610fe857600080fd5b908301906020820185811115610ffd57600080fd5b8251600160201b81118282018810171561101657600080fd5b82525081516020918201929091019080838360005b8381101561104357818101518382015260200161102b565b50505050905090810190601f1680156110705780820380516001836020036101000a031916815260200191505b5060405250505091505095945050505050565b60008061108f83611134565b90506001600160a01b0381166110a957600091505061112f565b806001600160a01b031663a7e28d48846040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b1580156110ff57600080fd5b505afa158015611113573d6000803e3d6000fd5b505050506040513d602081101561112957600080fd5b50519150505b919050565b6001600160a01b03808216600090815260036020526040902054168061116257506004546001600160a01b03165b6001600160a01b038116600114806111895750611187816001600160a01b0316611482565b155b1561112f5750600061112f565b6000546060906001600160a01b031615611264576000546040805163babcc53960e01b815233600482015290516001600160a01b039092169163babcc53991602480820192602092909190829003018186803b1580156111f557600080fd5b505afa158015611209573d6000803e3d6000fd5b505050506040513d602081101561121f57600080fd5b5051611264576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d5d2125511531254d51151608a1b604482015290519081900360640190fd5b60008383604081101561127657600080fd5b81359190810190604081016020820135600160201b81111561129757600080fd5b8201836020820111156112a957600080fd5b803590602001918460018302840111600160201b831117156112ca57600080fd5b91908080601f0160208091040260200160405190810160405280939291908181526020018383808284376000920191909152509697505050508989028501935050508215159050611357576040805162461bcd60e51b81526020600482015260126024820152711393d7d4d550935254d4d253d397d0d3d4d560721b604482015290519081900360640190fd5b80341461139d576040805162461bcd60e51b815260206004820152600f60248201526e57524f4e475f4554485f56414c554560881b604482015290519081900360640190fd5b6113ac8a8a8a8a8a8a8a611949565b9a9950505050505050505050565b60006113c985858585336108af565b95945050505050565b6003602052600090815260409020546001600160a01b031681565b6002546001600160a01b031681565b6006546001600160a01b031681565b6001600160a01b03821615611454576040805162461bcd60e51b815260206004820152600a6024820152692120a22fa927aaaa22a960b11b604482015290519081900360640190fd5b61145e8383611b9e565b600480546001600160a01b0319166001600160a01b03929092169190911790555050565b3b151590565b600085518751146114cf576040805162461bcd60e51b815260206004820152600c60248201526b0aea49e9c8ebe988a9c8ea8960a31b604482015290519081900360640190fd5b60005b87518110156117d0578681815181106114e757fe5b6020026020010151600360008a84815181106114ff57fe5b60200260200101516001600160a01b03166001600160a01b0316815260200190815260200160002060006101000a8154816001600160a01b0302191690836001600160a01b0316021790555086818151811061155757fe5b60200260200101516001600160a01b031688828151811061157457fe5b60200260200101516001600160a01b03167f812ca95fe4492a9e2d1f2723c2c40c03a60a27b059581ae20ac4e4d73bfba35460405160405180910390a360006001600160a01b03168782815181106115c857fe5b60200260200101516001600160a01b03161415801561160d575060016001600160a01b03168782815181106115f957fe5b60200260200101516001600160a01b031614155b156117c85760006001600160a01b031687828151811061162957fe5b60200260200101516001600160a01b031663a7e28d488a848151811061164b57fe5b60200260200101516040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b15801561169957600080fd5b505afa1580156116ad573d6000803e3d6000fd5b505050506040513d60208110156116c357600080fd5b50516001600160a01b03161415611721576040805162461bcd60e51b815260206004820152601c60248201527f544f4b454e5f4e4f545f48414e444c45445f42595f4741544557415900000000604482015290519081900360640190fd5b86818151811061172d57fe5b60200260200101516001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b15801561176d57600080fd5b505afa158015611781573d6000803e3d6000fd5b505050506040513d602081101561179757600080fd5b505187518890839081106117a757fe5b60200260200101906001600160a01b031690816001600160a01b0316815250505b6001016114d2565b506060634201f98560e01b8888604051602401808060200180602001838103835285818151815260200191508051906020019060200280838360005b8381101561182457818101518382015260200161180c565b50505050905001838103825284818151815260200191508051906020019060200280838360005b8381101561186357818101518382015260200161184b565b50505050905001945050505050604051602081830303815290604052906001600160e01b0319166020820180516001600160e01b03838183161783525050505090506118f9600660009054906101000a90046001600160a01b0316600160009054906101000a90046001600160a01b03168534600060405180606001604052808b81526020018d81526020018c81525087611905565b98975050505050505050565b60006118f98888888888886000015189602001518a604001518a611c6a565b7fb53127684a568b3173ae13b9f8a6016e243e63b6e8ee1178d6a717850b5d61035490565b6060600061195689611134565b90506060611965338686611e7d565b604080516001600160a01b0385811682529151929350818c169233928e16917f85291dff2161a93c2f12c819d31889c96c63042116f5bc5a205aa701c2c429f5919081900360200190a4816001600160a01b031663d2ce7d65348c8c8c8c8c886040518863ffffffff1660e01b815260040180876001600160a01b03166001600160a01b03168152602001866001600160a01b03166001600160a01b0316815260200185815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611a53578181015183820152602001611a3b565b50505050905090810190601f168015611a805780820380516001836020036101000a031916815260200191505b509750505050505050506000604051808303818588803b158015611aa357600080fd5b505af1158015611ab7573d6000803e3d6000fd5b50505050506040513d6000823e601f3d908101601f191682016040526020811015611ae157600080fd5b8101908080516040519392919084600160201b821115611b0057600080fd5b908301906020820185811115611b1557600080fd5b8251600160201b811182820188101715611b2e57600080fd5b82525081516020918201929091019080838360005b83811015611b5b578181015183820152602001611b43565b50505050905090810190601f168015611b885780820380516001836020036101000a031916815260200191505b5060405250505092505050979650505050505050565b6001600160a01b038216611bef576040805162461bcd60e51b81526020600482015260136024820152721253959053125117d0d3d55395115494105495606a1b604482015290519081900360640190fd5b6001546001600160a01b031615611c3c576040805162461bcd60e51b815260206004820152600c60248201526b1053149150511657d253925560a21b604482015290519081900360640190fd5b600180546001600160a01b039384166001600160a01b03199182161790915560028054929093169116179055565b6000808a6001600160a01b031663679b6ded898c8a8a8e8f8c8c8c6040518a63ffffffff1660e01b815260040180896001600160a01b03166001600160a01b03168152602001888152602001878152602001866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b0316815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611d31578181015183820152602001611d19565b50505050905090810190601f168015611d5e5780820380516001836020036101000a031916815260200191505b5099505050505050505050506020604051808303818588803b158015611d8357600080fd5b505af1158015611d97573d6000803e3d6000fd5b50505050506040513d6020811015611dae57600080fd5b81019080805190602001909291905050509050808a6001600160a01b03168a6001600160a01b03167fc1d1490cf25c3b40d600dfb27c7680340ed1ab901b7e8f3551280968a3b372b0866040518080602001828103825283818151815260200191508051906020019080838360005b83811015611e35578181015183820152602001611e1d565b50505050905090810190601f168015611e625780820380516001836020036101000a031916815260200191505b509250505060405180910390a49a9950505050505050505050565b606083838360405160200180846001600160a01b03166001600160a01b0316815260200180602001828103825284848281815260200192508082843760008184015260408051601f19601f909301831690940184810390920184525250999850505050505050505056fea2646970667358221220209a87e0b2b16371b8726764d296199cdef67a4e7dbc1a7892b6be8216dc205164736f6c634300060b0033"
                    //var contractAbi = l1Token.ContractBuilder.ContractABI;
                    //string functionData = new ConstructorCallEncoder().EncodeRequest(
                    //    contractByteCode: contractByteCode,
                    //    parameters: contractAbi.Constructor.InputParameters,
                    //    values: new object[] { gatewayAddress, parameters.Amount ?? MAX_APPROVAL }
                    //    );

                    string functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: l1TokenContractByteCode,
                        //contractByteCode: HelperMethods.GetBytecodeFromABI(abiFilePath) ?? null,
                        parameters: functionAbi?.InputParameters,
                        values: new object[]
                        {
                            l2TokenAddress,
                            setTokenGas?.MaxSubmissionCost!,
                            setGatewayGas?.MaxSubmissionCost!,
                            setTokenGas?.GasLimit!,
                            setGatewayGas?.GasLimit!,
                            doubleFeePerGas,
                            setTokenDeposit,
                            setGatewayDeposit,
                            l1SenderAddress
                        });

                    return new TransactionRequest
                    {
                        Data = functionData,
                        To = l1Token.Address,
                        Value = setTokenDeposit + setGatewayDeposit,
                        From = l1SenderAddress
                    };
                };

                var l1Provider = new Web3(l1Signer?.TransactionManager?.Client);
                var gEstimator = new L1ToL2MessageGasEstimator(l2Provider);


                // Estimate gas parameters
                var setTokenEstimates = await gEstimator.PopulateFunctionParams(
                    (parameters) => encodeFuncData(
                        new GasParams
                        {
                            GasLimit = parameters?.GasLimit,
                            MaxSubmissionCost = parameters?.MaxSubmissionCost
                        },
                        new GasParams
                        {
                            GasLimit = RetryableDataTools.ErrorTriggeringParams.GasLimit,
                            MaxSubmissionCost = BigInteger.One
                        },
                        parameters?.MaxFeePerGas
                    ),
                    l1Provider
                );

                // Perform the transaction
                var registerTx = await l1Signer.TransactionManager.SendTransactionAndWaitForReceiptAsync(new TransactionInput
                {
                    To = l1Token?.Address,
                    Data = setTokenEstimates?.Data,
                    Value = new HexBigInteger(setTokenEstimates?.Value.ToString()),
                    From = l1SenderAddress
                });

                // Return the transaction receipt
                return L1TransactionReceipt.MonkeyPatchWait(registerTx);
            }

            public async Task<List<GatewaySetEvent>> GetL1GatewaySetEvents(Web3 l1Provider, NewFilterInput filter)
            {
                await CheckL1Network(new SignerOrProvider(l1Provider));

                string l1GatewayRouterAddress = L2Network?.TokenBridge?.L1GatewayRouter;
                var eventFetcher = new EventFetcher(l1Provider);

                var argumentFilters = new Dictionary<string, object>();

                var eventList = await eventFetcher.GetEventsAsync<GatewaySetEvent>(
                    contractFactory: "L1GatewayRouter",
                    eventName: "GatewaySet",
                    argumentFilters: argumentFilters,
                    filter: new NewFilterInput
                    {
                        FromBlock = filter?.FromBlock,
                        ToBlock = filter?.ToBlock,
                        Address = new string[] { l1GatewayRouterAddress }
                    },
                    isClassic: true
                );

                var formattedEvents = eventList.Select(a => a.Event).ToList();

                return formattedEvents;
            }

            public async Task<List<GatewaySetEvent>> GetL2GatewaySetEvents(
                Web3 l2Provider,
                NewFilterInput filter,
                string? customNetworkL2GatewayRouter = null)
            {
                if (L2Network.IsCustom && customNetworkL2GatewayRouter == null)
                {
                    throw new ArbSdkError("Must supply customNetworkL2GatewayRouter for custom network");
                }

                await CheckL2Network(new SignerOrProvider(l2Provider));

                string l2GatewayRouterAddress = customNetworkL2GatewayRouter ?? L2Network?.TokenBridge?.L2GatewayRouter;

                var eventFetcher = new EventFetcher(l2Provider);
                var argumentFilters = new Dictionary<string, object>();

                var eventList = await eventFetcher.GetEventsAsync<GatewaySetEvent>(
                    contractFactory: "L2GatewayRouter",
                    eventName: "GatewaySet",
                    argumentFilters: argumentFilters,
                    filter: new NewFilterInput
                    {
                        FromBlock = filter?.FromBlock,
                        ToBlock = filter?.FromBlock,
                        Address = new string[] { l2GatewayRouterAddress },
                        Topics = filter?.Topics
                    },
                    isClassic: true
                );

                var formattedEvents = eventList.Select(a => a.Event).ToList();

                return formattedEvents;
            }

            public async Task<L1TransactionReceipt> SetGateways(
                Account l1Signer,
                Web3 l2Provider,
                List<TokenAndGateway> tokenGateways,
                GasOverrides? options = null)
            {
                if (!SignerProviderUtils.SignerHasProvider(l1Signer))
                {
                    throw new MissingProviderArbSdkError("l1Signer");
                }

                await CheckL1Network(new SignerOrProvider(l1Signer));
                await CheckL2Network(new SignerOrProvider(l2Provider));

                string from = l1Signer?.Address;

                var l1GatewayRouter = await LoadContractUtils.LoadContract(
                            provider: new Web3(l1Signer?.TransactionManager?.Client),
                            contractName: "L1GatewayRouter",
                            address: L2Network?.TokenBridge?.L1GatewayRouter,
                            isClassic: true
                            );
                var contractByteCode = await new Web3(l1Signer?.TransactionManager?.Client).Eth.GetCode.SendRequestAsync(l1GatewayRouter.Address);

                // Define function for setting gateways
                Func<L1ToL2MessageGasParams, TransactionRequest> setGatewaysFunc = (parameters) =>
                {
                    var functionAbi = l1GatewayRouter.ContractBuilder.GetFunctionAbi("setGateways");

                    // Define the base directory (assumed to be the root directory of the project)    //////
                    //string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    // Construct the path to the ABI file dynamically
                    //string abiFilePath = Path.Combine(baseDirectory, "src", "abi", "classic", "L1GatewayRouter.json");

                    //string contractByteCode = "0x608060405234801561001057600080fd5b50611f1d806100206000396000f3fe60806040526004361061011f5760003560e01c806393e59dc1116100a0578063d2ce7d6511610064578063d2ce7d651461066a578063dd61456914610704578063ed08fdc61461073c578063f887ea401461076f578063fb0e722b146107845761011f565b806393e59dc11461048c57806395fcea78146104a1578063a0c76a96146104b6578063a7e28d4814610604578063bda009fe146106375761011f565b80632e567b36116100e75780632e567b361461024857806347466f98146102de5780635625a95214610311578063658b53f4146103495780638da5cb5b146104775761011f565b8063032958021461012457806313af4035146101555780631459457a1461018a5780632d67b72d146101df5780632db09c1c14610233575b600080fd5b34801561013057600080fd5b50610139610799565b604080516001600160a01b039092168252519081900360200190f35b34801561016157600080fd5b506101886004803603602081101561017857600080fd5b50356001600160a01b03166107a8565b005b34801561019657600080fd5b50610188600480360360a08110156101ad57600080fd5b506001600160a01b03813581169160208101358216916040820135811691606081013582169160809091013516610861565b610221600480360360a08110156101f557600080fd5b506001600160a01b038135811691602081013591604082013591606081013591608090910135166108af565b60408051918252519081900360200190f35b34801561023f57600080fd5b50610139610b0c565b610188600480360360a081101561025e57600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b8111156102a057600080fd5b8201836020820111156102b257600080fd5b803590602001918460018302840111600160201b831117156102d357600080fd5b509092509050610b1b565b3480156102ea57600080fd5b506101886004803603602081101561030157600080fd5b50356001600160a01b0316610b5f565b6102216004803603608081101561032757600080fd5b506001600160a01b038135169060208101359060408101359060600135610c02565b610221600480360360a081101561035f57600080fd5b810190602081018135600160201b81111561037957600080fd5b82018360208201111561038b57600080fd5b803590602001918460208302840111600160201b831117156103ac57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295949360208101935035915050600160201b8111156103fb57600080fd5b82018360208201111561040d57600080fd5b803590602001918460208302840111600160201b8311171561042e57600080fd5b9190808060200260200160405190810160405280939291908181526020018383602002808284376000920191909152509295505082359350505060208101359060400135610d9f565b34801561048357600080fd5b50610139610e06565b34801561049857600080fd5b50610139610e15565b3480156104ad57600080fd5b50610188610e24565b3480156104c257600080fd5b5061058f600480360360a08110156104d957600080fd5b6001600160a01b03823581169260208101358216926040820135909216916060820135919081019060a081016080820135600160201b81111561051b57600080fd5b82018360208201111561052d57600080fd5b803590602001918460018302840111600160201b8311171561054e57600080fd5b91908080601f016020809104026020016040519081016040528093929190818152602001838380828437600092019190915250929550610e81945050505050565b6040805160208082528351818301528351919283929083019185019080838360005b838110156105c95781810151838201526020016105b1565b50505050905090810190601f1680156105f65780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561061057600080fd5b506101396004803603602081101561062757600080fd5b50356001600160a01b0316611083565b34801561064357600080fd5b506101396004803603602081101561065a57600080fd5b50356001600160a01b0316611134565b61058f600480360360c081101561068057600080fd5b6001600160a01b0382358116926020810135909116916040820135916060810135916080820135919081019060c0810160a0820135600160201b8111156106c657600080fd5b8201836020820111156106d857600080fd5b803590602001918460018302840111600160201b831117156106f957600080fd5b509092509050611196565b6102216004803603608081101561071a57600080fd5b506001600160a01b0381351690602081013590604081013590606001356113ba565b34801561074857600080fd5b506101396004803603602081101561075f57600080fd5b50356001600160a01b03166113d2565b34801561077b57600080fd5b506101396113ed565b34801561079057600080fd5b506101396113fc565b6004546001600160a01b031681565b6005546001600160a01b031633146107f4576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b6001600160a01b03811661083f576040805162461bcd60e51b815260206004820152600d60248201526c24a72b20a624a22fa7aba722a960991b604482015290519081900360640190fd5b600580546001600160a01b0319166001600160a01b0392909216919091179055565b61086d8260008661140b565b600580546001600160a01b03199081166001600160a01b0397881617909155600080548216948716949094179093556006805490931694169390931790555050565b600061a4b160ff16336001600160a01b0316638e5f5ad16040518163ffffffff1660e01b815260040160206040518083038186803b1580156108f057600080fd5b505afa158015610904573d6000803e3d6000fd5b505050506040513d602081101561091a57600080fd5b505160ff1614610963576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d0549097d1539050931151608a1b604482015290519081900360640190fd5b610975866001600160a01b0316611482565b6109b8576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d513d7d0d3d395149050d5608a1b604482015290519081900360640190fd5b60006109c333611134565b90506001600160a01b038116158015906109eb57506004546001600160a01b03828116911614155b15610a5657866001600160a01b0316816001600160a01b031614610a56576040805162461bcd60e51b815260206004820152601b60248201527f4e4f5f5550444154455f544f5f444946464552454e545f414444520000000000604482015290519081900360640190fd5b604080516001808252818301909252606091602080830190803683370190505090503381600081518110610a8657fe5b6001600160a01b0392909216602092830291909101909101526040805160018082528183019092526060918160200160208202803683370190505090508881600081518110610ad157fe5b60200260200101906001600160a01b031690816001600160a01b031681525050610aff82828a8a8a8a611488565b9998505050505050505050565b6001546001600160a01b031681565b6040805162461bcd60e51b815260206004820152601460248201527327a7262cafa7aaaa2127aaa7222fa927aaaa22a960611b604482015290519081900360640190fd5b6000546001600160a01b03163314610bae576040805162461bcd60e51b815260206004820152600d60248201526c1393d517d19493d357d31254d5609a1b604482015290519081900360640190fd5b600080546001600160a01b0383166001600160a01b0319909116811790915560408051918252517f37389c47920d5cc3229678a0205d0455002c07541a4139ebdce91ac2274657779181900360200190a150565b6005546000906001600160a01b03163314610c51576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b600480546001600160a01b0387166001600160a01b0319909116811790915560408051918252517f3a8f8eb961383a94d41d193e16a3af73eaddfd5764a4c640257323a1603ac3319181900360200190a160006001600160a01b03861615610d1b57856001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b158015610cec57600080fd5b505afa158015610d00573d6000803e3d6000fd5b505050506040513d6020811015610d1657600080fd5b505190505b604080516001600160a01b038084166024808401919091528351808403909101815260449092018352602082810180516001600160e01b031663f7c9362f60e01b17905260065460015485516060810187528981529283018b90529482018990529293610d949383169216903390349060009087611905565b979650505050505050565b6005546000906001600160a01b03163314610dee576040805162461bcd60e51b815260206004820152600a60248201526927a7262cafa7aba722a960b11b604482015290519081900360640190fd5b610dfc868686868633611488565b9695505050505050565b6005546001600160a01b031681565b6000546001600160a01b031681565b6000610e2e611924565b9050336001600160a01b03821614610e7e576040805162461bcd60e51b815260206004820152600e60248201526d2727aa2fa32927a6afa0a226a4a760911b604482015290519081900360640190fd5b50565b60606000610e8e87611134565b9050806001600160a01b031663a0c76a9688888888886040518663ffffffff1660e01b815260040180866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b03168152602001846001600160a01b03166001600160a01b0316815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015610f3e578181015183820152602001610f26565b50505050905090810190601f168015610f6b5780820380516001836020036101000a031916815260200191505b50965050505050505060006040518083038186803b158015610f8c57600080fd5b505afa158015610fa0573d6000803e3d6000fd5b505050506040513d6000823e601f3d908101601f191682016040526020811015610fc957600080fd5b8101908080516040519392919084600160201b821115610fe857600080fd5b908301906020820185811115610ffd57600080fd5b8251600160201b81118282018810171561101657600080fd5b82525081516020918201929091019080838360005b8381101561104357818101518382015260200161102b565b50505050905090810190601f1680156110705780820380516001836020036101000a031916815260200191505b5060405250505091505095945050505050565b60008061108f83611134565b90506001600160a01b0381166110a957600091505061112f565b806001600160a01b031663a7e28d48846040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b1580156110ff57600080fd5b505afa158015611113573d6000803e3d6000fd5b505050506040513d602081101561112957600080fd5b50519150505b919050565b6001600160a01b03808216600090815260036020526040902054168061116257506004546001600160a01b03165b6001600160a01b038116600114806111895750611187816001600160a01b0316611482565b155b1561112f5750600061112f565b6000546060906001600160a01b031615611264576000546040805163babcc53960e01b815233600482015290516001600160a01b039092169163babcc53991602480820192602092909190829003018186803b1580156111f557600080fd5b505afa158015611209573d6000803e3d6000fd5b505050506040513d602081101561121f57600080fd5b5051611264576040805162461bcd60e51b815260206004820152600f60248201526e1393d517d5d2125511531254d51151608a1b604482015290519081900360640190fd5b60008383604081101561127657600080fd5b81359190810190604081016020820135600160201b81111561129757600080fd5b8201836020820111156112a957600080fd5b803590602001918460018302840111600160201b831117156112ca57600080fd5b91908080601f0160208091040260200160405190810160405280939291908181526020018383808284376000920191909152509697505050508989028501935050508215159050611357576040805162461bcd60e51b81526020600482015260126024820152711393d7d4d550935254d4d253d397d0d3d4d560721b604482015290519081900360640190fd5b80341461139d576040805162461bcd60e51b815260206004820152600f60248201526e57524f4e475f4554485f56414c554560881b604482015290519081900360640190fd5b6113ac8a8a8a8a8a8a8a611949565b9a9950505050505050505050565b60006113c985858585336108af565b95945050505050565b6003602052600090815260409020546001600160a01b031681565b6002546001600160a01b031681565b6006546001600160a01b031681565b6001600160a01b03821615611454576040805162461bcd60e51b815260206004820152600a6024820152692120a22fa927aaaa22a960b11b604482015290519081900360640190fd5b61145e8383611b9e565b600480546001600160a01b0319166001600160a01b03929092169190911790555050565b3b151590565b600085518751146114cf576040805162461bcd60e51b815260206004820152600c60248201526b0aea49e9c8ebe988a9c8ea8960a31b604482015290519081900360640190fd5b60005b87518110156117d0578681815181106114e757fe5b6020026020010151600360008a84815181106114ff57fe5b60200260200101516001600160a01b03166001600160a01b0316815260200190815260200160002060006101000a8154816001600160a01b0302191690836001600160a01b0316021790555086818151811061155757fe5b60200260200101516001600160a01b031688828151811061157457fe5b60200260200101516001600160a01b03167f812ca95fe4492a9e2d1f2723c2c40c03a60a27b059581ae20ac4e4d73bfba35460405160405180910390a360006001600160a01b03168782815181106115c857fe5b60200260200101516001600160a01b03161415801561160d575060016001600160a01b03168782815181106115f957fe5b60200260200101516001600160a01b031614155b156117c85760006001600160a01b031687828151811061162957fe5b60200260200101516001600160a01b031663a7e28d488a848151811061164b57fe5b60200260200101516040518263ffffffff1660e01b815260040180826001600160a01b03166001600160a01b0316815260200191505060206040518083038186803b15801561169957600080fd5b505afa1580156116ad573d6000803e3d6000fd5b505050506040513d60208110156116c357600080fd5b50516001600160a01b03161415611721576040805162461bcd60e51b815260206004820152601c60248201527f544f4b454e5f4e4f545f48414e444c45445f42595f4741544557415900000000604482015290519081900360640190fd5b86818151811061172d57fe5b60200260200101516001600160a01b0316632db09c1c6040518163ffffffff1660e01b815260040160206040518083038186803b15801561176d57600080fd5b505afa158015611781573d6000803e3d6000fd5b505050506040513d602081101561179757600080fd5b505187518890839081106117a757fe5b60200260200101906001600160a01b031690816001600160a01b0316815250505b6001016114d2565b506060634201f98560e01b8888604051602401808060200180602001838103835285818151815260200191508051906020019060200280838360005b8381101561182457818101518382015260200161180c565b50505050905001838103825284818151815260200191508051906020019060200280838360005b8381101561186357818101518382015260200161184b565b50505050905001945050505050604051602081830303815290604052906001600160e01b0319166020820180516001600160e01b03838183161783525050505090506118f9600660009054906101000a90046001600160a01b0316600160009054906101000a90046001600160a01b03168534600060405180606001604052808b81526020018d81526020018c81525087611905565b98975050505050505050565b60006118f98888888888886000015189602001518a604001518a611c6a565b7fb53127684a568b3173ae13b9f8a6016e243e63b6e8ee1178d6a717850b5d61035490565b6060600061195689611134565b90506060611965338686611e7d565b604080516001600160a01b0385811682529151929350818c169233928e16917f85291dff2161a93c2f12c819d31889c96c63042116f5bc5a205aa701c2c429f5919081900360200190a4816001600160a01b031663d2ce7d65348c8c8c8c8c886040518863ffffffff1660e01b815260040180876001600160a01b03166001600160a01b03168152602001866001600160a01b03166001600160a01b0316815260200185815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611a53578181015183820152602001611a3b565b50505050905090810190601f168015611a805780820380516001836020036101000a031916815260200191505b509750505050505050506000604051808303818588803b158015611aa357600080fd5b505af1158015611ab7573d6000803e3d6000fd5b50505050506040513d6000823e601f3d908101601f191682016040526020811015611ae157600080fd5b8101908080516040519392919084600160201b821115611b0057600080fd5b908301906020820185811115611b1557600080fd5b8251600160201b811182820188101715611b2e57600080fd5b82525081516020918201929091019080838360005b83811015611b5b578181015183820152602001611b43565b50505050905090810190601f168015611b885780820380516001836020036101000a031916815260200191505b5060405250505092505050979650505050505050565b6001600160a01b038216611bef576040805162461bcd60e51b81526020600482015260136024820152721253959053125117d0d3d55395115494105495606a1b604482015290519081900360640190fd5b6001546001600160a01b031615611c3c576040805162461bcd60e51b815260206004820152600c60248201526b1053149150511657d253925560a21b604482015290519081900360640190fd5b600180546001600160a01b039384166001600160a01b03199182161790915560028054929093169116179055565b6000808a6001600160a01b031663679b6ded898c8a8a8e8f8c8c8c6040518a63ffffffff1660e01b815260040180896001600160a01b03166001600160a01b03168152602001888152602001878152602001866001600160a01b03166001600160a01b03168152602001856001600160a01b03166001600160a01b0316815260200184815260200183815260200180602001828103825283818151815260200191508051906020019080838360005b83811015611d31578181015183820152602001611d19565b50505050905090810190601f168015611d5e5780820380516001836020036101000a031916815260200191505b5099505050505050505050506020604051808303818588803b158015611d8357600080fd5b505af1158015611d97573d6000803e3d6000fd5b50505050506040513d6020811015611dae57600080fd5b81019080805190602001909291905050509050808a6001600160a01b03168a6001600160a01b03167fc1d1490cf25c3b40d600dfb27c7680340ed1ab901b7e8f3551280968a3b372b0866040518080602001828103825283818151815260200191508051906020019080838360005b83811015611e35578181015183820152602001611e1d565b50505050905090810190601f168015611e625780820380516001836020036101000a031916815260200191505b509250505060405180910390a49a9950505050505050505050565b606083838360405160200180846001600160a01b03166001600160a01b0316815260200180602001828103825284848281815260200192508082843760008184015260408051601f19601f909301831690940184810390920184525250999850505050505050505056fea2646970667358221220209a87e0b2b16371b8726764d296199cdef67a4e7dbc1a7892b6be8216dc205164736f6c634300060b0033"
                    //var contractAbi = l1Token.ContractBuilder.ContractABI;
                    //string functionData = new ConstructorCallEncoder().EncodeRequest(
                    //    contractByteCode: contractByteCode,
                    //    parameters: contractAbi.Constructor.InputParameters,
                    //    values: new object[] { gatewayAddress, parameters.Amount ?? MAX_APPROVAL }
                    //    );

                    string functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: contractByteCode,
                        //contractByteCode: HelperMethods.GetBytecodeFromABI(abiFilePath) ?? null,
                        parameters: functionAbi?.InputParameters,
                        values: new object[]
                        {
                            tokenGateways.Select(a => a?.TokenAddr),
                            tokenGateways.Select(a => a?.GatewayAddr),
                            parameters?.GasLimit,
                            parameters?.MaxFeePerGas,
                            parameters?.MaxSubmissionCost
                        });

                    BigInteger? value = parameters?.GasLimit * parameters?.MaxFeePerGas + parameters?.MaxSubmissionCost;

                    return new TransactionRequest
                    {
                        Data = functionData,
                        Value = value,
                        From = from,
                        To = l1GatewayRouter.Address
                    };
                };

                var gEstimator = new L1ToL2MessageGasEstimator(l2Provider);
                var estimates = await gEstimator.PopulateFunctionParams(setGatewaysFunc, new Web3(l1Signer?.TransactionManager?.Client), options);

                var res = await l1Signer.TransactionManager.SendTransactionAndWaitForReceiptAsync(new TransactionInput
                {
                    To = estimates?.To,
                    Data = estimates?.Data,
                    Value = new HexBigInteger(estimates?.Estimates?.Deposit?.ToString()),
                    From = from
                });
                return L1TransactionReceipt.MonkeyPatchContractCallWait(res);
            }

        }
    }
}