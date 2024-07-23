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

namespace Arbitrum.Message
{
    public static class SubmitRetryableMessageDataParser
    {
        public static RetryableMessageParams Parse(string eventData)     
        {
            //// Define the ABI types
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
            // Define the ABI types
            var abiTypes = new Parameter[]
            {
                new Parameter("uint256", 1), // dest
                new Parameter("uint256", 2), // l2 call value
                new Parameter("uint256", 3), // msg val
                new Parameter("uint256", 4), // max submission
                new Parameter("uint256", 5), // excess fee refund addr
                new Parameter("uint256", 6), // call value refund addr
                new Parameter("uint256", 7), // max gas
                new Parameter("uint256", 8), // gas price bid
                new Parameter("uint256", 9)  // data length
            };

           
            var eventDataBytes = eventData.HexToByteArray();
            string address = System.Text.Encoding.UTF8.GetString(eventDataBytes, 0, eventDataBytes.Length);

            string decodedText = new Bytes32TypeDecoder().Decode<string>(eventDataBytes);

            var type = abiTypes.FirstOrDefault().ABIType.Name;

            int decodedInt = new IntTypeDecoder().Decode<int>(eventDataBytes);

            var transaction = new Transaction();
            transaction.Input = eventData;
            


            // Initialize FunctionCallDecoder
            var decoder = new ParameterDecoder();
            // Define the parameter types
            var parameterTypes = abiTypes.Select(type => new Parameter(type.Type)).ToArray();

            // Decode the parameters
            var decodedParameters = decoder.DecodeDefaultData(eventDataBytes, abiTypes);

            var arrayTypeDecoder = ArrayType.CreateABIType("uint256[]");

            var list = arrayTypeDecoder.Decode<List<BigInteger>>(eventData);

            for ( var i = 0;i<abiTypes.Length; i++)
            {

                var decodedParameter = arrayTypeDecoder.Decode(eventDataBytes, typeof(BigInteger));

                List<dynamic> result = new List<dynamic>();
                result.Add(decodedParameter);
            }
            return new RetryableMessageParams();

            //var eventDataBytes = eventData.HexToByteArray();
            //var parameterDecoder = new ParameterDecoder();

            //var decodedValues = parameterDecoder.DecodeParameters(abiTypes, eventDataBytes);

            //var eventDataBytes = eventData.HexToByteArray();
            //// Decode the ABI-encoded data
            //var stringTypeDecoder = new AddressTypeDecoder(); 

            //var parsed = stringTypeDecoder.Decode<dynamic>(eventDataBytes); 

            //string AddressFromBigInteger(BigInteger bn)
            //{
            //    byte[] addressBytes = bn.ToByteArray();
            //    // Ensure the byte array is 20 bytes long
            //    if (addressBytes.Length < 20)
            //    {
            //        // Pad with zeros if necessary
            //        byte[] paddedAddressBytes = new byte[20];
            //        Array.Copy(addressBytes, 0, paddedAddressBytes, 20 - addressBytes.Length, addressBytes.Length);
            //        addressBytes = paddedAddressBytes;
            //    }
            //    else if (addressBytes.Length > 20)
            //    {
            //        // Ethereum address should be 20 bytes long, truncate if longer
            //        addressBytes = addressBytes[..20];
            //    }
            //    // Convert to checksum address
            //    string checksumAddress = new AddressUtil().ConvertToChecksumAddress(addressBytes);
            //    return checksumAddress;
            //}
            //var destAddress = AddressFromBigInteger((BigInteger)parsed[0]);
            //var l2CallValue = (BigInteger)parsed[1];
            //var l1Value = (BigInteger)parsed[2];
            //var maxSubmissionFee = (BigInteger)parsed[3];
            //var excessFeeRefundAddress = AddressFromBigInteger((BigInteger)parsed[4]);
            //var callValueRefundAddress = AddressFromBigInteger((BigInteger)parsed[5]);
            //var gasLimit = (BigInteger)parsed[6];
            //var maxFeePerGas = (BigInteger)parsed[7];
            //var callDataLength = (BigInteger)parsed[8];
            //var data = "0x" + eventData.Substring(eventData.Length - (int)(callDataLength * 2));
            //return new RetryableMessageParams
            //{
            //    DestAddress = destAddress,
            //    L2CallValue = l2CallValue,
            //    L1Value = l1Value,
            //    MaxSubmissionFee = maxSubmissionFee,
            //    ExcessFeeRefundAddress = excessFeeRefundAddress,
            //    CallValueRefundAddress = callValueRefundAddress,
            //    GasLimit = gasLimit,
            //    MaxFeePerGas = maxFeePerGas,
            //    Data = data.HexToByteArray()
            //};

        }
    }
}