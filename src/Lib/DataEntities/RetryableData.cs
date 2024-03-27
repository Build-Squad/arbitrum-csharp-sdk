using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.FunctionEncoding;
using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.Decoders;
using Arbitrum.DataEntities;
using Nethereum.RPC.Eth.DTOs;
using Arbitrum.Utils;
using Nethereum.Util;
using System.Formats.Asn1;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI;

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

    // Define the params type for the CreateRetryableTicket method
    public class CreateRetryableTicketParams
    {
        public L1ToL2MessageParams L1ToL2MessageParams { get; set; }
        public L1ToL2MessageGasParams L1ToL2MessageGasParams { get; set; }
        public PayableOverrides Overrides { get; set; }
    }

    public class PayableOverrides : Overrides
    {
        public BigInteger? Value { get; set; }
    }

    public class Overrides
    {
        public BigInteger? GasLimit { get; set; }
        public BigInteger? GasPrice { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }
        public BigInteger? MaxPriorityFeePerGas { get; set; }
        public BigInteger? Nonce { get; set; }
        public int? Type { get; set; }
        public List<AccessList>? AccessList { get; set; }
        //public Record<string, object> CustomData { get; set; }
        public bool? CcipReadEnabled { get; set; }
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

        public static RetryableData TryParseError(string errorDataHex)
        {
            try
            {
                if (errorDataHex.StartsWith("0x"))
                {
                    errorDataHex = errorDataHex.Substring(2);
                }
                errorDataHex = errorDataHex.Substring(8);

                var decodedData = ABIDecoder.Current.Decode<BigInteger[]>(errorDataHex.HexToByteArray(), RetryableData.AbiTypes);

                if (decodedData.Length != RetryableData.AbiTypes.Length)
                {
                    return null;
                }
                else
                {
                    return new RetryableData
                    {
                        From = AddressUtil.Current.ConvertToChecksumAddress(decodedData[0].ToHex()),
                        To = AddressUtil.Current.ConvertToChecksumAddress(decodedData[1].ToHex()),
                        L2CallValue = decodedData[2],
                        Deposit = decodedData[3],
                        MaxSubmissionCost = decodedData[4],
                        ExcessFeeRefundAddress = AddressUtil.Current.ConvertToChecksumAddress(decodedData[5].ToHex()),
                        CallValueRefundAddress = AddressUtil.Current.ConvertToChecksumAddress(decodedData[6].ToHex()),
                        GasLimit = decodedData[7],
                        MaxFeePerGas = decodedData[8],
                        Data = decodedData[9]
                    };
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
