using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;

namespace Arbitrum.Utils
{
    public class Lib
    {

        public static async Task<bool> IsArbitrumChain(Web3 provider)
        {
            try
            {
                var arbSysContract = LoadContractUtils.LoadContract(
                    provider: provider,
                    contractName: "ArbSys",
                    address: Constants.ARB_SYS_ADDRESS,
                    isClassic: false
                    );
                var arbSysContractFunction = arbSysContract.GetFunction("arbOSVersion");
                await arbSysContractFunction.CallAsync<bool>();
                return true;
            }
            catch (Exception ex)
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
                    var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                    if (confirmations.HasValue)
                    {
                        var latestBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        if (latestBlock.Value - receipt.BlockNumber.Value < confirmations.Value)
                        {
                            return null;
                        }
                    }
                    return receipt;
                }
                catch (TimeoutException)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
            else
            {
                try
                {
                    var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                    return receipt;
                }
                catch (Exception ex) when (ex is RpcResponseException || ex is RpcClientUnknownException)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }

        public static async Task<BigInteger?> GetFirstBlockForL1Block(
        Web3 provider,
        BigInteger forL1Block,
        bool allowGreater = false,
        BigInteger? minL2Block = null,
        string maxL2Block = "latest"
        )
        {
            if (!await IsArbitrumChain(provider))
            {
                return forL1Block;
            }

            var arbProvider = new ArbitrumProvider(provider);

            var currentArbBlock = await arbProvider.Provider.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var arbitrumChainId = await arbProvider.Provider.Eth.ChainId.SendRequestAsync();
            var nitroGenesisBlock = NetworkUtils.l2Networks[(int)arbitrumChainId.Value].NitroGenesisBlock;
             
            async Task<BigInteger> GetL1Block(BigInteger forL2Block)
            {
                var block = await arbProvider.GetBlock(forL2Block.ToHexBigInteger());
                return BigInteger.Parse(block.L1BlockNumber.ToString(), System.Globalization.NumberStyles.HexNumber);
            }

            if (!minL2Block.HasValue)
            {
                minL2Block = nitroGenesisBlock;
            }

            if (maxL2Block == "latest")
            {
                maxL2Block = currentArbBlock.ToString();
            }

            if (minL2Block >= BigInteger.Parse(maxL2Block))
            {
                throw new ArgumentException($"'minL2Block' ({minL2Block}) must be lower than 'maxL2Block' ({maxL2Block}).");
            }

            if (minL2Block < nitroGenesisBlock)
            {
                throw new ArgumentException($"'minL2Block' ({minL2Block}) cannot be below 'nitroGenesisBlock', which is {nitroGenesisBlock} for the current network.");
            }

            var start = minL2Block.Value;
            var end = BigInteger.Parse(maxL2Block);

            BigInteger? resultForTargetBlock = null;
            BigInteger? resultForGreaterBlock = null;

            while (start <= end)
            {
                var mid = start + (end - start) / 2;
                var l1Block = await GetL1Block(mid);

                if (l1Block == forL1Block)
                {
                    resultForTargetBlock = mid;
                    end = mid - 1;
                }
                else if (l1Block < forL1Block)
                {
                    start = mid + 1;
                }
                else
                {
                    if (allowGreater)
                    {
                        resultForGreaterBlock = mid;
                    }
                    end = mid - 1;
                }
            }

            return resultForTargetBlock ?? resultForGreaterBlock;
        }


        public static async Task<object[]> GetBlockRangesForL1Block(
        Web3 provider,
        int forL1Block,
        bool allowGreater = false,
        int? minL2Block = null,
        string maxL2Block = "latest"
        )
        {
            // Convert maxL2Block to current block number if needed
            if (maxL2Block == "latest")
            {
                maxL2Block = (await provider.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value.ToString();
            }

            var arbProvider = new ArbitrumProvider(provider);

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
                return new object[] { null, null };
            }

            if (startBlock != null && endBlock != null)
            {
                return new object[] { startBlock, endBlock - 1 };
            }

            return new object[] { startBlock, maxL2Block };
        }

    }
}
