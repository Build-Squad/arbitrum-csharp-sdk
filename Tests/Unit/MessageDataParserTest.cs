﻿using System;
using NUnit.Framework; // or Xunit
using Nethereum.Web3;
using static Nethereum.Util.UnitConversion;
using Arbitrum.Message;
using Nethereum.Hex.HexTypes;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding;

namespace Arbitrum.Tests.Unit
{
    public class SubmitRetryableMessageDataParserTests
    {
        [Test] // Use [Fact] if you're using Xunit
        public void DoesParseL1ToL2Message()
        {
            // Arrange
            var retryableData = "0x000000000000000000000000467194771DAE2967AEF3ECBEDD3BF9A310C76C650000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000030346F1C785E00000000000000000000000000000000000000000000000000000053280CF1490000000000000000000000007F869DC59A96E798E759030B3C39398BA584F0870000000000000000000000007F869DC59A96E798E759030B3C39398BA584F08700000000000000000000000000000000000000000000000000000000000210F100000000000000000000000000000000000000000000000000000000172C586500000000000000000000000000000000000000000000000000000000000001442E567B360000000000000000000000006B175474E89094C44DA98B954EEDEAC495271D0F0000000000000000000000007F869DC59A96E798E759030B3C39398BA584F0870000000000000000000000007F869DC59A96E798E759030B3C39398BA584F08700000000000000000000000000000000000000000000003871022F1082344C7700000000000000000000000000000000000000000000000000000000000000A000000000000000000000000000000000000000000000000000000000000000800000000000000000000000000000000000000000000000000000000000000040000000000000000000000000000000000000000000000000000000000000006000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

            //This is a retryable hex code which contains the information of addresses and other things like L1Value, Gas Fees etc mentioned below in the tests
            // Act
            var res = SubmitRetryableMessageDataParser.Parse(retryableData);

            // Assert
            Assert.That(res.CallValueRefundAddress, Is.EqualTo("0x7F869dC59A96e798e759030b3c39398ba584F087"));
            Assert.That(res.Data, Is.EqualTo("0x2E567B360000000000000000000000006B175474E89094C44DA98B954EEDEAC495271D0F0000000000000000000000007F869DC59A96E798E759030B3C39398BA584F0870000000000000000000000007F869DC59A96E798E759030B3C39398BA584F08700000000000000000000000000000000000000000000003871022F1082344C7700000000000000000000000000000000000000000000000000000000000000A000000000000000000000000000000000000000000000000000000000000000800000000000000000000000000000000000000000000000000000000000000040000000000000000000000000000000000000000000000000000000000000006000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
            Assert.That(res.DestAddress, Is.EqualTo("0x467194771dAe2967Aef3ECbEDD3Bf9a310C76C65"));
            Assert.That(res.ExcessFeeRefundAddress, Is.EqualTo("0x7F869dC59A96e798e759030b3c39398ba584F087"));
            Assert.That(res.GasLimit, Is.EqualTo(BigInteger.Parse("0x0210f1".Substring(2), System.Globalization.NumberStyles.HexNumber)));
            Assert.That(res.L1Value, Is.EqualTo(BigInteger.Parse("0x30346f1c785e".Substring(2), System.Globalization.NumberStyles.HexNumber)));
            Assert.That(res.L2CallValue, Is.EqualTo(BigInteger.Parse("0", System.Globalization.NumberStyles.HexNumber)));
            Assert.That(res.MaxFeePerGas, Is.EqualTo(BigInteger.Parse("0x172c5865".Substring(2), System.Globalization.NumberStyles.HexNumber)));
            Assert.That(res.MaxSubmissionFee, Is.EqualTo(BigInteger.Parse("0x53280cf149".Substring(2), System.Globalization.NumberStyles.HexNumber)));
        }

        [Test] // Use [Fact] if you're using Xunit
        public void DoesParseEthDepositInL1ToL2Message()
        {
            // Arrange
            var retryableData = "0x000000000000000000000000F71946496600E1E1D47B8A77EB2F109FD82DC86A0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001A078F0000D790000000000000000000000000000000000000000000000000000000000370E285A0C000000000000000000000000F71946496600E1E1D47B8A77EB2F109FD82DC86A000000000000000000000000F71946496600E1E1D47B8A77EB2F109FD82DC86A000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

            // Act
            var res = SubmitRetryableMessageDataParser.Parse(retryableData);

            // Assert
            Assert.That(res.CallValueRefundAddress, Is.EqualTo("0xf71946496600e1e1d47b8A77EB2f109Fd82dc86a"));
            Assert.That(res.Data, Is.EqualTo("0x"));
            Assert.That(res.DestAddress, Is.EqualTo("0xf71946496600e1e1d47b8A77EB2f109Fd82dc86a"));
            Assert.That(res.ExcessFeeRefundAddress, Is.EqualTo("0xf71946496600e1e1d47b8A77EB2f109Fd82dc86a"));
            Assert.That((int)res.GasLimit, Is.EqualTo(0));
            Assert.That(res.L1Value, Is.EqualTo(Web3.Convert.ToWei(30.01, EthUnit.Ether)));
            Assert.That((int)res.L2CallValue, Is.EqualTo(0));
            Assert.That((int)res.MaxFeePerGas, Is.EqualTo(0));
            BigInteger intValue = BigInteger.Parse("0x370e285a0c".Substring(2), System.Globalization.NumberStyles.HexNumber);

            Assert.That(res.MaxSubmissionFee, Is.EqualTo(intValue));
        }

        [Test]
        public void DecodeFunctionInput()
        {
            var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_INFURA_PROJECT_ID");

            // Input data that you want to decode
            string inputData = "0xa9059cbb000000000000000000000000bBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB0000000000000000000000000000000000000000000000000000000000000001";

            // The function signature (first 4 bytes of the keccak256 hash of the function)
            string functionSignature = "a9059cbb"; // transfer(address,uint256)
            var functionCallDecoder = new FunctionCallDecoder();

            // Create an instance of the TransferFunction
            var transferFunction = new TransferFunction();

            // Decode the input data into the TransferFunction object
            var decodedFunction = functionCallDecoder.DecodeFunctionInput<TransferFunction>(transferFunction, functionSignature, inputData);


            Console.WriteLine($"To: {decodedFunction.To}");
            Console.WriteLine($"Value: {decodedFunction.Value}");
        }

        // Define the Transfer function
        public class TransferFunction : FunctionMessage
        {
            [Parameter("address", "to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "value", 2)]
            public BigInteger Value { get; set; }
        }
    }
}
