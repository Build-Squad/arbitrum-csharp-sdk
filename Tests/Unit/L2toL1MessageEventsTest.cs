using NUnit.Framework;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Contracts;
using Moq;
using System.Collections.Generic;
using System.Numerics;
using Arbitrum.Message;
using Arbitrum.DataEntities;
using Nethereum.Hex.HexTypes;

namespace Arbitrum.Message.Tests.Unit
{

    [TestFixture]
    public class L2ToL1MessageTests
    {
        private const string classicTopic = "0x5baaa87db386365b5c161be377bc3d8e317e8d98d71a3ca7ed7d555340c8f767";
        private const string nitroTopic = "0x3e7aafa77dbf186b7fd488006beff893744caa3c4f6f299e8a709fa2087374fc";
        private const string arbSys = "0x0000000000000000000000000000000000000064";

        public interface IWeb3Wrapper : IWeb3
        {
            Task<HexBigInteger> GetBlockNumberAsync(CancellationToken cancellationToken);
            Task<string> GetVersionAsync(CancellationToken cancellationToken);
            Task<FilterLog[]> GetLogsAsync(NewFilterInput filterInput, CancellationToken cancellationToken);
        }

        public class Web3Wrapper : Web3, IWeb3Wrapper
        {
            public Web3Wrapper(string url) : base(url) { }


            public Task<HexBigInteger> GetBlockNumberAsync(CancellationToken cancellationToken)
            {
                return Eth.Blocks.GetBlockNumber.SendRequestAsync(cancellationToken);
            }

            public Task<string> GetVersionAsync(CancellationToken cancellationToken)
            {
                return Net.Version.SendRequestAsync(cancellationToken);
            }

            public Task<FilterLog[]> GetLogsAsync(NewFilterInput filterInput, CancellationToken cancellationToken)
            {
                return Eth.Filters.GetLogs.SendRequestAsync(filterInput, cancellationToken);
            }
        }

        private async Task<(Mock<IWeb3Wrapper>, L2Network, BigInteger)> CreateProviderMockAsync(int? networkChoiceOverride = null)
        {
            var l2Network = await NetworkUtils.GetL2NetworkAsync(networkChoiceOverride ?? 42161);

            var l2ProviderMock = new Mock<IWeb3Wrapper>();
            var latestBlock = l2Network.NitroGenesisBlock + 1000;

            l2ProviderMock.Setup(p => p.GetBlockNumberAsync(CancellationToken.None))
                          .ReturnsAsync(new HexBigInteger(latestBlock));

            l2ProviderMock.Setup(p => p.GetVersionAsync(CancellationToken.None))
                          .ReturnsAsync(l2Network.ChainID.ToString());

            l2ProviderMock.Setup(p => p.GetLogsAsync(It.IsAny<NewFilterInput>(), CancellationToken.None))
                          .ReturnsAsync(new FilterLog[0]);

            return (l2ProviderMock, l2Network, latestBlock);
        }

        [Test]
        public async Task DoesCallForClassicEvents()
        {
            var (l2ProviderMock, l2Network, _) = await CreateProviderMockAsync();

            var fromBlock = new BlockParameter(0);
            var toBlock = new BlockParameter(1000);

            var filter = new NewFilterInput()
            {
                FromBlock = fromBlock,
                ToBlock = toBlock
            };

            NewFilterInput capturedInput = null;

            // Inside the CreateProviderMockAsync method
            l2ProviderMock
                .Setup(p => p.GetLogsAsync(
                    It.IsAny<NewFilterInput>(),
                    default // Explicitly provide the default value for CancellationToken
                ))
                .Callback<NewFilterInput, CancellationToken>((input, cancellationToken) => capturedInput = input)
                .ReturnsAsync(new FilterLog[0]); // Corrected usage of ReturnsAsync

            await L2ToL1Message.GetL2ToL1Events(l2ProviderMock.Object, filter);

            // Asserting that the captured input matches the expected values
            Assert.That(capturedInput, Is.Not.Null);
            Assert.That(capturedInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedInput.Topics, Is.EqualTo(new[] { classicTopic }));
            Assert.That(capturedInput.FromBlock, Is.EqualTo(fromBlock));
            Assert.That(capturedInput.ToBlock, Is.EqualTo(toBlock));
        }



