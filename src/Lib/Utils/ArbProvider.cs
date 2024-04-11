using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using System.Runtime.Serialization;
using System.Numerics;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using Nethereum.ABI.Util;

namespace Arbitrum.Utils
{
    public class Formats
    {
        public Transaction transaction { get; set; }
        public TransactionInput transactionRequest { get; set; }
        public TransactionReceipt receipt { get; set; }
        public dynamic receiptLog { get; set; }
        public Block block { get; set; }
        public BlockWithTransactions blockWithTransactions { get; set; }
        public NewFilterInput filter { get; set; }
        public FilterLog filterLog { get; set; }
    }
    public class ArbFormatter
    {
        private readonly Formats _formats;
        public Formats Formats => _formats;

        public ArbFormatter(Formats formats)
        {
            _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        }

        public Formats GetDefaultFormats()
        {
            Formats superFormats = base.GetDefaultFormats();

            Func<dynamic, BigInteger> bigNumber = this.BigNumber;
            Func<dynamic, string> hash = this.Hash;
            Func<dynamic, int> number = this.Number;

            var arbBlockProps = new
            {
                sendRoot = hash,
                sendCount = bigNumber,
                l1BlockNumber = number
            };

            var arbReceiptFormat = new Formats()
            {
                l1BlockNumber = number,
                gasUsedForL1 = bigNumber
            };

            return new Formats
            {
                receipt = arbReceiptFormat,
                block = new { superFormats.block, arbBlockProps },
                blockWithTransactions = new { superFormats.blockWithTransactions, arbBlockProps }
            };
        }
        public ArbTransactionReceipt Receipt(dynamic value)
        {
            var formattedValue = new ArbTransactionReceipt
            {
                L1BlockNumber = value.l1BlockNumber ?? 0,
                GasUsedForL1 = value.gasUsedForL1 ?? 0,
                // Include other properties as needed
            };

            return formattedValue;
        }

        public ArbBlock Block(dynamic block)
        {
            var formattedBlock = new ArbBlock
            {
                SendRoot = block.sendRoot ?? null,
                SendCount = block.sendCount ?? null,
                L1BlockNumber = block.l1BlockNumber ?? null,
                // Include other properties as needed
            };

            return formattedBlock;
        }

        public ArbBlockWithTransactions BlockWithTransactions(dynamic block)
        {
            var formattedBlock = new ArbBlockWithTransactions
            {
                SendRoot = block.sendRoot ?? null,
                SendCount = block.sendCount ?? null,
                L1BlockNumber = block.l1BlockNumber ?? null,
                // Include other properties as needed
            };

            return formattedBlock;
        }
    }

    public class ArbitrumProvider
    {
        public readonly Web3 Provider; // Replace "dynamic" with the actual provider type

        public ArbitrumProvider(Web3 provider, string? network = null)
        {
            if (provider is SignerOrProvider)
            {
                Provider = provider;
            }
            else if (provider is ArbitrumProvider)
            {
                Provider = provider;
            }

            this.Provider = provider;
        }

        public async Task<ArbTransactionReceipt> GetTransactionReceipt(string transactionHash)
        {
            dynamic receipt = await Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
            return new ArbFormatter().Receipt(receipt);
        }

        public async Task<ArbBlockWithTransactions> GetBlockWithTransactions(string blockIdentifier)
        {
            var block = await Provider.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(blockIdentifier);    /////
            return new ArbFormatter().BlockWithTransactions(block);
        }

        public async Task<ArbBlock> GetBlock(HexBigInteger blockIdentifier)
        {
            var block =  await Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockIdentifier);   /////
            return new ArbFormatter().Block(block);
        }
    }
}