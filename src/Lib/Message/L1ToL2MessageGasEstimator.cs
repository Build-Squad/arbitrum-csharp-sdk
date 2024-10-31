using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.src.Lib.DataEntities;
using Arbitrum.Utils;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;

namespace Arbitrum.Message
{
    public class GasOverrides
    {
        public PercentIncreaseWithMin? GasLimit { get; set; }
        public PercentIncreaseType? MaxSubmissionFee { get; set; }
        public PercentIncreaseType? MaxFeePerGas { get; set; }
        public PercentIncreaseWithOnlyBase? Deposit { get; set; }
    }
    public class  PercentIncreaseType
    {
        public BigInteger? Base {  get; set; }
        public BigInteger PercentIncrease {  get; set; }
    }
    public class PercentIncreaseWithOnlyBase
    {
        public BigInteger? Base { get; set; }
    }
    public class PercentIncreaseWithMin : PercentIncreaseType
    {
        public BigInteger? Min { get; set; }
    }
    public static class DefaultL1ToL2MessageEstimateOptions
    {
        public const int DefaultSubmissionFeePercentIncrease = 300;

        public const int DefaultGasPricePercentIncrease = 200;

        public static BigInteger MaxSubmissionFeePercentIncrease { get; } = DefaultSubmissionFeePercentIncrease;
        public static BigInteger GasLimitPercentIncrease { get; } = BigInteger.Zero;
        public static BigInteger MaxFeePerGasPercentIncrease { get; } = DefaultGasPricePercentIncrease;
    }

    public class PopulateFunctionParamsResult
    {
        public L1ToL2MessageGasParams? Estimates { get; set; }
        public RetryableData? Retryable { get; set; }
        public string? Data { get; set; }
        public string? To { get; set; }
        public BigInteger? Value { get; set; }
    }

    public class L1ToL2MessageGasEstimator
    {
        public readonly Web3 _l2Provider;
        public L1ToL2MessageGasEstimator(Web3 l2provider)
        {
            _l2Provider = l2provider;
        }

        public BigInteger? PercentIncrease(BigInteger? num, BigInteger increase)
        {
            BigInteger? increaseAmount = num * increase / 100;
            BigInteger? result = num + increaseAmount;

            return result;
        }

        private PercentIncreaseType ApplySubmissionPriceDefaults(PercentIncreaseType maxSubmissionFeeOptions)
        {
            var defaultOptions = new PercentIncreaseType
            {
                Base = maxSubmissionFeeOptions?.Base ?? null,
                PercentIncrease = maxSubmissionFeeOptions?.PercentIncrease
                                  ?? DefaultL1ToL2MessageEstimateOptions.MaxSubmissionFeePercentIncrease
            };

            return defaultOptions;
        }

        private PercentIncreaseType ApplyMaxFeePerGasDefaults(PercentIncreaseType maxFeePerGasOptions)
        {
            return new PercentIncreaseType
            {
                Base = maxFeePerGasOptions?.Base ?? null,
                PercentIncrease = maxFeePerGasOptions?.PercentIncrease
                                ?? DefaultL1ToL2MessageEstimateOptions.MaxFeePerGasPercentIncrease
            };
        }

        private PercentIncreaseWithMin ApplyGasLimitDefaults(PercentIncreaseWithMin gasLimitDefaults)
        {
            return new PercentIncreaseWithMin
            {
                Base = gasLimitDefaults?.Base ?? null,
                PercentIncrease = gasLimitDefaults?.PercentIncrease ?? DefaultL1ToL2MessageEstimateOptions.GasLimitPercentIncrease,
                Min = gasLimitDefaults?.Min ?? BigInteger.Zero
            };
        }

        public async Task<BigInteger?> EstimateSubmissionFee(
            Web3 l1Provider,
            BigInteger l1BaseFee,
            BigInteger callDataSize,
            PercentIncreaseType? options = null)
        {
            var defaultedOptions = ApplySubmissionPriceDefaults(options);

            var l2Network = await NetworkUtils.GetL2Network(_l2Provider);

            var inbox =  await LoadContractUtils.LoadContract(
                                                contractName: "Inbox",
                                                provider: l1Provider,
                                                address: l2Network.EthBridge?.Inbox,
                                                isClassic: false);

            BigInteger? baseValue = defaultedOptions.Base;
            if (baseValue == default)
            {
                baseValue = await inbox.GetFunction("calculateRetryableSubmissionFee").CallAsync<dynamic>(callDataSize, l1BaseFee);
            }

            var value = PercentIncrease(baseValue, defaultedOptions.PercentIncrease);
            return value;
        }

        public async Task<BigInteger> EstimateRetryableTicketGasLimit(
        L1ToL2MessageNoGasParams parameters,
        BigInteger? senderDeposit)
        {
            if (senderDeposit == BigInteger.Zero || senderDeposit == null)
            {
                senderDeposit = Web3.Convert.ToWei(1, UnitConversion.EthUnit.Ether) + parameters?.L2CallValue; 
            }

            var originalData = parameters.Data;
            var paddedData = new byte[originalData.Length + 420];
            Buffer.BlockCopy(originalData, 0, paddedData, 0, originalData.Length);
            parameters.Data = paddedData;

            var estimateRetryableTicketParams = new EstimateRetryableTicketFunction
            {
                Sender = parameters.From,
                Deposit = senderDeposit.Value,
                To = parameters.To,
                L2CallValue = parameters.L2CallValue.Value,
                ExcessFeeRefundAddress = parameters.ExcessFeeRefundAddress,
                CallValueRefundAddress = parameters.CallValueRefundAddress,
                Data = parameters.Data,
            };

            var nodeInterface = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    provider: _l2Provider,
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    isClassic: false);

