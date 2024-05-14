using System;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Arbitrum.Message.Tests.Unit
{
    [TestFixture]
    public class L1toL2MessageTests
    {
        [Test]
        public void TestNitroEvents()
        {
            // Receipt from mainnet tx: 0x00000a61331187be51ab9ae792d74f601a5a21fb112f5b9ac5bccb23d4d5aaba
            var receipt = new TransactionReceipt
            {
                To = "0x72Ce9c846789fdB6fC1f34aC4AD25Dd9ef7031ef",
                From = "0xa2e06c19EE14255889f0Ec0cA37f6D0778D06754",
                ContractAddress = "",
                TransactionIndex = 323.ToHexBigInteger(),
                GasUsed = 0.ToHexBigInteger(),
                LogsBloom = "0x0400010000000000...", // Truncated for brevity
                BlockHash = "0xe5b6457bc2ec1bb39a88cee7f294ea3ad41b76d1069fd2e69c5959b4ffd6dd56",
                TransactionHash = "0x00000a61331187be51ab9ae792d74f601a5a21fb112f5b9ac5bccb23d4d5aaba",
                Logs = EventExtensions.ConvertToJArray(
                    new FilterLog[]
                    { 
                        new FilterLog
                        {
                            TransactionIndex = 323.ToHexBigInteger(),
                            BlockNumber = 15500657.ToHexBigInteger(),
                            TransactionHash = "0x00000a61331187be51ab9ae792d74f601a5a21fb112f5b9ac5bccb23d4d5aaba",
                            Address = "0x72Ce9c846789fdB6fC1f34aC4AD25Dd9ef7031ef",
                            Topics = new[]
                            {
                                "0x85291dff2161a93c2f12c819d31889c96c63042116f5bc5a205aa701c2c429f5",
                                "0x000000000000000000000000c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
                                "0x000000000000000000000000a2e06c19ee14255889f0ec0ca37f6d0778d06754",
                                "0x000000000000000000000000a2e06c19ee14255889f0ec0ca37f6d0778d06754",
                            },
                            Data = "0x000000000000000000000000d92023e9d9911199a6711321d1277285e6d4e2db",
                            LogIndex = 443.ToHexBigInteger(),
                            BlockHash = "0xe5b6457bc2ec1bb39a88cee7f294ea3ad41b76d1069fd2e69c5959b4ffd6dd56",
                            Removed = false,
                        }
                    }
                ),
                CumulativeGasUsed = 0.ToHexBigInteger(),
                BlockNumber = 15500657.ToHexBigInteger(),
                EffectiveGasPrice = 0.ToHexBigInteger(),
                Type = 2.ToHexBigInteger(),
                Status = 1.ToHexBigInteger(),
            };

            var arbProvider = new SignerOrProvider(new Web3("https://arb1.arbitrum.io/rpc"));
            var l1TxnReceipt = new L1TransactionReceipt(receipt);

            Assert.That(async () =>
            {
                await l1TxnReceipt.GetL1ToL2MessagesClassic(arbProvider.Provider);
            }, Throws.Exception.TypeOf<Exception>().With.Message.EqualTo(
                "This method is only for classic transactions. Use 'getL1ToL2Messages' for nitro transactions."));

            Assert.That(async () =>
            {
                await l1TxnReceipt.GetL1ToL2Messages(arbProvider);
            }, Throws.Nothing);

            // Your assertions here
        }

        [Test]
        public void TestClassicEvents()
        {
            // Receipt from mainnet tx: 0xc80e0c4844bb502ed7d7e2db6f9e6b2d52e3d25688de216394f0227fc5dc814e
            var receipt = new TransactionReceipt
            {
                To = "0x4Dbd4fc535Ac27206064B68FfCf827b0A60BAB3f",
                From = "0xA928c403Af1993eB309451a3559F8946E7d81F7F",
                ContractAddress = "",
                TransactionIndex = 0.ToHexBigInteger(),
                GasUsed = 0.ToHexBigInteger(),
                LogsBloom = "0x0000000000000000...", // Truncated for brevity
                BlockHash = "0x8c6512df3bc8d8422c9a345ee2c1e822f1538e0a00f1a02ac1a366ef57c7e561",
                TransactionHash = "0xc80e0c4844bb502ed7d7e2db6f9e6b2d52e3d25688de216394f0227fc5dc814e",
                Logs = EventExtensions.ConvertToJArray(
                new FilterLog[]
                {
                    new FilterLog
                    {
                        TransactionIndex = 0.ToHexBigInteger(),
                        BlockNumber = 14925720.ToHexBigInteger(),
                        TransactionHash = "0xc80e0c4844bb502ed7d7e2db6f9e6b2d52e3d25688de216394f0227fc5dc814e",
                        Address = "0x4Dbd4fc535Ac27206064B68FfCf827b0A60BAB3f",
                        Topics = new[]
                        {
                            "0x2234b61c232f50328e929b52ac20e3a2d0a9d040d02fb5ec87e0d7a0f7b1e8f7",
                            "0x0000000000000000000000000000000000000000000000000000000000000000",
                            "0x000000000000000000000000a928c403af1993eb309451a3559f8946e7d81f7f",
                        },
                        Data = "0x0000000000000000000000000000000000000000000000a968163f0a57b40000",
                        LogIndex = 0.ToHexBigInteger(),
                        BlockHash = "0x8c6512df3bc8d8422c9a345ee2c1e822f1538e0a00f1a02ac1a366ef57c7e561",
                        Removed = false,
                    }
                }),
                CumulativeGasUsed = 0.ToHexBigInteger(),
                BlockNumber = 14925720.ToHexBigInteger(),
                EffectiveGasPrice = 0.ToHexBigInteger(),
                Type = 2.ToHexBigInteger(),
                Status = 1.ToHexBigInteger(),
            };

            var arbProvider = new SignerOrProvider(new Web3("https://arb1.arbitrum.io/rpc"));
            var l1TxnReceipt = new L1TransactionReceipt(receipt);

            Assert.That(async () =>
            {
                await l1TxnReceipt.GetL1ToL2Messages(arbProvider);
            }, Throws.Exception.TypeOf<Exception>().With.Message.EqualTo(
                "This method is only for nitro transactions. Use 'getL1ToL2MessagesClassic' for classic transactions."));

            Assert.That(async () =>
            {
                await l1TxnReceipt.GetL1ToL2MessagesClassic(arbProvider.Provider);
            }, Throws.Nothing);

            // Your assertions here
        }
    }
}
