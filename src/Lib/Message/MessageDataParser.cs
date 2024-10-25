using System;
using System.Numerics;
using Arbitrum.DataEntities;
using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using System.Formats.Asn1;
using System.Text;
using Nethereum.ABI.Model;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Encoders;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;

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

            // Create an instance of the TransferFunction
            var transferFunction = new DecodeFunction();

            // Decode the input data into the TransferFunction object
            var decodedFunction = functionCallDecoder.DecodeFunctionInput<DecodeFunction>(transferFunction, "a9059cbb", eventData);

            string AddressFromBigNumber(BigInteger bn)
            {
                // Convert BigInteger to a byte array
                byte[] bytes = bn.ToByteArray();

                // Ensure the byte array is 20 bytes long (Ethereum address length)
                byte[] addressBytes = new byte[20];

                // Check if the system architecture is little-endian and reverse if necessary
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                int copyLength = Math.Min(bytes.Length, 20);

                // Copy bytes to addressBytes, ensuring it fills from the end
                Array.Copy(bytes, bytes.Length - copyLength, addressBytes, 20 - copyLength, copyLength);

                // Convert the byte array to a hexadecimal string and format it as a checksum address
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
                // Assuming eventDataString is a hexadecimal string
                int dataOffset = eventData.Length - 2 * (int)callDataLength;

                data = "0x" + eventData.Substring(dataOffset);
            }
            else
            {
                // Assuming eventDataBytes is byte array data
                byte[] dataBytes = new byte[(int)callDataLength];

                //Array.Copy(dataBytes, dataBytes.Length - callDataLength, dataBytes, 0, callDataLength);
                data = "0x" + BitConverter.ToString(dataBytes).Replace("-", string.Empty).ToLower();
            }
            var dataStartIndex = eventData.Length - (int)(callDataLength * 2);
            //var data = dataStartIndex >= 0 ? "0x" + eventData.Substring(dataStartIndex) : string.Empty;

            //var data = "0x" + eventData.Substring(eventData.Length - (int)(callDataLength * 2));

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