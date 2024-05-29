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
    }

    public class Erc20WithdrawParams : EthWithdrawParams
    {
        public string? Erc20L1Address { get; set; }
    }

    public class L1ToL2TxReqAndSignerProvider : L1ToL2TxReqAndSigner
    {
    }

    public class L2ToL1TxReqAndSigner : L2ToL1TransactionRequest
    {
        public SignerOrProvider? L2Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }
    public class SignerTokenApproveParams : TokenApproveParams
    {
        public SignerOrProvider? L1Signer { get; set; }
    }

    public class ProviderTokenApproveParams : TokenApproveParams
    {
        public Web3? L1Provider { get; set; }
    }
    public class ApproveParamsOrTxRequest : SignerTokenApproveParams
    {
        public TransactionRequest? TxRequest { get; set; }
    }

    public class DepositRequest : Erc20DepositParams
    {
        public Web3? L1Provider { get; set; }
        public string? From { get; set; }
    }
    public class DefaultedDepositRequest : DepositRequest
    {
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
    public class Erc20Bridger : AssetBridger<Erc20DepositParams, Erc20WithdrawParams, L1ContractCallTransactionReceipt>
    {
        public static BigInteger MAX_APPROVAL { get; set; } = BigInteger.Parse("18446744073709551615");
        public static BigInteger MIN_CUSTOM_DEPOSIT_GAS_LIMIT { get; set; } = BigInteger.Parse("275000");

        public Erc20Bridger(L2Network l2Network) : base(l2Network)
        {
        }

        public static async Task<Erc20Bridger> FromProvider(Web3 l2Provider)
        {
            L2Network l2Network = await NetworkUtils.GetL2Network(l2Provider);
            return new Erc20Bridger(l2Network);
        }

        public async Task<string> GetL1GatewayAddress(string erc20L1Address, Web3 l1Provider)
        {
            await CheckL1Network(l1Provider);

            // Load the L1GatewayRouter contract
            var l1GatewayRouter = await LoadContractUtils.LoadContract(
                contractName: "L1GatewayRouter",
                address: L2Network?.TokenBridge?.L1GatewayRouter,
                provider: l1Provider,
                isClassic: true
            );

            // Call the getGateway function
            var getGatewayFunction = l1GatewayRouter.GetFunction("getGateway");
            return await getGatewayFunction.CallAsync<dynamic>(erc20L1Address);
        }

        public async Task<string> GetL2GatewayAddress(string erc20L1Address, Web3 l2Provider)
        {
            await CheckL2Network(l2Provider);

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

            await CheckL1Network(parameters?.L1Signer);

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

            return await parameters?.L1Signer?.Account.TransactionManager?.SendTransactionAndWaitForReceiptAsync(approveGasTokenRequest)!;
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
                Value = new HexBigInteger(BigInteger.Zero)
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
            await CheckL1Network(parameters?.L1Signer);

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
            return await parameters?.L1Signer?.Account.TransactionManager?.SendTransactionAndWaitForReceiptAsync(approveRequest);
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
            await CheckL1Network(l1Provider);

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
            await CheckL2Network(l2Provider);

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
            await CheckL1Network(l1Provider);

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
            await CheckL1Network(parameters?.L1Provider);
            await CheckL2Network(parameters?.L2Provider);

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

                    functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: contractByteCode,
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

                    functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: contractByteCode,
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
                    Value = new HexBigInteger(GetDepositRequestCallValue(depositParams).Value)
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
                    Value = new HexBigInteger((estimates?.Value).Value),
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

        public override async Task<L1ContractCallTransactionReceipt> Deposit(dynamic parameters)
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

            var provider = parameters?.L2Signer?.Provider;

            // Create the router interface
            var routerInterface = await LoadContractUtils.LoadContract(
                        contractName: "L2GatewayRouter",
                        provider: provider,
                        isClassic: true
                    );

            var functionAbi = routerInterface.ContractBuilder.GetFunctionAbi("outboundTransfer");

            string functionData = new ConstructorCallEncoder().EncodeRequest(
                contractByteCode: await provider.Eth.GetCode.SendRequestAsync(routerInterface.Address),
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
                    Value = new HexBigInteger(BigInteger.Zero),
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
                SignerOrProvider l1Signer,
                Web3 l2Provider)
            { 
                if (!SignerProviderUtils.SignerHasProvider(l1Signer))
                {
                    throw new MissingProviderArbSdkError("l1Signer");
                }

                await CheckL1Network(l1Signer);
                await CheckL2Network(l2Provider);

                string l1SenderAddress = l1Signer?.Account?.Address;

                var l1Token = await LoadContractUtils.LoadContract(
                        contractName: "ICustomToken",
                        address: l1TokenAddress,
                        provider: l1Signer?.Provider,
                        isClassic: true
                    );
                var l1TokenContractByteCode = await l1Signer.Provider.Eth.GetCode.SendRequestAsync(l1Token.Address);

                var l2Token = await LoadContractUtils.LoadContract(
                        contractName: "IArbToken",
                        address: l2TokenAddress,
                        provider: l2Provider,
                        isClassic: true
                    );

                // Sanity checks
                if (!await LoadContractUtils.IsContractDeployed(l1Signer.Provider, l1Token.Address))
                {
                    throw new Exception("L1 token is not deployed.");
                }
                if (!await LoadContractUtils.IsContractDeployed(l2Provider, l2Token.Address))
                {
                    throw new Exception("L2 token is not deployed.");
                }

                string l1AddressFromL2 = await l2Token.GetFunction("l1Address").CallAsync<dynamic>();

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

                    string functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: l1TokenContractByteCode,
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
                        Value = new HexBigInteger((setTokenDeposit + setGatewayDeposit).Value),
                        From = l1SenderAddress
                    };
                };

                var l1Provider = l1Signer.Provider;
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

                var registerTx = new TransactionRequest()
                {
                    To = l1Token?.Address,
                    Data = setTokenEstimates?.Data,
                    Value = new HexBigInteger(setTokenEstimates?.Value.ToString()),
                    From = l1SenderAddress
                };

                // Perform the transaction
                var txReceipt = await l1Signer.Provider.TransactionManager.SendTransactionAndWaitForReceiptAsync(new TransactionRequest
                {
                    To = l1Token?.Address,
                    Data = setTokenEstimates?.Data,
                    Value = new HexBigInteger(setTokenEstimates?.Value.ToString()),
                    From = l1SenderAddress
                });

                // Return the transaction receipt
                return L1TransactionReceipt.MonkeyPatchWait(txReceipt);
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

            public async Task<L1ContractCallTransactionReceipt> SetGateways(
                SignerOrProvider l1Signer,
                Web3 l2Provider,
                List<TokenAndGateway> tokenGateways,
                GasOverrides? options = null)
            {
                if (!SignerProviderUtils.SignerHasProvider(l1Signer))
                {
                    throw new MissingProviderArbSdkError("l1Signer");
                }

                await CheckL1Network(l1Signer);
                await CheckL2Network(l2Provider);

                string from = l1Signer?.Account.Address;

                var l1GatewayRouter = await LoadContractUtils.LoadContract(
                            provider: l1Signer.Provider,
                            contractName: "L1GatewayRouter",
                            address: L2Network?.TokenBridge?.L1GatewayRouter,
                            isClassic: true
                            );
                var contractByteCode = await l1Signer.Provider.Eth.GetCode.SendRequestAsync(l1GatewayRouter.Address);

                // Define function for setting gateways
                Func<L1ToL2MessageGasParams, TransactionRequest> setGatewaysFunc = (parameters) =>
                {
                    var functionAbi = l1GatewayRouter.ContractBuilder.GetFunctionAbi("setGateways");

                    string functionData = new ConstructorCallEncoder().EncodeRequest(
                        contractByteCode: contractByteCode,
                        parameters: functionAbi?.InputParameters,
                        values: new object[]
                        {
                            tokenGateways.Select(a => a?.TokenAddr),
                            tokenGateways.Select(a => a?.GatewayAddr),
                            parameters?.GasLimit,
                            parameters?.MaxFeePerGas,
                            parameters?.MaxSubmissionCost
                        });

                    var value = parameters?.GasLimit * parameters?.MaxFeePerGas + parameters?.MaxSubmissionCost;

                    return new TransactionRequest
                    {
                        Data = functionData,
                        Value = new HexBigInteger(value.Value),
                        From = from,
                        To = l1GatewayRouter.Address
                    };
                };

                var gEstimator = new L1ToL2MessageGasEstimator(l2Provider);
                var estimates = await gEstimator.PopulateFunctionParams(setGatewaysFunc, l1Signer.Provider, options);

                var resTx = new TransactionRequest
                {
                    To = estimates?.To,
                    Data = estimates?.Data,
                    Value = new HexBigInteger(estimates?.Estimates?.Deposit?.ToString()),
                    From = from
                };

                var resReceipt = await l1Signer.Provider.TransactionManager.SendTransactionAndWaitForReceiptAsync(resTx);
                return L1TransactionReceipt.MonkeyPatchContractCallWait(resReceipt);
            }

        }
    }
}