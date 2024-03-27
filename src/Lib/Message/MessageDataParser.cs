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

namespace Arbitrum.Message
{
    public class SubmitRetryableMessageDataParser
    {
        public static RetryableMessageParams Parse(string eventData)     ////////
        {
            var eventDataBytes = eventData.HexToByteArray();

            // Decode the ABI-encoded data
            var stringTypeDecoder = new StringTypeDecoder();
            var parsed = stringTypeDecoder.Decode(eventDataBytes);

            // Define the ABI types
            //var abiTypes = new[] 
            // {
            //    "uint256", // dest
            //    "uint256", // l2 call value
            //    "uint256", // msg val
            //    "uint256", // max submission
            //    "uint256", // excess fee refund addr
            //    "uint256", // call value refund addr
            //    "uint256", // max gas
            //    "uint256", // gas price bid
            //    "uint256"  // data length
            //};

            string AddressFromBigInteger(BigInteger bn)
            {
                byte[] addressBytes = bn.ToByteArray();

                // Ensure the byte array is 20 bytes long
                if (addressBytes.Length < 20)
                {
                    // Pad with zeros if necessary
                    byte[] paddedAddressBytes = new byte[20];
                    Array.Copy(addressBytes, 0, paddedAddressBytes, 20 - addressBytes.Length, addressBytes.Length);
                    addressBytes = paddedAddressBytes;
                }
                else if (addressBytes.Length > 20)
                {
                    // Ethereum address should be 20 bytes long, truncate if longer
                    addressBytes = addressBytes[..20];
                }

                // Convert to checksum address
                string checksumAddress = new AddressUtil().ConvertToChecksumAddress(addressBytes);
                return checksumAddress;
            }

            var destAddress = AddressFromBigInteger((BigInteger)parsed[0]);
            var l2CallValue = (BigInteger)parsed[1];
            var l1Value = (BigInteger)parsed[2];
            var maxSubmissionFee = (BigInteger)parsed[3];
            var excessFeeRefundAddress = AddressFromBigInteger((BigInteger)parsed[4]);
            var callValueRefundAddress = AddressFromBigInteger((BigInteger)parsed[5]);
            var gasLimit = (BigInteger)parsed[6];
            var maxFeePerGas = (BigInteger)parsed[7];
            var callDataLength = (BigInteger)parsed[8];
            var data = "0x" + eventData.Substring(eventData.Length - (int)(callDataLength * 2));

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
