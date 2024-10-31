using Arbitrum.DataEntities;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Numerics;

namespace Arbitrum.Message
{
    [Function("transfer", "bool")]
    public class DecodeFunction : FunctionMessage
    {
        [Parameter("uint256", "test", 1)]
        public BigInteger Dest { get; set; }

        [Parameter("uint256", "l2CallValue", 2)]
        public BigInteger L2CallValue { get; set; }

        [Parameter("uint256", "msgVal", 3)]
        public BigInteger MsgVal { get; set; }

        [Parameter("uint256", "maxSubmission", 4)]
        public BigInteger MaxSubmission { get; set; }

        [Parameter("uint256", "excessFeeRefundAddr", 5)]
        public BigInteger ExcessFeeRefundAddr { get; set; }

        [Parameter("uint", "callValueRefundAddr", 6)]
        public BigInteger CallValueRefundAddr { get; set; }

        [Parameter("uint256", "maxGas", 7)]
        public BigInteger MaxGas { get; set; }

        [Parameter("uint256", "gasPriceBid", 8)]
        public BigInteger GasPriceBid { get; set; }

        [Parameter("uint256", "dataLength", 9)]
        public BigInteger DataLength { get; set; }
    }

    public static class SubmitRetryableMessageDataParser
    {
        public static RetryableMessageParams Parse(string eventData)
        {
            var parsed = new DecodeFunction().DecodeInput(eventData);

            var functionCallDecoder = new FunctionCallDecoder();

            var transferFunction = new DecodeFunction();

            var decodedFunction = functionCallDecoder.DecodeFunctionInput(transferFunction, "a9059cbb", eventData);

            string AddressFromBigNumber(BigInteger bn)
            {
                byte[] bytes = bn.ToByteArray();

                byte[] addressBytes = new byte[20];

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                int copyLength = Math.Min(bytes.Length, 20);

                Array.Copy(bytes, bytes.Length - copyLength, addressBytes, 20 - copyLength, copyLength);

                return new AddressUtil().ConvertToChecksumAddress(addressBytes.ToHex());
            }

            var destAddress = AddressFromBigNumber(decodedFunction.Dest);
            var l2CallValue = decodedFunction.L2CallValue;
            var l1Value = decodedFunction.MsgVal;
            var maxSubmissionFee = decodedFunction.MaxSubmission;
            var excessFeeRefundAddress = AddressFromBigNumber(decodedFunction.ExcessFeeRefundAddr);
            var callValueRefundAddress = AddressFromBigNumber(decodedFunction.CallValueRefundAddr);
            var gasLimit = decodedFunction.MaxGas;
            var maxFeePerGas = decodedFunction.GasPriceBid;
            var callDataLength = decodedFunction.DataLength;

            string data;
            if (eventData.StartsWith("0x"))
            {
                int dataOffset = eventData.Length - 2 * (int)callDataLength;

                data = string.Concat("0x", eventData.AsSpan(dataOffset));
            }
            else
            {
                byte[] dataBytes = new byte[(int)callDataLength];

                data = "0x" + BitConverter.ToString(dataBytes).Replace("-", string.Empty).ToLower();
            }

            var dataStartIndex = eventData.Length - (int)(callDataLength * 2);
            data = dataStartIndex >= 0 ? string.Concat("0x", eventData.AsSpan(dataStartIndex)) : string.Empty;

            return new RetryableMessageParams
            {
                DestAddress = destAddress,
                L2CallValue = l2CallValue,
                L1Value = l1Value,
                MaxSubmissionFee = maxSubmissionFee,
                ExcessFeeRefundAddress = excessFeeRefundAddress,
                CallValueRefundAddress = callValueRefundAddress,
                GasLimit = gasLimit,
                MaxFeePerGas = maxFeePerGas,
                Data = data
            };

        }
    }
}