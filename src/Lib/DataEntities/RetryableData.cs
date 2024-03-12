using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.FunctionEncoding;
using System.Numerics;
using System.Text.Json;
using Arbitrum.DataEntities;

namespace Arbitrum.DataEntities
{
    [FunctionOutput]
    public class RetryableData
    {
        [Parameter("address", "from", 1)]
        public string From { get; set; }

        [Parameter("address", "to", 2)]
        public string To { get; set; }

        [Parameter("uint256", "l2CallValue", 3)]
        public BigInteger L2CallValue { get; set; }

        [Parameter("uint256", "deposit", 4)]
        public BigInteger Deposit { get; set; }

        [Parameter("uint256", "maxSubmissionCost", 5)]
        public BigInteger MaxSubmissionCost { get; set; }

        [Parameter("address", "excessFeeRefundAddress", 6)]
        public string ExcessFeeRefundAddress { get; set; }

        [Parameter("address", "callValueRefundAddress", 7)]
        public string CallValueRefundAddress { get; set; }

        [Parameter("uint256", "gasLimit", 8)]
        public BigInteger GasLimit { get; set; }

        [Parameter("uint256", "maxFeePerGas", 9)]
        public BigInteger MaxFeePerGas { get; set; }

        [Parameter("bytes", "data", 10)]
        public byte[] Data { get; set; }
    }

    /**
     * Tools for parsing retryable data from errors.
     * When calling createRetryableTicket on Inbox.sol special values
     * can be passed for gasLimit and maxFeePerGas. This causes the call to revert
     * with the info needed to estimate the gas needed for a retryable ticket using
     * L1ToL2GasPriceEstimator.
     */

    public class RetryableDataTools
    {
        public static RetryableData ErrorTriggeringParams => new RetryableData
        {
            GasLimit = 1,
            MaxFeePerGas = 1
        };

        private static bool IsErrorData(object maybeErrorData)
        {
            if (maybeErrorData is { } && maybeErrorData.GetType().GetProperty("errorData") != null)
            {
                return true;
            }

            return false;
        }

        private static string TryGetErrorData(object ethersJsError)
        {
            if (IsErrorData(ethersJsError))
            {
                var errorDataProperty = ethersJsError.GetType().GetProperty("errorData");
                return errorDataProperty?.GetValue(ethersJsError)?.ToString();
            }
            else
            {
                dynamic typedError = ethersJsError;

                if (typedError.data != null)
                {
                    return typedError.data;
                }
                else if (typedError.error?.error?.body != null)
                {
                    dynamic maybeData = JsonSerializer.Deserialize<JsonElement>(
                        typedError.error?.error?.body).GetProperty("error")?.GetProperty("data")?.GetString();

                    return maybeData ?? null;
                }
                else if (typedError.error?.error?.data != null)
                {
                    return typedError.error?.error?.data;
                }
                else
                {
                    return null;
                }
            }
        }

        /**
             * Try to parse a retryable data struct from the supplied ethersjs error, or any explicitly supplied error data
             * @param ethersJsErrorOrData
             * @returns
             */
        //public static RetryableData TryParseError(object ethersJsErrorOrData)
        //{
        //    string errorData = (ethersJsErrorOrData is string)
        //        ? (string)ethersJsErrorOrData
        //        : TryGetErrorData(ethersJsErrorOrData);

        //    if (string.IsNullOrEmpty(errorData))
        //    {
        //        return null;
        //    }

        //    return ErrorInterface.ParseError(errorData).Args as RetryableData;
        //}
    }
}
