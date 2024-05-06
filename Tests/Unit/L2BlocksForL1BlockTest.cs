using NUnit.Framework;
using System.Threading.Tasks;
using Arbitrum.Message;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;

namespace Arbitrum.Message.Tests.Unit
{
    [TestFixture]
    public class L2BlocksLookupTests
    {
        private readonly IClient provider = new Web3("https://arb1.arbitrum.io/rpc").Client;
        private readonly ArbitrumProvider arbProvider;

        public L2BlocksLookupTests()
        {
            arbProvider = new ArbitrumProvider(provider);
        }

        [Test]
        public async Task SuccessfullySearchesForL2BlockRange()
        {
            var l2Blocks = await Lib.GetBlockRangesForL1Block(provider: arbProvider.Client,forL1Block: 17926532, minL2Block: 121800000,maxL2Block: 122000000);
            Assert.That(l2Blocks.Length, Is.EqualTo(2));
            await ValidateL2Blocks(l2Blocks, 2);
        }

        [Test]
        public async Task FailsToSearchForL2BlockRange()
        {
            var l2Blocks = await Lib.GetBlockRangesForL1Block(provider: arbProvider.Client, forL1Block: 17926533, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Blocks.Length, Is.EqualTo(2));
            foreach (var block in l2Blocks)
            {
                Assert.That(block, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task SuccessfullySearchesForFirstL2Block()
        {
            var l2Block = await Lib.GetFirstBlockForL1Block(provider: arbProvider.Client, forL1Block: 17926532, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Block, Is.Not.Null);
            await ValidateL2Blocks(new int[] { l2Block }, 1);
        }

        [Test]
        public async Task FailsToSearchForFirstL2BlockWithoutAllowGreaterFlag()
        {
            var l2Block = await Lib.GetFirstBlockForL1Block(provider: arbProvider.Client, forL1Block: 17926533, allowGreater: false, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Block, Is.Null);
        }

        [Test]
        public async Task SuccessfullySearchesForFirstL2BlockWithAllowGreaterFlag()
        {
            var l2Block = await Lib.GetFirstBlockForL1Block(provider: arbProvider.Client, forL1Block: 17926533, allowGreater: true, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Block, Is.Not.Null);
            await ValidateL2Blocks(new int[] { l2Block }, 1);
        }

        public async Task ValidateL2Blocks(int[] l2Blocks, int l2BlocksCount, string type = "int32")
        {
            if (l2Blocks.Length != l2BlocksCount)
            {
                throw new ArgumentException($"Expected L2 block range to have the array length of {l2BlocksCount}, got {l2Blocks.Length}.");
            }


            if (l2Blocks.Any(block => block == 0 ? type != "undefined" : block.GetType().Name.ToLower() != type))
            {
                throw new ArgumentException($"Expected all blocks to be {type}.");
            }

            if (type == "undefined")
            {
                return;
            }

            var arbProvider = new ArbitrumProvider(new RpcClient(new Uri("https://arb1.arbitrum.io/rpc")));
            var promises = l2Blocks.Select(async (l2Block, index) =>
            {
                if (l2Block == null)
                {
                    throw new ArgumentNullException("L2 block is undefined.");
                }

                var isStartBlock = index == 0;
                var blockNumber = l2Block;
                var block = await arbProvider.GetBlock(blockNumber);
                var nextBlockNumber = isStartBlock ? blockNumber - 1 : blockNumber + 1;
                var nextBlock = await arbProvider.GetBlock(nextBlockNumber);

                return (block, nextBlock);
            });

            var result = await Task.WhenAll(promises);

            int startBlock, blockBeforeStartBlock, endBlock, blockAfterEndBlock;
            try
            {
                startBlock = result[0].block.L1BlockNumber;
                blockBeforeStartBlock = result[0].nextBlock.L1BlockNumber;
                endBlock = result[1].block.L1BlockNumber;
                blockAfterEndBlock = result[1].nextBlock.L1BlockNumber;
            }
            catch (Exception ex)
            {
                if (ex.Message == "System.Exception : Index was outside the bounds of the array.")
                { 
                    endBlock = 0;
                    blockAfterEndBlock = 0;
                }
                throw new Exception(ex.Message);
            }


            if (startBlock < blockBeforeStartBlock)
            {
                throw new Exception("L2 block is not the first block in range for L1 block.");
            }

            if (endBlock < blockAfterEndBlock)
            {
                throw new Exception("L2 block is not the last block in range for L1 block.");
            }
        }
    }
}
