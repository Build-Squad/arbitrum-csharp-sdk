﻿using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using System.Runtime.Serialization;
using System.Numerics;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using Nethereum.ABI.Util;
using Nethereum.JsonRpc.Client;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;

namespace Arbitrum.Utils
{
    public class Formats
    {
        public Transaction? Transaction { get; set; }
        public TransactionRequest? TransactionRequest { get; set; }
        public TransactionReceipt? Receipt { get; set; }
        public FilterLog? ReceiptLog { get; set; }
        public Block? Block { get; set; }
        public BlockWithTransactions? BlockWithTransactions { get; set; }
        public NewFilterInput? Filter { get; set; }
        public FilterLog? FilterLog { get; set; }
    }
    public class ArbFormatter
    {
        //public Formats GetDefaultFormats()
        //{
        //    Formats superFormats = base.GetDefaultFormats();

        //    Func<dynamic, BigInteger> bigNumber = this.BigNumber;
        //    Func<dynamic, string> hash = this.Hash;
        //    Func<dynamic, int> number = this.Number;

        //    var arbBlockProps = new
        //    {
        //        sendRoot = hash,
        //        sendCount = bigNumber,
        //        l1BlockNumber = number
        //    };

        //    var arbReceiptFormat = new Formats()
        //    {
        //        l1BlockNumber = number,
        //        gasUsedForL1 = bigNumber
        //    };

        //    return new Formats
        //    {
        //        receipt = arbReceiptFormat,
        //        block = new { superFormats.block, arbBlockProps },
        //        blockWithTransactions = new { superFormats.blockWithTransactions, arbBlockProps }
        //    };
        //}
        public ArbTransactionReceipt Receipt(dynamic receipt)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }
            return new ArbTransactionReceipt()
            {
                // Assign values from `receipt` to the properties of `ArbTransactionReceipt` using null-coalescing
                //L1BlockNumber = receipt.BlockNumber ?? null,
                //GasUsedForL1 = receipt.GasUsedForL1 ?? null,
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
                // Assign values from `block` to the properties of `Block` using null-coalescing
                //SendRoot = block?.SendRoot ?? null,
                //SendCount = block?.SendCount ?? null,
                //L1BlockNumber = block?.L1BlockNumber ?? null,
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
                // Assign values from `block` to the properties of `Block` using null-coalescing
                //SendRoot = block?.SendRoot ?? null,
                //SendCount = block?.SendCount ?? null,
                //L1BlockNumber = block?.L1BlockNumber ?? null,
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
            if (provider is SignerOrProvider signerOrProvider)
            {
                Provider = signerOrProvider.Provider;
            }
            else if (provider is ArbitrumProvider arbitrumProvider)
            {
                Provider = arbitrumProvider.Provider;
            }
            Provider = provider;

            this.Formatter = new ArbFormatter();
        }
        public async Task<ArbTransactionReceipt> GetTransactionReceipt(string transactionHash)
        {
            var receipt = await Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            return new ArbFormatter().Receipt(receipt);
        }

        public async Task<ArbBlockWithTransactions> GetBlockWithTransactions(string blockIdentifier)
        {
            var block = await Provider.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(blockIdentifier);  
            return new ArbFormatter().BlockWithTransactions(block);
        }

        public async Task<ArbBlock> GetBlock(dynamic blockIdentifier)
        {
            dynamic block;
            try
            {
                //Console.WriteLine(typeof(blockIdentifier));
                block = await Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockIdentifier);
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return new ArbFormatter().Block(block);   
        }
    }
}