        [Test]
        public async Task DoesCallForNitroEvents()
        {
            var (l2ProviderMock, l2Network, _) = await CreateProviderMockAsync();

            var fromBlock = new BlockParameter(l2Network.NitroGenesisBlock.ToHexBigInteger());
            var toBlock = new BlockParameter((l2Network.NitroGenesisBlock + 500).ToHexBigInteger());

            var filter = new NewFilterInput()
            {
                FromBlock = fromBlock,
                ToBlock = toBlock
            };

            NewFilterInput capturedInput = null;

            // Inside the CreateProviderMockAsync method
            l2ProviderMock
                .Setup(p => p.GetLogsAsync(
                    It.IsAny<NewFilterInput>(),
                    default // Explicitly provide the default value for CancellationToken
                ))
                .Callback<NewFilterInput, CancellationToken>((input, cancellationToken) => capturedInput = input)
                .ReturnsAsync(new FilterLog[0]); // Corrected usage of ReturnsAsync

            await L2ToL1Message.GetL2ToL1Events(l2ProviderMock.Object, filter);

            // Asserting that the captured input matches the expected values
            Assert.That(capturedInput, Is.Not.Null);
            Assert.That(capturedInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedInput.Topics, Is.EqualTo(new[] { nitroTopic }));
            Assert.That(capturedInput.FromBlock, Is.EqualTo(fromBlock));
            Assert.That(capturedInput.ToBlock, Is.EqualTo(toBlock));
        }


        [Test]
        public async Task DoesCallForClassicAndNitroEvents()
        {
            var (l2ProviderMock, l2Network, _) = await CreateProviderMockAsync();

            var classicFromBlock = new BlockParameter(0);
            var classicToBlock = new BlockParameter(l2Network.NitroGenesisBlock.ToHexBigInteger());
            var nitroFromBlock = new BlockParameter(l2Network.NitroGenesisBlock.ToHexBigInteger());
            var nitroToBlock = new BlockParameter((l2Network.NitroGenesisBlock + 500).ToHexBigInteger());

            var filter = new NewFilterInput()
            {
                FromBlock = classicFromBlock,
                ToBlock = nitroToBlock
            };

            NewFilterInput capturedClassicInput = null;
            NewFilterInput capturedNitroInput = null;

            // Inside the CreateProviderMockAsync method
            l2ProviderMock
                .Setup(p => p.GetLogsAsync(
                    It.IsAny<NewFilterInput>(),
                    default // Explicitly provide the default value for CancellationToken
                ))
                .Callback<NewFilterInput, CancellationToken>((input, cancellationToken) =>
                {
                    if (input.Topics[0] == classicTopic)
                    {
                        capturedClassicInput = input;
                    }
                    else if (input.Topics[0] == nitroTopic)
                    {
                        capturedNitroInput = input;
                    }
                })
                .ReturnsAsync(new FilterLog[0]); // Corrected usage of ReturnsAsync

            await L2ToL1Message.GetL2ToL1Events(l2ProviderMock.Object, filter);

            // Asserting that the captured inputs match the expected values
            Assert.That(capturedClassicInput, Is.Not.Null);
            Assert.That(capturedClassicInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedClassicInput.Topics, Is.EqualTo(new[] { classicTopic }));
            Assert.That(capturedClassicInput.FromBlock, Is.EqualTo(classicFromBlock));
            Assert.That(capturedClassicInput.ToBlock, Is.EqualTo(classicToBlock));

            Assert.That(capturedNitroInput, Is.Not.Null);
            Assert.That(capturedNitroInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedNitroInput.Topics, Is.EqualTo(new[] { nitroTopic }));
            Assert.That(capturedNitroInput.FromBlock, Is.EqualTo(nitroFromBlock));
            Assert.That(capturedNitroInput.ToBlock, Is.EqualTo(nitroToBlock));
        }