            var gasEstimate = await nodeInterface.GetFunction<EstimateRetryableTicketFunction>().EstimateGasAsync(estimateRetryableTicketParams);

            return gasEstimate;
        }

        public async Task<BigInteger?> EstimateMaxFeePerGas(PercentIncreaseType? options = null)
        {
            var maxFeePerGasDefaults = ApplyMaxFeePerGasDefaults(options);

            var baseGasPrice = maxFeePerGasDefaults.Base ?? await _l2Provider.Eth.GasPrice.SendRequestAsync();

            var maxFeePerGas = PercentIncrease(baseGasPrice, maxFeePerGasDefaults.PercentIncrease);

            return maxFeePerGas;
        }

        public static async Task<bool> IsValid(L1ToL2MessageGasParams estimates, L1ToL2MessageGasParams reEstimates)
        {
            return await Task.Run(() =>
            {
                return estimates.MaxFeePerGas >= reEstimates.MaxFeePerGas
                    && estimates.MaxSubmissionCost >= reEstimates.MaxSubmissionCost;
            });
        }

        public async Task<L1ToL2MessageGasParams> EstimateAll(
        L1ToL2MessageNoGasParams retryableEstimateData,
        BigInteger l1BaseFee,
        Web3 l1Provider,
        GasOverrides? options = null)
        {
            options ??= new GasOverrides();

            var data = retryableEstimateData.Data;
            var gasLimitDefaults = ApplyGasLimitDefaults(options?.GasLimit!);

            // Estimate the L1 gas price
            var maxFeePerGas = await EstimateMaxFeePerGas(options?.MaxFeePerGas);

            // Estimate the submission fee
            var maxSubmissionFee = await EstimateSubmissionFee(
                l1Provider,
                l1BaseFee,
                data.Length,
                options?.MaxSubmissionFee
            );

            // Estimate the gas limit
            var calculatedGasLimit = PercentIncrease(
                gasLimitDefaults.Base ?? await EstimateRetryableTicketGasLimit(
                    retryableEstimateData,
                    options?.Deposit?.Base
                ),
                gasLimitDefaults.PercentIncrease
            );

            var gasLimit = calculatedGasLimit > gasLimitDefaults.Min ? calculatedGasLimit : gasLimitDefaults.Min;

            var deposit = options?.Deposit?.Base ?? gasLimit * maxFeePerGas + maxSubmissionFee + retryableEstimateData.L2CallValue;

            return new L1ToL2MessageGasParams
            {
                MaxFeePerGas = maxFeePerGas,
                MaxSubmissionCost = maxSubmissionFee,
                GasLimit = calculatedGasLimit,
                Deposit = deposit
            };
        }

        public async Task<PopulateFunctionParamsResult> PopulateFunctionParams(
        Func<L1ToL2MessageGasParams, CallInput> dataFunc,
        Web3 l1Provider,
        GasOverrides? gasOverrides = null)
        {
            var dummyParams = new L1ToL2MessageGasParams
            {
                GasLimit = RetryableDataTools.ErrorTriggeringParams.GasLimit,
                MaxFeePerGas = RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas,
                MaxSubmissionCost = BigInteger.One
            };

            var txRequest = dataFunc(dummyParams);

            RetryableData? retryable = null;
            try
            {
                var res = await l1Provider.Eth.Transactions.Call.SendRequestAsync(txRequest);

                retryable = RetryableDataTools.TryParseError(res);
                if (retryable == null)
                {
                    throw new ArbSdkError($"No retryable data found in error: {res}");
                }
            }
            catch (Exception ex)
            {
                if (ex is SmartContractCustomErrorRevertException rpcResponseException)
                {
                    retryable = RetryableDataTools.TryParseError(rpcResponseException.ExceptionEncodedData);
                }
                else
                {
                    retryable = RetryableDataTools.TryParseError(((RpcResponseException)ex).RpcError.Data.ToString());
                }
            }

            var baseFee = await Lib.GetBaseFee(l1Provider);
            var estimates = await EstimateAll(
                new L1ToL2MessageNoGasParams
                {
                    From = retryable?.From,
                    To = retryable?.To,
                    Data = retryable?.Data,
                    L2CallValue = retryable?.L2CallValue,
                    ExcessFeeRefundAddress = retryable?.ExcessFeeRefundAddress,
                    CallValueRefundAddress = retryable?.CallValueRefundAddress
                },
                baseFee,
                l1Provider,
                gasOverrides
            );

            // Form the real data for the transaction
            var realTxRequest = dataFunc(new L1ToL2MessageGasParams
            {
                GasLimit = estimates?.GasLimit,
                MaxFeePerGas = estimates?.MaxFeePerGas,
                MaxSubmissionCost = estimates?.MaxSubmissionCost
            });

            return new PopulateFunctionParamsResult
            {
                Estimates = estimates,
                Retryable = retryable,
                Data = realTxRequest?.Data,
                To = realTxRequest?.To,
                Value = realTxRequest.Value
            };
        }
    }
}
