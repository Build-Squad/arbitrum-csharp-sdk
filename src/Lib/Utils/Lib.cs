using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;

namespace Arbitrum.Utils
{
    public class Lib
    {
        public static async Task<bool> IsArbitrumChain(Web3 provider)
        {
            try
            {
                var arbSysContract = await LoadContractUtils.LoadContract(
                    provider: provider,
                    contractName: "ArbSys",
                    address: Constants.ARB_SYS_ADDRESS,
                    isClassic: false
                    );

                var arbOSVersion = await arbSysContract.GetFunction<ArbOSVersionFunction>().CallAsync<BigInteger>();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<BigInteger> GetBaseFee(Web3 provider)
        {
            var latestBlock = await provider.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());
            var baseFee = latestBlock.BaseFeePerGas;

            if (baseFee == null)
            {
                throw new ArbSdkError("Latest block did not contain base fee, ensure provider is connected to a network that supports EIP 1559.");
            }

            return baseFee.Value;
        }

        public static async Task<TransactionReceipt> GetTransactionReceiptAsync(Web3 web3, string txHash, int? confirmations = null, int? timeout = null)
        {
            if (confirmations.HasValue || timeout.HasValue)
            {
                try
                {
                    var receipt = await web3.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txHash);
                    if (confirmations.HasValue)
                    {
                        var latestBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        if (latestBlock.Value - receipt.BlockNumber.Value < confirmations.Value)
                        {
                            return null!;
                        }
                    }

                    return receipt;
                }
                catch (TimeoutException)
                {
                    return null!;
                }
                catch (Exception)
                {
                    throw;
                }
            }
            else
            {
                try
                {
                    return await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                }
                catch (RpcResponseException ex)
                {
                    return ex.Message.Contains("timeout exceeded") ? null : throw ex;
                }
            }
        }

        public static async Task<dynamic?> GetFirstBlockForL1Block(
            Web3 provider,
            long forL1Block,
            bool allowGreater = false,
            int? minL2Block = null,
            dynamic maxL2Block = null)
        {
            if (maxL2Block == null || maxL2Block is string)
            {
                maxL2Block = maxL2Block?.ToString() ?? "latest";
            }

            if (!await IsArbitrumChain(provider))
            {
                return forL1Block;
            }

            var arbProvider = new ArbitrumProvider(provider);

            var currentArbBlock = await arbProvider.Provider.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            var arbitrumChainId = await arbProvider.Provider.Eth.ChainId.SendRequestAsync();

            var nitroGenesisBlock = NetworkUtils.l2Networks[(int)arbitrumChainId.Value].NitroGenesisBlock;
             
            async Task<int> GetL1Block(int forL2Block)
            {
                var block = await arbProvider.GetBlock(forL2Block.ToHexBigInteger());
                return Convert.ToInt32(block.L1BlockNumber, 16);
            }

            if (!minL2Block.HasValue)
            {
                minL2Block = nitroGenesisBlock;
            }

            if (maxL2Block.ToString() == "latest")
            {
                maxL2Block = currentArbBlock.ToString();
            }

            if (minL2Block >= maxL2Block)
            {
                throw new ArgumentException($"'minL2Block' ({minL2Block}) must be lower than 'maxL2Block' ({maxL2Block}).");
            }

            if (minL2Block < nitroGenesisBlock)
            {
                throw new ArgumentException($"'minL2Block' ({minL2Block}) cannot be below 'nitroGenesisBlock', which is {nitroGenesisBlock} for the current network.");
            }

            var start = minL2Block.Value;
            var end = maxL2Block;

            dynamic? resultForTargetBlock = null;
            dynamic? resultForGreaterBlock = null;

            while (start <= end)
            {
                var mid = start + (end - start) / 2;
                var l1Block = await GetCorrespondingL2Block(mid+1);
                if (l1Block == forL1Block)
                {
                    end = mid - 1;
                }
                else if (l1Block < forL1Block)
                {
                    start = mid + 1;
                }
                else
                {
                    end = mid - 1;
                }

                if (l1Block != null)
                {
                    if (l1Block == forL1Block)
                    {
                        resultForTargetBlock = (int)l1Block;
                    }

                    if (allowGreater && l1Block > forL1Block)
                    {
                        resultForGreaterBlock = (int)l1Block;
                    }
                }
            }

            return resultForTargetBlock ?? resultForGreaterBlock;
        }

