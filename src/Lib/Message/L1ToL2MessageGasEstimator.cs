using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.ABI.Decoders;
using Nethereum.Hex.HexConvertors.Extensions;
using Arbitrum.Utils;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Hex.HexTypes;
using Org.BouncyCastle.Utilities.Encoders;
using Nethereum.RPC.Eth.DTOs;

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
        // Default amount to increase the maximum submission cost. Submission cost is calculated
        // from (call data size * some const * l1 base fee). So we need to provide some leeway for
        // base fee increase. Since submission fee is a small amount it isn't too bad for UX to increase
        // it by a large amount, and provide better safety.
        public const int DefaultSubmissionFeePercentIncrease = 300;

        // When submitting a retryable we need to estimate what the gas price for it will be when we actually come
        // to execute it. Since the l2 price can move due to congestion we should provide some padding here.
        public const int DefaultGasPricePercentIncrease = 200;

        // Properties with getter only
        public static BigInteger MaxSubmissionFeePercentIncrease { get; } = DefaultSubmissionFeePercentIncrease;
        public static BigInteger GasLimitPercentIncrease { get; } = BigInteger.Zero;
        public static BigInteger MaxFeePerGasPercentIncrease { get; } = DefaultGasPricePercentIncrease;
    }

    public class PopulateFunctionParamsResult
    {
        public L1ToL2MessageGasParams Estimates { get; set; }
        public RetryableData Retryable { get; set; }
        public byte[] Data { get; set; }
        public string To { get; set; }
        public BigInteger Value { get; set; }
    }

    public class L1ToL2MessageGasEstimator
    {
        public readonly Web3 _l2Provider;
        public L1ToL2MessageGasEstimator(Web3 l2provider)
        {
            _l2Provider = l2provider;
        }

        public BigInteger PercentIncrease(BigInteger num, BigInteger increase)
        {
            // Calculate increase percentage
            BigInteger increaseAmount = num * increase / 100;

            // Add increase amount to original number
            BigInteger result = num + increaseAmount;

            return result;
        }

        private PercentIncreaseType ApplySubmissionPriceDefaults(PercentIncreaseType maxSubmissionFeeOptions)
        {
            var defaultOptions = new PercentIncreaseType
            {
                Base = maxSubmissionFeeOptions?.Base,
                PercentIncrease = maxSubmissionFeeOptions?.PercentIncrease
                                  ?? DefaultL1ToL2MessageEstimateOptions.MaxSubmissionFeePercentIncrease
            };

            return defaultOptions;
        }

        private PercentIncreaseType ApplyMaxFeePerGasDefaults(PercentIncreaseType maxFeePerGasOptions)
        {
            return new PercentIncreaseType
            {
                Base = maxFeePerGasOptions?.Base,
                PercentIncrease = maxFeePerGasOptions?.PercentIncrease
                                ?? DefaultL1ToL2MessageEstimateOptions.MaxFeePerGasPercentIncrease
            };
        }

        private PercentIncreaseWithMin ApplyGasLimitDefaults(PercentIncreaseWithMin gasLimitDefaults)
        {
            return new PercentIncreaseWithMin
            {
                Base = gasLimitDefaults?.Base,
                PercentIncrease = gasLimitDefaults?.PercentIncrease ?? DefaultL1ToL2MessageEstimateOptions.GasLimitPercentIncrease,
                Min = gasLimitDefaults?.Min ?? BigInteger.Zero
            };
        }

        public async Task<BigInteger> EstimateSubmissionFee(
            Web3 l1Provider,
            BigInteger l1BaseFee,
            BigInteger callDataSize,
            PercentIncreaseType? options = null)
        {
            var defaultedOptions = ApplySubmissionPriceDefaults(options);
            var network = await NetworkUtils.GetL2NetworkAsync(l1Provider);
            var inbox =  await LoadContractUtils.LoadContract(
                                                contractName: "Inbox",
                                                provider: l1Provider,
                                                address: network?.EthBridge?.Inbox,
                                                isClassic: false);

            BigInteger? baseValue = defaultedOptions.Base;
            if (baseValue == null)
            {
                baseValue = await inbox.CalculateRetryableSubmissionFee(callDataSize, l1BaseFee).CallAsync<BigInteger>();
            }

            return PercentIncrease(baseValue.Value, defaultedOptions.PercentIncrease);
        }

        public async Task<BigInteger> EstimateRetryableTicketGasLimit(
        L1ToL2MessageNoGasParams parameters,
        BigInteger? senderDeposit)
        {
            if (senderDeposit == BigInteger.Zero)
            {
                // If senderDeposit is not provided or is zero, calculate a default value by converting 1 ether to Wei 
                // and adding the L2CallValue from the parameters object if it is not null. 
                senderDeposit = Web3.Convert.ToWei(1, UnitConversion.EthUnit.Ether) + parameters.L2CallValue;
            }

            var nodeInterface = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    provider: _l2Provider,
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    isClassic: false);


            var estimateGasFunction = nodeInterface.GetFunction("estimateRetryableTicket");     /////
            return await estimateGasFunction.CallAsync<BigInteger>(
                            parameters.From,
                            senderDeposit,
                            parameters.To,
                            parameters.L2CallValue,
                            parameters.ExcessFeeRefundAddress,
                            parameters.CallValueRefundAddress,
                            parameters.Data
                            ).EstimateGas();
        }

        public async Task<BigInteger> EstimateMaxFeePerGas(PercentIncreaseType? options = null)
        {
            var maxFeePerGasDefaults = ApplyMaxFeePerGasDefaults(options);

            // Estimate the L2 gas price
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
            var data = retryableEstimateData.Data;
            var gasLimitDefaults = ApplyGasLimitDefaults(options?.GasLimit);

            // Estimate the L1 gas price
            var maxFeePerGasTask = EstimateMaxFeePerGas(options?.MaxFeePerGas);

            // Estimate the submission fee
            var maxSubmissionFeeTask = EstimateSubmissionFee(
                l1Provider,
                l1BaseFee,
                new BigInteger(L1ToL2MessageUtils.ByteArrayToInt(data)),
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

            // Await all Tasks
            var maxFeePerGas = await maxFeePerGasTask;
            var maxSubmissionFee = await maxSubmissionFeeTask;

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
        Func<L1ToL2MessageGasParams, L1ToL2TransactionRequest> dataFunc,
        Web3 l1Provider,
        GasOverrides gasOverrides = null)
        {
            // Get function data that should trigger a retryable data error
            var errorTriggeringParams = RetryableDataTools.ErrorTriggeringParams;
            var txRequest = dataFunc(new L1ToL2MessageGasParams
            {
                GasLimit = errorTriggeringParams.GasLimit,
                MaxFeePerGas = errorTriggeringParams.MaxFeePerGas,
                MaxSubmissionCost = BigInteger.One
            });

            var nullData = txRequest.TxRequest?.Data;
            var to = txRequest.TxRequest.To;
            var value = txRequest.TxRequest.Value;
            var from = txRequest.TxRequest.From;

            RetryableData retryable = null;
            try
            {
                // Get retryable data from the null call
                var res = await l1Provider.Eth.Transactions.Call.SendRequestAsync(new CallInput
                {
                    To = to,
                    Data = nullData?.ToString(),
                    Value = value.ToHexBigInteger(),
                    From = from
                });

                retryable = RetryableDataTools.TryParseError(res);
                if (retryable == null)
                {
                    throw new ArbSdkError($"No retryable data found in error: {res}");
                }
            }
            catch (Exception err)
            {
                retryable = RetryableDataTools.TryParseError(err.Message);
                if (retryable == null)
                {
                    throw new ArbSdkError("No retryable data found in error", err);
                }
            }

            // Use retryable data to get gas estimates
            var baseFee = await Lib.GetBaseFee(l1Provider);
            var estimates = await EstimateAll(
                new L1ToL2MessageNoGasParams
                {
                    From = retryable.From,
                    To = retryable.To,
                    Data = retryable.Data,
                    L2CallValue = retryable.L2CallValue,
                    ExcessFeeRefundAddress = retryable.ExcessFeeRefundAddress,
                    CallValueRefundAddress = retryable.CallValueRefundAddress
                },
                baseFee,
                l1Provider,
                gasOverrides
            );

            // Form the real data for the transaction
            var realTxRequest = dataFunc(new L1ToL2MessageGasParams
            {
                GasLimit = estimates.GasLimit,
                MaxFeePerGas = estimates.MaxFeePerGas,
                MaxSubmissionCost = estimates.MaxSubmissionCost
            });

            return new PopulateFunctionParamsResult
            {
                Estimates = estimates,
                Retryable = retryable,
                Data = realTxRequest.TxRequest.Data,
                To = realTxRequest.TxRequest.To,
                Value = realTxRequest.TxRequest.Value
            };
        }
    }
}
