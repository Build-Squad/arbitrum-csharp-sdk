using Nethereum.ABI.Decoders;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;

namespace Arbitrum.src.Lib.DataEntities
{
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
        public bool? CcipReadEnabled { get; set; }
    }

    public class RetryableData
    {
        public string From { get; set; }
        public string To { get; set; }
        public BigInteger? L2CallValue { get; set; }
        public BigInteger? Value { get; set; }
        public BigInteger? MaxSubmissionCost { get; set; }
        public string ExcessFeeRefundAddress { get; set; }
        public string CallValueRefundAddress { get; set; }
        public BigInteger? GasLimit { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }
        public byte[]? Data { get; set; }
    }

    public static class RetryableDataTools
    {
        public static RetryableData ErrorTriggeringParams => new RetryableData
        {
            GasLimit = BigInteger.One,
            MaxFeePerGas = BigInteger.One,
        };

        public static RetryableData? TryParseError(string errorData)
        {
            try
            {
                errorData = errorData.StartsWith("0x") ? errorData[10..] : errorData[8..];

                var _addressDecoder = new AddressTypeDecoder();
                var _intDecoder = new IntTypeDecoder(true);
                var _bytesDecoder = new BytesTypeDecoder();

                var encodedData = errorData.HexToByteArray();

                var retryableData = new RetryableData();
                int offset = 0;
                int chunkSize = 32; // Standard size for most encoded ABI types

                // Decoding "From" (address type)
                retryableData.From = (string)_addressDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(string));
                offset += chunkSize;

                // Decoding "To" (address type)
                retryableData.To = (string)_addressDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(string));
                offset += chunkSize;

                // Decoding "L2CallValue" (uint256)
                retryableData.L2CallValue = (BigInteger?)_intDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(BigInteger));
                offset += chunkSize;

                // Decoding "Value" (uint256)
                retryableData.Value = (BigInteger?)_intDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(BigInteger));
                offset += chunkSize;

                // Decoding "MaxSubmissionCost" (uint256)
                retryableData.MaxSubmissionCost = (BigInteger?)_intDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(BigInteger));
                offset += chunkSize;

                // Decoding "ExcessFeeRefundAddress" (address type)
                retryableData.ExcessFeeRefundAddress = (string)_addressDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(string));
                offset += chunkSize;

                // Decoding "CallValueRefundAddress" (address type)
                retryableData.CallValueRefundAddress = (string)_addressDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(string));
                offset += chunkSize;

                // Decoding "GasLimit" (uint256)
                retryableData.GasLimit = (BigInteger?)_intDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(BigInteger));
                offset += chunkSize;

                // Decoding "MaxFeePerGas" (uint256)
                retryableData.MaxFeePerGas = (BigInteger?)_intDecoder.Decode(encodedData.Skip(offset).Take(chunkSize).ToArray(), typeof(BigInteger));
                offset += chunkSize;

                // Decoding "Data" (bytes type) (can have variable length)
                int dataLength = BitConverter.ToInt32(encodedData.Skip(offset).Take(32).ToArray().Reverse().ToArray(), 0);
                retryableData.Data = (byte[]?)_bytesDecoder.Decode(encodedData.Skip(offset + 32).Take(dataLength).ToArray(), typeof(byte[]));

                return retryableData;
            }
            catch (Exception ex)
            {
                throw new Exception("Parsing error", ex.InnerException);
            }
        }
    }
}