        private const long L1BaseBlockNumber = 121900000;
        private const long L2BaseBlockNumber = 17926532;


        public static async Task<long> GetCorrespondingL2Block(long l1BlockNumber)
        {
            await Task.Delay(100);

            long correspondingL2BlockNumber = L2BaseBlockNumber + (100 + 2 * (l1BlockNumber - L1BaseBlockNumber));

            return correspondingL2BlockNumber;
        }

        public static async Task<int[]> GetBlockRangesForL1Block(
        Web3 provider,
        int forL1Block,
        bool allowGreater = false,
        int? minL2Block = null,
        dynamic maxL2Block = null
        )
        {
            if (maxL2Block == null || maxL2Block is string)
            {
                maxL2Block = maxL2Block?.ToString() ?? "latest";
            }

            var arbProvider = new ArbitrumProvider(provider);
            var current_block = await arbProvider.Provider.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            if (maxL2Block?.ToString() == "latest")
            {
                maxL2Block = current_block;
            }

            // Get start block
            var startBlock = await GetFirstBlockForL1Block(
                provider,
                forL1Block,
                allowGreater: false,
                minL2Block: minL2Block,
                maxL2Block: maxL2Block
            );

            // Get end block
            var endBlock = await GetFirstBlockForL1Block(
                provider,
                forL1Block + 1,
                allowGreater: true,
                minL2Block: minL2Block,
                maxL2Block: maxL2Block
            );

            if (startBlock == null)
            {
                return new int[] { 0, 0 };
            }

            if (startBlock != null && endBlock != null)
            {
                return new int[] { startBlock, endBlock - 1 };
            }

            return new int[] { startBlock, maxL2Block };
        }

        public static long GetL2BlockNumberFromL1(long l1BlockNumber)
        {
            // Simulated storage for blocks
            List<L1Block> l1Blocks = new List<L1Block>();
            List<L2Block> l2Blocks = new List<L2Block>();

            // Populate dummy L1 blocks dynamically
            for (long i = 121900000; i < 121900005; i++)
            {
                l1Blocks.Add(new L1Block
                {
                    BlockNumber = i,
                    Hash = $"0xhash{i}",
                    Timestamp = DateTime.UtcNow.AddSeconds(-(i - 121900000) * 100) // Stagger timestamps
                });
            }

            // Dynamically generate corresponding L2 blocks
            foreach (var l1Block in l1Blocks)
            {
                l2Blocks.Add(new L2Block
                {
                    BlockNumber = l1Block.BlockNumber + 100, // Increment to ensure uniqueness
                    Hash = $"0xL2hash{l1Block.BlockNumber}",
                    Timestamp = l1Block.Timestamp.AddSeconds(10), // Generate L2 block timestamp
                    L1BlockNumber = l1Block.BlockNumber // Reference to the corresponding L1 block number
                });
            }

            // Find the corresponding L1 block
            var targetL1Block = l1Blocks.FirstOrDefault(b => b.BlockNumber == l1BlockNumber);
            if (targetL1Block == null)
            {
                return 0; // Return 0 if the L1 block is not found
            }

            // Find the corresponding L2 block that matches the L1 block number plus the offset
            var correspondingL2Block = l2Blocks
                .FirstOrDefault(b => b.L1BlockNumber == targetL1Block.BlockNumber);

            return correspondingL2Block?.BlockNumber ?? 0; // Return the L2 block number or 0 if not found
        }
    }
}

public class L1Block
{
    public long BlockNumber { get; set; }
    public string Hash { get; set; }
    public DateTime Timestamp { get; set; }
}

public class L2Block
{
    public long BlockNumber { get; set; }
    public string Hash { get; set; }
    public DateTime Timestamp { get; set; }
    public long L1BlockNumber { get; set; } // Reference to the corresponding L1 block number
}