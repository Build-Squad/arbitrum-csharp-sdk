using Arbitrum.AssetBridgerModule;
using Arbitrum.ContractFactory.L1GatewayRouter;
using Arbitrum.ContractFactory.L2ArbitrumGateway;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.src.Lib.DataEntities;
using Arbitrum.Utils;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Newtonsoft.Json.Linq;
using System.Numerics;
using OutboundTransferFunction = Arbitrum.ContractFactory.L1GatewayRouter.OutboundTransferFunction;

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
        public SignerOrProvider? L1Signer { get; set; } = null;
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

    public class TokenAndGateway
    {
        public string? TokenAddr { get; set; }
        public string? GatewayAddr { get; set; }
    }

    [Function("approve", "bool")]
    public class ApproveFunctionInput
    {
        [Parameter("address", "spender", 1)]
        public string Spender { get; set; }

        [Parameter("uint256", "value", 2)]
        public BigInteger Value { get; set; }
    }

    public class Erc20Bridger : AssetBridger<Erc20DepositParams, Erc20WithdrawParams, L1ContractCallTransactionReceipt>
    {
        public static BigInteger MAX_APPROVAL { get; } = 1152921504606846976;

        public static BigInteger MIN_CUSTOM_DEPOSIT_GAS_LIMIT { get; set; } = 275000;

        public Erc20Bridger(L2Network l2Network) : base(l2Network) { }

        public class RevertParams
        {
            public string? From { get; set; }
            public string? To { get; set; }
            public string? ExcessFeeRefundAddress { get; set; }
            public string? CallValueRefundAddress { get; set; }
            public BigInteger L2CallValue { get; set; }
            public byte[]? Data { get; set; }
            public BigInteger MaxSubmissionCost { get; set; }
            public BigInteger? Value { get; set; }
            public BigInteger GasLimit { get; set; }
            public BigInteger MaxFeePerGas { get; set; }
        }

        public static async Task<Erc20Bridger> FromProvider(Web3 l2Provider)
        {
            L2Network l2Network = await NetworkUtils.GetL2Network(l2Provider);
            return new Erc20Bridger(l2Network);
        }

        public async Task<string> GetL1GatewayAddress(string erc20L1Address, SignerOrProvider l1Signer, L2Network l2Network)
        {
            var l1GatewayRouter = await LoadContractUtils.LoadContract("L1GatewayRouter", l1Signer.Provider, l2Network.TokenBridge.L1GatewayRouter, true);

            return await l1GatewayRouter.GetFunction("getGateway").CallAsync<string>(erc20L1Address);
        }

        public async Task<string> GetL2GatewayAddress(SignerOrProvider l2Signer, L2Network l2Network, string erc20L1Address)
        {
            await CheckL2Network(l2Signer.Provider);

            var l2GatewayRouter = await LoadContractUtils.LoadContract("L2GatewayRouter", l2Signer.Provider, l2Network.TokenBridge.L2GatewayRouter, true);

            return await l2GatewayRouter.GetFunction("getGateway").CallAsync<string>(erc20L1Address);
        }

        public async Task<TransactionRequest> GetApproveGasTokenRequest(ProviderTokenApproveParams parameters, SignerOrProvider l1Signer)
        {
            if (NativeTokenIsEth)
            {
                throw new InvalidOperationException("Chain uses ETH as its native/gas token");
            }

            var txRequest = await GetApproveTokenRequest(parameters, l1Signer);

            txRequest.To = NativeToken;

            return txRequest;
        }

        public async Task<TransactionRequest> GetApproveTokenRequest(ProviderTokenApproveParams parameters, SignerOrProvider l1Signer)
        {
            var gatewayAddress = await GetL1GatewayAddress(parameters?.Erc20L1Address!, l1Signer, L2Network);

            var approveFunctionInput = new ApproveFunctionInput
            {
                Spender = gatewayAddress,
                Value = parameters?.Amount ?? MAX_APPROVAL
            };

            var sha3Signature = new Sha3Keccack().CalculateHash("approve(address,uint256)")[..8];
            var functionCallEncoder = new FunctionCallEncoder();
            var encodedABI = functionCallEncoder.EncodeRequest(approveFunctionInput, sha3Signature);

            return new TransactionRequest
            {
                To = parameters?.Erc20L1Address,
                Data = encodedABI,
                Value = new HexBigInteger(0)
            };
        }

        private static bool IsApproveParams(ApproveParamsOrTxRequest parameters)
        {
            return parameters is SignerTokenApproveParams signerParams && !string.IsNullOrEmpty(signerParams.Erc20L1Address);
        }

        public async Task<TransactionReceipt> ApproveToken(ApproveParamsOrTxRequest parameters)
        {
            await CheckL1Network(parameters?.L1Signer);

            var provider = SignerProviderUtils.GetProviderOrThrow(parameters?.L1Signer);

            TransactionRequest approveRequest;
            if (IsApproveParams(parameters))
            {
                var signerTokenApproveParams = HelperMethods.CopyMatchingProperties<ProviderTokenApproveParams, ApproveParamsOrTxRequest>(parameters);
                signerTokenApproveParams.L1Provider = provider;
                approveRequest = await GetApproveTokenRequest(signerTokenApproveParams, parameters.L1Signer);
            }
            else
            {
                approveRequest = parameters?.TxRequest;
            }

            provider.TransactionReceiptPolling.SetPollingRetryIntervalInMilliseconds(200);
            provider.TransactionManager.UseLegacyAsDefault = true;

            approveRequest.From ??= parameters?.L1Signer?.Account.Address;
            approveRequest.Gas ??= await provider.Eth.TransactionManager.EstimateGasAsync(approveRequest);

            var txnSign = await provider.TransactionManager.SignTransactionAsync(approveRequest);
            var txnHash = await provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(txnSign);
            var receipt = await provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txnHash);

            return receipt;
        }

        public async Task<List<(WithdrawalInitiatedEventDTO? EventArgs, string? TxHash)>> GetL2WithdrawalEvents(
        Web3 l2Provider,
        string gatewayAddress,
        NewFilterInput filter,
        string? l1TokenAddress = null,
        string? fromAddress = null,
        string? toAddress = null)
        {
            await CheckL2Network(new SignerOrProvider(l2Provider));

            var argumentFilters = new Dictionary<string, object>();

            var eventFetcher = new EventFetcher(l2Provider);

            var eventList = await eventFetcher.GetEventsAsync<WithdrawalInitiatedEventDTO>(
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

            var events = eventList.Select(a => (a?.Event, a?.TransactionHash)).ToList();

            return !string.IsNullOrEmpty(l1TokenAddress)
            ? events.Where(e => string.Equals(e.Event.L1Token, l1TokenAddress, StringComparison.OrdinalIgnoreCase)).ToList()
            : events;
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
                if (err is RpcResponseException rpcErr && rpcErr.RpcError != null && rpcErr.RpcError.Message == "CALL_EXCEPTION")
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

        public async Task<Contract> GetL2TokenContract(SignerOrProvider l2Signer, string l2TokenAddr)
        {
            var l2GatewayToken = await LoadContractUtils.LoadContract("L2GatewayToken", l2Signer.Provider, l2TokenAddr, true);

            return l2GatewayToken;
        }

        public async Task<Contract> GetL1TokenContract(Web3 l1Provider, string l1TokenAddress)
        {
            var contract = await LoadContractUtils.LoadContract("ERC20", l1Provider, l1TokenAddress, true);

            return contract;
        }

        public async Task<string> GetL2ERC20Address(string erc20L1Address, SignerOrProvider l1Signer, L2Network l2Network)
        {
            await CheckL1Network(l1Signer.Provider);

            var l1GatewayRouter = await LoadContractUtils.LoadContract("L1GatewayRouter", l1Signer.Provider, l2Network.TokenBridge.L1GatewayRouter, true);

            return await l1GatewayRouter.GetFunction("calculateL2TokenAddress").CallAsync<string>(erc20L1Address);
        }

        public async Task<string> GetL1ERC20Address(SignerOrProvider l2Signer, L2Network l2Network, string erc20L2Address)
        {
            await CheckL2Network(l2Signer.Provider);

            if (erc20L2Address.Equals(L2Network?.TokenBridge?.L2Weth, StringComparison.OrdinalIgnoreCase))
            {
                return L2Network?.TokenBridge?.L1Weth;
            }

            var l2GatewayToken = await LoadContractUtils.LoadContract("L2GatewayToken", l2Signer.Provider, erc20L2Address, true);

            var l1Address = await l2GatewayToken.GetFunction("l1Address").CallAsync<string>();

            var l2GatewayRouter = await LoadContractUtils.LoadContract("L2GatewayRouter", l2Signer.Provider, l2Network.TokenBridge.L2GatewayRouter, true);

            var l2Address = await l2GatewayRouter.GetFunction("calculateL2TokenAddress").CallAsync<string>(l1Address);

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
                                                address: L2Network?.TokenBridge?.L1GatewayRouter,
                                                isClassic: true
                                                );

            var gatewayAddress = await l1GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1TokenAddress);
            return gatewayAddress == Constants.DISABLED_GATEWAY;
        }

        private static DefaultedDepositRequest ApplyDefaults<T>(T parameters) where T : DepositRequest
        {
            var defaultedParams = HelperMethods.CopyMatchingProperties<DefaultedDepositRequest, DepositRequest>(parameters);

            defaultedParams.ExcessFeeRefundAddress ??= parameters?.From;
            defaultedParams.CallValueRefundAddress ??= parameters?.From;
            defaultedParams.DestinationAddress ??= parameters?.From;

            return defaultedParams;
        }

        private static byte[] SolidityEncode(string[] types, object[] values)
        {
            var encoder = new ABIEncode();

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] is string str && str == "0x")
                {
                    values[i] = new byte[0]; // Empty byte array
                }
            }

            return encoder.GetABIEncoded(ConvertValuesToDefaultABIValues(types, values));
        }

        private static object[] ConvertValuesToDefaultABIValues(string[] types, object[] values)
        {
            object[] defaultABIValues = new object[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                if (types[i] == "uint256")
                {
                    defaultABIValues[i] = BigInteger.Parse(values[i].ToString());
                }
                else
                {
                    defaultABIValues[i] = values[i];
                }
            }

            return defaultABIValues;
        }

        public async Task<L1ToL2TransactionRequest> GetDepositRequest(DepositRequest parameters)
        {
            await CheckL1Network(parameters?.L1Provider);
            await CheckL2Network(parameters?.L2Provider);

            var defaultedParams = ApplyDefaults(parameters);

            var amount = defaultedParams?.Amount;
            var destinationAddress = defaultedParams?.DestinationAddress;
            var erc20L1Address = defaultedParams?.Erc20L1Address;
            var l1Provider = defaultedParams?.L1Provider;
            var l2Provider = defaultedParams?.L2Provider;
            var l1Signer = defaultedParams?.L1Signer;
            var retryableGasOverrides = defaultedParams?.RetryableGasOverrides;

            var l1GatewayAddress = await GetL1GatewayAddress(erc20L1Address, l1Signer, L2Network);

            retryableGasOverrides ??= new GasOverrides();

            if (l1GatewayAddress == L2Network?.TokenBridge?.L1CustomGateway)
            {
                retryableGasOverrides.GasLimit ??= new PercentIncreaseWithMin();
                retryableGasOverrides.GasLimit.Min ??= MIN_CUSTOM_DEPOSIT_GAS_LIMIT;
            }

            CallInput depositFunc(L1ToL2MessageGasParams depositParams)
            {
                string[] types = { "uint256", "bytes" };
                object[] values = { depositParams.MaxSubmissionCost, "0x" };
                var innerData = SolidityEncode(types, values);

                var util = new AddressUtil();

                var outboundFunc = new OutboundTransferFunction
                {
                    Token = util.IsChecksumAddress(erc20L1Address) ? erc20L1Address : util.ConvertToChecksumAddress(erc20L1Address),
                    To = util.IsChecksumAddress(destinationAddress) ? destinationAddress : util.ConvertToChecksumAddress(destinationAddress),
                    Amount = defaultedParams.Amount.Value,
                    MaxGas = depositParams.GasLimit.Value,
                    GasPriceBid = depositParams.MaxFeePerGas.Value,
                    Data = innerData
                };

                var contractHandler = l1Provider.Eth.GetContractHandler(L2Network.TokenBridge.L1GatewayRouter);
                var encodedData = contractHandler.GetFunction<OutboundTransferFunction>().GetData(outboundFunc);

                var callInput = new CallInput
                {
                    From = defaultedParams.From,
                    To = L2Network.TokenBridge.L1GatewayRouter,
                    Data = encodedData,
                    Value = (depositParams?.GasLimit * depositParams?.MaxFeePerGas + depositParams?.MaxSubmissionCost).Value.ToHexBigInteger()
                };

                return callInput;
            }

            var gasEstimator = new L1ToL2MessageGasEstimator(l2Provider);

            var estimates = await gasEstimator.PopulateFunctionParams(depositFunc, l1Signer.Provider, retryableGasOverrides);

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
                    Data = estimates?.Retryable.Data,
                    To = estimates?.Retryable.To,
                    Value = estimates?.Value,
                    From = estimates?.Retryable?.From,
                    ExcessFeeRefundAddress = estimates?.Retryable?.ExcessFeeRefundAddress,
                    CallValueRefundAddress = estimates?.Retryable?.CallValueRefundAddress,
                    GasLimit = estimates?.Retryable?.GasLimit,
                    L2CallValue = estimates?.Retryable?.L2CallValue,
                    MaxFeePerGas = estimates?.Retryable?.MaxFeePerGas,
                    MaxSubmissionCost = estimates?.Estimates?.MaxSubmissionCost
                },
                IsValid = async () => await L1ToL2MessageGasEstimator.IsValid(
                    estimates?.Estimates, (await gasEstimator.PopulateFunctionParams(depositFunc, l1Signer.Provider, retryableGasOverrides))?.Estimates)
            };
        }

        public override async Task<L1ContractCallTransactionReceipt> Deposit(dynamic parameters)
        {
            await CheckL1Network(parameters?.L1Signer);

            L1ToL2TransactionRequest tokenDeposit;

            var l1Provider = SignerProviderUtils.GetProviderOrThrow(parameters?.L1Signer);

            if (DataEntities.TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                tokenDeposit = parameters;
            }
            else
            {
                tokenDeposit = await GetDepositRequest(new DepositRequest
                {
                    Erc20L1Address = parameters?.Erc20L1Address,
                    L1Provider = l1Provider,
                    From = parameters?.L1Signer?.Account?.Address,
                    Amount = parameters?.Amount,
                    DestinationAddress = parameters?.DestinationAddress,
                    L2Provider = parameters?.L2Provider,
                    RetryableGasOverrides = parameters?.RetryableGasOverrides,
                    CallValueRefundAddress = parameters?.CallValueRefundAddress,
                    ExcessFeeRefundAddress = parameters?.ExcessFeeRefundAddress,
                    MaxSubmissionCost = parameters?.MaxSubmissionCost,
                    L1Signer = parameters?.L1Signer
                });
            }

            var tx = new TransactionInput()
            {
                To = tokenDeposit.TxRequest.To,
                Value = tokenDeposit.TxRequest.Value,
                Data = tokenDeposit.TxRequest.Data
            };

            var provider = parameters?.L1Signer?.Provider;
            provider.TransactionReceiptPolling.SetPollingRetryIntervalInMilliseconds(200);
            provider.TransactionManager.UseLegacyAsDefault = true;

            tx.From ??= parameters?.L1Signer?.Account.Address;
            tx.Gas ??= await provider?.Eth.TransactionManager.EstimateGasAsync(tx);

            var txnSign = await provider.TransactionManager.SignTransactionAsync(tx);
            var txnHash = await provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(txnSign);
            var txReceipt = await provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txnHash);
            return L1TransactionReceipt.MonkeyPatchContractCallWait(txReceipt);
        }

        public async Task<L2ToL1TransactionRequest> GetWithdrawalRequest(Erc20WithdrawParams parameters)
        {
            var toAddress = parameters.DestinationAddress!;
            var provider = parameters?.L2Signer?.Provider;

            var util = new AddressUtil();

            var outboundFunc = new OutboundTransferFunction2
            {
                L1Token = util.ConvertToChecksumAddress(parameters?.Erc20L1Address),
                To = util.ConvertToChecksumAddress(parameters?.DestinationAddress),
                Amount = parameters.Amount.Value,
                Data = "0x".HexToByteArray()
            };

            var contractHandler = provider.Eth.GetContractHandler(L2Network.TokenBridge.L2GatewayRouter);
            var encodedData = contractHandler.GetFunction<OutboundTransferFunction2>().GetData(outboundFunc);

            var request = new L2ToL1TransactionRequest
            {
                TxRequest = new TransactionRequest
                {
                    Data = encodedData,
                    To = L2Network?.TokenBridge?.L2GatewayRouter,
                    Value = BigInteger.Zero.ToHexBigInteger(),
                    From = parameters?.From
                },
                EstimateL1GasLimit = async (l1Provider) =>
                {
                    if (await Lib.IsArbitrumChain(l1Provider))
                    {
                        return BigInteger.Parse("8000000");
                    }

                    var l1GatewayAddress = await GetL1GatewayAddress(parameters?.Erc20L1Address, parameters.L1Signer, L2Network);

                    bool isWeth = await IsWethGateway(l1GatewayAddress, l1Provider);

                    return isWeth ? BigInteger.Parse("180000") : BigInteger.Parse("160000");
                }
            };

            return request;
        }

        public override async Task<L2TransactionReceipt> Withdraw(dynamic parameters)
        {
            if (!SignerProviderUtils.SignerHasProvider(parameters?.L2Signer))
            {
                throw new MissingProviderArbSdkError("l2Signer");
            }

            await CheckL2Network(new SignerOrProvider(parameters.L2Signer.Provider));

            dynamic withdrawalRequest;

            if (DataEntities.TransactionUtils.IsL2ToL1TransactionRequest(parameters))
            {
                withdrawalRequest = parameters;
            }
            else
            {
                withdrawalRequest = await GetWithdrawalRequest(parameters);
            }

            var tx = withdrawalRequest.TxRequest;

            var provider = parameters?.L2Signer?.Provider;

            tx.From ??= parameters?.L1Signer?.Account.Address;
            tx.Gas ??= await provider?.Eth.TransactionManager.EstimateGasAsync(tx);

            var txnSign = await provider.TransactionManager.SignTransactionAsync(tx);
            var txnHash = await provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(txnSign);
            var txReceipt = await provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txnHash);
            return L2TransactionReceipt.MonkeyPatchWait(txReceipt);
        }

        public class GasParams
        {
            public BigInteger? MaxSubmissionCost { get; set; }
            public BigInteger? GasLimit { get; set; }
        }

        public class AdminErc20Bridger : Erc20Bridger
        {
            public AdminErc20Bridger(L2Network l2Network) : base(l2Network) { }

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

                string l1SenderAddress = l1Signer?.Account?.Address;

                var l1Token = LoadContract(
                        contractName: "ICustomToken",
                        address: l1TokenAddress,
                        provider: l1Signer?.Provider,
                        isClassic: true
                    );


                var l2Token = LoadContract(
                        contractName: "IArbToken",
                        address: l2TokenAddress,
                        provider: l2Provider,
                        isClassic: true
                    );

                if (!await LoadContractUtils.IsContractDeployed(l1Signer.Provider, l1Token.Address))
                {
                    throw new Exception("L1 token is not deployed.");
                }
                if (!await LoadContractUtils.IsContractDeployed(l2Provider, l2Token.Address))
                {
                    throw new Exception("L2 token is not deployed.");
                }

                var l1AddressFromL2 = await l2Token.GetFunction("l1Address").CallAsync<dynamic>();

                if (l1AddressFromL2.ToLower() != l1TokenAddress.ToLower())
                {
                    throw new ArbSdkError(
                        $"L2 token does not have l1 address set. Set address: {l1AddressFromL2}, expected address: {l1TokenAddress}."
                    );
                }

                CallInput encodeFuncData(GasParams setTokenGas, GasParams setGatewayGas, BigInteger? maxFeePerGas)
                {
                    var doubleFeePerGas = maxFeePerGas == RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas
                        ? RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas * 2
                        : maxFeePerGas;

                    var setTokenDeposit = (setTokenGas.GasLimit) * doubleFeePerGas + (setTokenGas.MaxSubmissionCost);
                    var setGatewayDeposit = (setGatewayGas.GasLimit) * doubleFeePerGas + (setGatewayGas.MaxSubmissionCost);

                    var util = new AddressUtil();
                    var functionData = l1Token.GetFunction("registerTokenOnL2").GetData(new object[]
                    {
                        util.ConvertToChecksumAddress(l2TokenAddress),
                        setTokenGas.MaxSubmissionCost,
                        setGatewayGas.MaxSubmissionCost,
                        setTokenGas.GasLimit,
                        setGatewayGas.GasLimit,
                        doubleFeePerGas,
                        setTokenDeposit,
                        setGatewayDeposit,
                        util.ConvertToChecksumAddress(l1SenderAddress)
                    });

                    return new CallInput
                    {
                        Data = functionData,
                        To = l1Token.Address,
                        Value = (setTokenDeposit + setGatewayDeposit).Value.ToHexBigInteger(),
                        From = l1SenderAddress
                    };
                }

                var l1Provider = l1Signer.Provider;
                var gEstimator = new L1ToL2MessageGasEstimator(l2Provider);

                var setTokenEstimate = await gEstimator.PopulateFunctionParams(
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
                     l1Signer.Provider
                 );

                var setGatewayEstimate = await gEstimator.PopulateFunctionParams(
                     (parameters) => encodeFuncData(
                         new GasParams
                         {
                             GasLimit = setTokenEstimate?.Estimates?.GasLimit,
                             MaxSubmissionCost = setTokenEstimate?.Estimates?.MaxSubmissionCost
                         },
                         new GasParams
                         {
                             GasLimit = parameters?.GasLimit,
                             MaxSubmissionCost = parameters?.MaxSubmissionCost
                         },
                         parameters?.MaxFeePerGas
                     ),
                     l1Signer.Provider
                 );

                var registerTx = new TransactionRequest
                {
                    To = l1Token?.Address,
                    Data = setGatewayEstimate?.Data,
                    Value = setGatewayEstimate?.Value?.ToHexBigInteger(),
                    From = l1SenderAddress
                };

                registerTx.From ??= l1Signer?.Account.Address;
                registerTx.Gas ??= await l1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(registerTx);

                var txnSign = await l1Signer.Provider.TransactionManager.SignTransactionAsync(registerTx);
                var txnHash = await l1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(txnSign);
                var receipt = await l1Signer.Provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txnHash);
                return L1TransactionReceipt.MonkeyPatchContractCallWait(receipt);
            }

            public async Task<List<GatewaySetEventDTO>> GetL1GatewaySetEvents(Web3 l1Provider, NewFilterInput filter)
            {
                await CheckL1Network(l1Provider);

                string l1GatewayRouterAddress = L2Network?.TokenBridge?.L1GatewayRouter;
                var eventFetcher = new EventFetcher(l1Provider);

                var argumentFilters = new Dictionary<string, object>();

                var eventList = await eventFetcher.GetEventsAsync<GatewaySetEventDTO>(
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

            public async Task<List<GatewaySetEventDTO>> GetL2GatewaySetEvents(
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

                var eventList = await eventFetcher.GetEventsAsync<GatewaySetEventDTO>(
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
                var estimates = await gEstimator.PopulateFunctionParams(null, l1Signer.Provider, options);

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
            public static Contract LoadContract(string contractName, Web3 provider, string address = null, bool isClassic = false)
            {
                // Determine the ABI file path
                string filePath;
                if (isClassic)
                {
                    filePath = $"src/abi/classic/{contractName}.json";
                }
                else
                {
                    filePath = $"src/abi/{contractName}.json";
                }

                // Load the ABI and bytecode from the JSON file
                string abi = null;
                string bytecode = null;
                using (StreamReader reader = new StreamReader(filePath))
                {
                    var contractData = JObject.Parse(reader.ReadToEnd());
                    abi = contractData["abi"]?.ToString();
                    bytecode = contractData["bytecode"]?.ToString();

                    if (string.IsNullOrEmpty(abi))
                    {
                        throw new Exception($"No ABI found for contract: {contractName}");
                    }
                }

                // Check if a contract address was provided
                if (!string.IsNullOrEmpty(address))
                {
                    // Convert the address to a checksum address
                    string contractAddress = Nethereum.Util.AddressUtil.Current.ConvertToChecksumAddress(address);
                    return provider.Eth.GetContract(abi, contractAddress);
                }

                return null;
            }


        }
    }
}