using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Arbitrum.Message
{
    public class L1Transaction
    {
    }
    public class L1ContractTransaction
    {

    }
    public class L1TransactionReceipt : TransactionReceipt
    {
        public static L1ContractTransaction MonkeyPatchWait()
        {
            return new L1ContractTransaction { };
        }
    }
}