        [Test]
        public async Task DoesCallForClassicAndNitroEventsFromEarliestToLatest()
        {
            var (l2ProviderMock, l2Network, _) = await CreateProviderMockAsync();

            var classicToBlock = new BlockParameter(l2Network.NitroGenesisBlock.ToHexBigInteger());
            var nitroFromBlock = new BlockParameter(l2Network.NitroGenesisBlock.ToHexBigInteger());

            var filter = new NewFilterInput()
            {
                FromBlock = BlockParameter.CreateLatest(),
                ToBlock = BlockParameter.CreateLatest()
            };

            NewFilterInput capturedClassicInput = null;
            NewFilterInput capturedNitroInput = null;

            // Inside the CreateProviderMockAsync method
            l2ProviderMock
                .Setup(p => p.GetLogsAsync(
                    It.IsAny<NewFilterInput>(),
                    default // Explicitly provide the default value for CancellationToken
                ))
                .Callback<NewFilterInput, CancellationToken>((input, cancellationToken) =>
                {
                    if (input.Topics[0] == classicTopic)
                    {
                        capturedClassicInput = input;
                    }
                    else if (input.Topics[0] == nitroTopic)
                    {
                        capturedNitroInput = input;
                    }
                })
                .ReturnsAsync(new FilterLog[0]); // Corrected usage of ReturnsAsync

            await L2ToL1Message.GetL2ToL1Events(l2ProviderMock.Object, filter);

            // Asserting that the captured inputs match the expected values
            Assert.That(capturedClassicInput, Is.Not.Null);
            Assert.That(capturedClassicInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedClassicInput.Topics, Is.EqualTo(new[] { classicTopic }));
            Assert.That(capturedClassicInput.FromBlock, Is.EqualTo(new BlockParameter(0)));
            Assert.That(capturedClassicInput.ToBlock, Is.EqualTo(classicToBlock));

            Assert.That(capturedNitroInput, Is.Not.Null);
            Assert.That(capturedNitroInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedNitroInput.Topics, Is.EqualTo(new[] { nitroTopic }));
            Assert.That(capturedNitroInput.FromBlock, Is.EqualTo(nitroFromBlock));
            Assert.That(capturedNitroInput.ToBlock, Is.EqualTo(BlockParameter.CreateLatest()));
        }


        [Test]
        public async Task DoesCallForOnlyNitroForLatest()
        {
            var (l2ProviderMock, l2Network, _) = await CreateProviderMockAsync();

            var fromBlock = new BlockParameter((l2Network.NitroGenesisBlock + 2).ToHexBigInteger());
            var toBlock = BlockParameter.CreateLatest();

            var filter = new NewFilterInput()
            {
                FromBlock = fromBlock,
                ToBlock = toBlock
            };

            NewFilterInput capturedInput = null;

            // Inside the CreateProviderMockAsync method
            l2ProviderMock
                .Setup(p => p.GetLogsAsync(
                    It.IsAny<NewFilterInput>(),
                    default // Explicitly provide the default value for CancellationToken
                ))
                .Callback<NewFilterInput, CancellationToken>((input, cancellationToken) => capturedInput = input)
                .ReturnsAsync(new FilterLog[0]); // Corrected usage of ReturnsAsync

            await L2ToL1Message.GetL2ToL1Events(l2ProviderMock.Object, filter);

            // Asserting that the captured input matches the expected values
            Assert.That(capturedInput, Is.Not.Null);
            Assert.That(capturedInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedInput.Topics, Is.EqualTo(new[] { nitroTopic }));
            Assert.That(capturedInput.FromBlock, Is.EqualTo(fromBlock));
            Assert.That(capturedInput.ToBlock, Is.EqualTo(toBlock));
        }


        [Test]
        public async Task DoesntCallClassicWhenNitroGenesisIs0()
        {
            var (l2ProviderMock, l2Network, _) = await CreateProviderMockAsync(421614);

            var fromBlock = BlockParameter.CreateEarliest();
            var toBlock = BlockParameter.CreateLatest();

            var filter = new NewFilterInput()
            {
                FromBlock = fromBlock,
                ToBlock = toBlock
            };

            NewFilterInput capturedInput = null;

            // Inside the CreateProviderMockAsync method
            l2ProviderMock
                .Setup(p => p.GetLogsAsync(
                    It.IsAny<NewFilterInput>(),
                    default // Explicitly provide the default value for CancellationToken
                ))
                .Callback<NewFilterInput, CancellationToken>((input, cancellationToken) => capturedInput = input)
                .ReturnsAsync(new FilterLog[0]); // Corrected usage of ReturnsAsync

            await L2ToL1Message.GetL2ToL1Events(l2ProviderMock.Object, filter);

            // Asserting that the captured input matches the expected values
            Assert.That(capturedInput, Is.Not.Null);
            Assert.That(capturedInput.Address, Is.EqualTo(new[] { arbSys }));
            Assert.That(capturedInput.Topics, Is.EqualTo(new[] { nitroTopic }));
            Assert.That(capturedInput.FromBlock, Is.EqualTo(fromBlock));
            Assert.That(capturedInput.ToBlock, Is.EqualTo(toBlock));
        }
    }
}