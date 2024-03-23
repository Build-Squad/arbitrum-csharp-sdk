using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;

namespace Arbitrum.Utils
{
    public class ArbFormatter
    {
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

        public async Task<ArbBlock> GetBlock(string blockIdentifier)
        {
            var block =  await Provider.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(blockIdentifier);   /////
            return new ArbFormatter().Block(block);
        }
    }
}