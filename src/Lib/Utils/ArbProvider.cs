using Arbitrum.DataEntities;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Arbitrum.Utils
{
    public class ArbFormatter
    {
        public ArbTransactionReceipt Receipt(dynamic receipt)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }
            return new ArbTransactionReceipt()
            {
                TransactionHash = receipt.TransactionHash ?? null,
                TransactionIndex = receipt.TransactionIndex ?? null,
                EffectiveGasPrice = receipt.EffectiveGasPrice ?? null,
                Logs = receipt.Logs ?? null,
                Root = receipt.Root ?? null,
                BlockHash = receipt.BlockHash ?? null,
                BlockNumber = receipt.BlockNumber ?? null,
                From = receipt.From ?? null,
                To = receipt.To ?? null,
                CumulativeGasUsed = receipt.CumulativeGasUsed ?? null,
                GasUsed = receipt.GasUsed ?? null,
                ContractAddress = receipt.ContractAddress ?? null,
                Status = receipt.Status ?? null,
                Type = receipt.Type ?? null,
                LogsBloom = receipt.LogsBloom ?? null
            };
        }

        public ArbBlock Block(BlockWithTransactions block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var formattedBlock = new ArbBlock
            {
                L1BlockNumber = block?.Number?.HexValue,
                Number = block?.Number,
                BlockHash = block?.BlockHash,
                Author = block?.Author,
                SealFields = block?.SealFields,
                ParentHash = block?.ParentHash,
                Nonce = block?.Nonce,
                Sha3Uncles = block?.Sha3Uncles,
                LogsBloom = block?.LogsBloom,
                TransactionsRoot = block?.TransactionsRoot,
                StateRoot = block?.StateRoot,
                ReceiptsRoot = block?.ReceiptsRoot,
                Miner = block?.Miner,
                Difficulty = block?.Difficulty,
                TotalDifficulty = block?.TotalDifficulty,
                MixHash = block?.MixHash,
                ExtraData = block?.ExtraData,
                Size = block?.Size,
                GasLimit = block?.GasLimit,
                GasUsed = block?.GasUsed,
                Timestamp = block?.Timestamp,
                Uncles = block?.Uncles,
                BaseFeePerGas = block?.BaseFeePerGas,
                WithdrawalsRoot = block?.WithdrawalsRoot,
                Withdrawals = block?.Withdrawals
            };

            return formattedBlock;
        }

        public ArbBlockWithTransactions BlockWithTransactions(dynamic block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var formattedBlock = new ArbBlockWithTransactions
            {
                Transactions = block?.Transactions ?? null,
                Number = block?.Number ?? null,
                BlockHash = block?.BlockHash ?? null,
                Author = block?.Author ?? null,
                SealFields = block?.SealFields ?? null,
                ParentHash = block?.ParentHash ?? null,
                Nonce = block?.Nonce ?? null,
                Sha3Uncles = block?.Sha3Uncles ?? null,
                LogsBloom = block?.LogsBloom ?? null,
                TransactionsRoot = block?.TransactionsRoot ?? null,
                StateRoot = block?.StateRoot ?? null,
                ReceiptsRoot = block?.ReceiptsRoot ?? null,
                Miner = block?.Miner ?? null,
                Difficulty = block?.Difficulty ?? null,
                TotalDifficulty = block?.TotalDifficulty ?? null,
                MixHash = block?.MixHash ?? null,
                ExtraData = block?.ExtraData ?? null,
                Size = block?.Size ?? null,
                GasLimit = block?.GasLimit ?? null,
                GasUsed = block?.GasUsed ?? null,
                Timestamp = block?.Timestamp ?? null,
                Uncles = block?.Uncles ?? null,
                BaseFeePerGas = block?.BaseFeePerGas ?? null,
                WithdrawalsRoot = block?.WithdrawalsRoot ?? null,
                Withdrawals = block?.Withdrawals ?? null,
            };

            return formattedBlock;
        }
    }

    public class ArbitrumProvider
    {
        public Web3 Provider { get; private set; }
        public ArbFormatter Formatter { get; private set; }

        public ArbitrumProvider(dynamic provider, string network = null)
        {
            Provider = provider;

            Formatter = new ArbFormatter();
        }

        public async Task<ArbTransactionReceipt> GetTransactionReceipt(string transactionHash)
        {
            var receipt = await Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            return new ArbFormatter().Receipt(receipt);
        }

        public async Task<ArbBlock> GetBlock(HexBigInteger blockIdentifier)
        {
            dynamic block;
            try
            {
                block = await Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockIdentifier);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            return new ArbFormatter().Block(block);   
        }
    }
}