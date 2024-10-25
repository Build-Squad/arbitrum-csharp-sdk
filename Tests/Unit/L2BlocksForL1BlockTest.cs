using NUnit.Framework;
using System.Threading.Tasks;
using Arbitrum.Message;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using System.ComponentModel.DataAnnotations;

namespace Arbitrum.Tests.Unit
{
    [TestFixture]
    public class L2BlocksLookupTests
    {
        private readonly Web3 provider = new Web3("https://arb1.arbitrum.io/rpc");
        private readonly ArbitrumProvider arbProvider;

        public L2BlocksLookupTests()
        {
            arbProvider = new ArbitrumProvider(provider);
        }

        [Test]
        public async Task SuccessfullySearchesForL2BlockRange()
        {
            var l2Blocks = await Lib.GetBlockRangesForL1Block(provider: arbProvider.Provider,forL1Block: 17926532, minL2Block: 121800000,maxL2Block: 122000000);
            Assert.That(l2Blocks.Length, Is.EqualTo(2));
            await ValidateL2Blocks(l2Blocks, 2);
        }

        [Test]
        public async Task FailsToSearchForL2BlockRange()
        {
            var l2Blocks = await Lib.GetBlockRangesForL1Block(provider: arbProvider.Provider, forL1Block: 17926533, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Blocks.Length, Is.EqualTo(2));
            foreach (var block in l2Blocks)
            {
                Assert.That(block, Is.EqualTo(0));
            }
        }

        [Test]
        public async Task SuccessfullySearchesForFirstL2Block()
        {
            var l2Block = await Lib.GetFirstBlockForL1Block(provider: arbProvider.Provider, forL1Block: 17926532, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Block, Is.Not.Null);
            await ValidateL2Blocks(new int[] { l2Block }, 1);
        }

        [Test]
        public async Task FailsToSearchForFirstL2BlockWithoutAllowGreaterFlag()
        {
            var l2Block = await Lib.GetFirstBlockForL1Block(provider: arbProvider.Provider, forL1Block: 17926533, allowGreater: false, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Block, Is.Null);
        }

        [Test]
        public async Task SuccessfullySearchesForFirstL2BlockWithAllowGreaterFlag()
        {
            var l2Block = await Lib.GetFirstBlockForL1Block(provider: arbProvider.Provider, forL1Block: 17926533, allowGreater: true, minL2Block: 121800000, maxL2Block: 122000000);
            Assert.That(l2Block, Is.Not.Null);
            await ValidateL2Blocks(new int[] { l2Block }, 1);
        }

        public async Task ValidateL2Blocks(int[] l2Blocks, int l2BlocksCount, string type = "int32")
        {
            if (l2Blocks.Length != l2BlocksCount)
            {
                throw new ArgumentException($"Expected L2 block range to have the array length of {l2BlocksCount}, got {l2Blocks.Length}.");
            }

            // Check if all blocks are integers or null when blockType is "number"
            if (l2Blocks.Any(block => block.GetType().Name.ToLower() == "int32" && !l2Blocks.All(block => block == null || block is int)))
            {
                throw new ArgumentException("Expected all blocks to be integers or None.");
            }

            // Check if all blocks are null when blockType is "undefined"
            if (l2Blocks.Any(block => block.GetType().Name.ToLower() == "undefined") && !l2Blocks.All(block => block == null))
            {
                throw new ArgumentException("Expected all blocks to be None when block type is 'undefined'.");
            }

            // Early return if blockType is "undefined"
            if (type == "undefined")
            {
                return;
            }

            var arbProvider = new ArbitrumProvider(new Web3(new RpcClient(new Uri("https://arb1.arbitrum.io/rpc"))));
            
            var tasks = new List<Task<ArbBlock>>();
            for (int index = 0; index < l2BlocksCount; index++)
            {
                var l2Block = l2Blocks[index];
                if (l2Block == null)
                {
                    throw new ValidationException("L2 block is undefined.");
                }

                bool isStartBlock = index == 0;
                tasks.Add(arbProvider.GetBlock(l2Block.ToHexBigInteger())); // Current block
                tasks.Add(arbProvider.GetBlock((l2Block + (isStartBlock ? -1  : 1)).ToHexBigInteger())); // Adjacent block
            }

            // Await all tasks (equivalent to asyncio.gather in Python)
            var blocks = await Task.WhenAll(tasks);

            // Loop through the fetched blocks in pairs (current and adjacent)
            for (int i = 0; i < blocks.Length; i += 2)
            {
                var currentBlock = blocks[i];
                var adjacentBlock = blocks[i + 1];

                // Skip if either block is null
                if (currentBlock == null || adjacentBlock == null) continue;

                int currentBlockNumber = Convert.ToInt32(currentBlock.L1BlockNumber, 16);
                int adjacentBlockNumber = Convert.ToInt32(adjacentBlock.L1BlockNumber, 16);

                bool isStartBlock = i == 0;

                if (isStartBlock)
                {
                    if (currentBlockNumber <= adjacentBlockNumber)
                    {
                        throw new ValidationException("L2 start block is not the first block in range for L1 block.");
                    }
                }
                else
                {
                    if (currentBlockNumber >= adjacentBlockNumber)
                    {
                        throw new ValidationException("L2 end block is not the last block in range for L1 block.");
                    }
                }
            }

            //if (startBlock < blockBeforeStartBlock)
            //{
            //    throw new Exception("L2 block is not the first block in range for L1 block.");
            //}

            //if (endBlock < blockAfterEndBlock)
            //{
            //    throw new Exception("L2 block is not the last block in range for L1 block.");
            //}
        }
    }
}
