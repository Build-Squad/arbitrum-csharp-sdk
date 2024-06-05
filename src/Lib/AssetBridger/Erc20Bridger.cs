using Arbitrum.AssetBridgerModule;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Utils;
using Nethereum.ABI;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;

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
            //await CheckL1Network(l1Provider);

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

            // If 'From' field is null, set it to L1Signer's address
            if (approveGasTokenRequest.From == null)
            {
                approveGasTokenRequest.From = parameters?.L1Signer?.Account.Address;
            }

            // Retrieve the current nonce if not done automatically
            if (approveGasTokenRequest.Nonce == null)
            {
                var nonce = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(approveGasTokenRequest.From);

                approveGasTokenRequest.Nonce = nonce;
            }

            //estimate gas for the transaction if not done automatically
            if (approveGasTokenRequest.Gas == null)
            {
                var gas = await parameters.L1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(approveGasTokenRequest);

                approveGasTokenRequest.Gas = gas;
            }

            //sign transaction
            var signedTx = await parameters.L1Signer.Account.TransactionManager.SignTransactionAsync(approveGasTokenRequest);

            //send transaction
            var txnHash = await parameters.L1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

            // Get transaction receipt
            var receipt = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

            return receipt;
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

            var functionData = erc20Interface.GetFunction("approve").GetData(gatewayAddress, parameters?.Amount ?? MAX_APPROVAL);


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
                signerTokenApproveParams.L1Provider = provider;
                approveRequest = await GetApproveTokenRequest(signerTokenApproveParams);
            }
            else
            {
                // If the parameters are not of type SignerTokenApproveParams, use the existing transaction request
                approveRequest = parameters?.TxRequest;
            }

            // If 'From' field is null, set it to L1Signer's address
            if (approveRequest.From == null)
            {
                approveRequest.From = parameters?.L1Signer?.Account.Address;
            }

            // Retrieve the current nonce if not done automatically
            if (approveRequest.Nonce == null)
            {
                var nonce = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(approveRequest.From);

                approveRequest.Nonce = nonce;
            }

            //estimate gas for the transaction if not done automatically
            if (approveRequest.Gas == null)
            {
                var gas = await parameters.L1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(approveRequest);

                approveRequest.Gas = gas;
            }

            //sign transaction
            var signedTx = await parameters.L1Signer.Account.TransactionManager.SignTransactionAsync(approveRequest);

            //send transaction
            var txnHash = await parameters.L1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

            // Get transaction receipt
            var receipt = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

            // Send the transaction and return the transaction receipt
            return receipt;
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
            var defaultedParams = HelperMethods.CopyMatchingProperties<DefaultedDepositRequest, DepositRequest>(parameters); 

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

            // Define deposit function
            Func<L1ToL2MessageGasParams, TransactionRequest> depositFunc = (depositParams) =>
            {
                // Add missing maxSubmissionCost if necessary
                depositParams.MaxSubmissionCost = parameters?.MaxSubmissionCost ?? depositParams?.MaxSubmissionCost;

                byte[] innerData = GetDepositRequestOutboundTransferInnerData(depositParams);
                string functionData;
                if (defaultedParams.ExcessFeeRefundAddress != defaultedParams.From)
                {
                    functionData = iGatewayRouter.GetFunction("outboundTransferCustomRefund").GetData(
                        new object[]
                        {
                            AddressUtil.Current.ConvertToChecksumAddress(erc20L1Address),
                            AddressUtil.Current.ConvertToChecksumAddress(defaultedParams?.ExcessFeeRefundAddress),
                            AddressUtil.Current.ConvertToChecksumAddress(destinationAddress),
                            (int)amount!,
                            (int)depositParams?.GasLimit!,
                            (int)depositParams?.MaxFeePerGas!,
                            innerData
                        }
                        );
                }
                else
                {

                    functionData = iGatewayRouter.GetFunction("outboundTransfer").GetData(
                        new object[]
                        {
                            AddressUtil.Current.ConvertToChecksumAddress(erc20L1Address),
                            AddressUtil.Current.ConvertToChecksumAddress(destinationAddress),
                            (int)amount,
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
                    Value = GetDepositRequestCallValue(depositParams).Value.ToHexBigInteger()
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
                    Value = (estimates?.Value).Value.ToHexBigInteger(),
                    From = parameters?.From,
                },
                RetryableData = new RetryableData
                {
                    Data = estimates?.Data.HexToByteArray(),
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

            var l1Provider = SignerProviderUtils.GetProviderOrThrow(parameters?.L1Signer);

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
                MaxFeePerGas = parameters?.Overrides?.MaxFeePerGas.Value.ToHexBigInteger(),
                MaxPriorityFeePerGas = parameters?.Overrides?.MaxPriorityFeePerGas.Value.ToHexBigInteger(),
                Nonce = tokenDeposit?.TxRequest?.Nonce,
                Type = tokenDeposit?.TxRequest?.Type,
            };

            // If 'From' field is null, set it to L1Signer's address
            if (tx.From == null)
            {
                tx.From = parameters?.L1Signer?.Account.Address;
            }

            // Retrieve the current nonce if not done automatically
            if (tx.Nonce == null)
            {
                var nonce = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(tx.From);

                tx.Nonce = nonce;
            }

            //estimate gas for the transaction if not done automatically
            if (tx.Gas == null)
            {
                var gas = await parameters.L1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(tx);

                tx.Gas = gas;
            }

            //sign transaction
            var signedTx = await parameters.L1Signer.Account.TransactionManager.SignTransactionAsync(tx);

            //send transaction
            var txnHash = await parameters.L1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

            // Get transaction receipt
            var receipt = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

            // Apply monkey patching
            return L1TransactionReceipt.MonkeyPatchContractCallWait(receipt);
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

            var functionData = routerInterface.GetFunction("outboundTransfer").GetData(
                new object[]
                {
                    parameters?.Erc20L1Address!,
                    toAddress,
                    parameters?.Amount,
                    "0x"
                });

            // Create the transaction request
            L2ToL1TransactionRequest request = new L2ToL1TransactionRequest
            {
                TxRequest = new TransactionRequest
                {
                    Data = functionData,
                    To = L2Network?.TokenBridge?.L2GatewayRouter,
                    Value = BigInteger.Zero.ToHexBigInteger(),
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

            // If 'From' field is null, set it to L1Signer's address
            if (tx.From == null)
            {
                tx.From = parameters?.L2Signer?.Account.Address;
            }

            // Retrieve the current nonce if not done automatically
            if (tx.Nonce == null)
            {
                var nonce = await parameters.L2Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(tx.From);

                tx.Nonce = nonce;
            }

            //estimate gas for the transaction if not done automatically
            if (tx.Gas == null)
            {
                var gas = await parameters.L2Signer.Provider.Eth.TransactionManager.EstimateGasAsync(tx);

                tx.Gas = gas;
            }

            //sign transaction
            var signedTx = await parameters.L2Signer.Account.TransactionManager.SignTransactionAsync(tx);

            //send transaction
            var txnHash = await parameters.L2Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

            // Get transaction receipt
            var receipt = await parameters.L2Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

            return L2TransactionReceipt.MonkeyPatchWait(receipt);
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

                    var functionData = l1Token.GetFunction("registerTokenOnL2").GetData(
                        new object[]
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
                    Value = setTokenEstimates?.Value.Value.ToHexBigInteger(),
                    From = l1SenderAddress
                };

                // If 'From' field is null, set it to L1Signer's address
                if (registerTx.From == null)
                {
                    registerTx.From = l1Signer?.Account.Address;
                }

                // Retrieve the current nonce if not done automatically
                if (registerTx.Nonce == null)
                {
                    var nonce = await l1Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(registerTx.From);

                    registerTx.Nonce = nonce;
                }

                //estimate gas for the transaction if not done automatically
                if (registerTx.Gas == null)
                {
                    var gas = await l1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(registerTx);

                    registerTx.Gas = gas;
                }

                //sign transaction
                var signedTx = await l1Signer.Account.TransactionManager.SignTransactionAsync(registerTx);

                //send transaction
                var txnHash = await l1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

                // Get transaction receipt
                var receipt = await l1Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

                // Return the transaction receipt
                return L1TransactionReceipt.MonkeyPatchWait(receipt);
            }

            public async Task<List<GatewaySetEvent>> GetL1GatewaySetEvents(Web3 l1Provider, NewFilterInput filter)
            {
                await CheckL1Network(l1Provider);

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

                await CheckL2Network(l2Provider);

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

                // Define function for setting gateways
                Func<L1ToL2MessageGasParams, TransactionRequest> setGatewaysFunc = (parameters) =>
                {
                    var functionData = l1GatewayRouter.GetFunction("setGateways").GetData(
                        new object[]
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
                        Value = value.Value.ToHexBigInteger(),
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
                    Value = estimates?.Estimates?.Deposit.Value.ToHexBigInteger(),
                    From = from
                };
                // If 'From' field is null, set it to L1Signer's address
                if (resTx.From == null)
                {
                    resTx.From = l1Signer?.Account.Address;
                }

                // Retrieve the current nonce if not done automatically
                if (resTx.Nonce == null)
                {
                    var nonce = await l1Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(resTx.From);

                    resTx.Nonce = nonce;
                }

                //estimate gas for the transaction if not done automatically
                if (resTx.Gas == null)
                {
                    var gas = await l1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(resTx);

                    resTx.Gas = gas;
                }

                //sign transaction
                var signedTx = await l1Signer.Account.TransactionManager.SignTransactionAsync(resTx);

                //send transaction
                var txnHash = await l1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

                // Get transaction receipt
                var receipt = await l1Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

                return L1TransactionReceipt.MonkeyPatchContractCallWait(receipt);
            }

        }
    }
}