﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Arbitrum.Utils
{
    public class Lib
    {
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
    }
}
