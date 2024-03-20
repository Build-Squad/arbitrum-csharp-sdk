using Nethereum.Contracts;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Nethereum.Util.HashProviders;
using Nethereum.Contracts.Services;
using Nethereum.Web3;

namespace Arbitrum.Message
{
    public class L2ContractTransaction : ContractTransactionVO
    {
        public L2ContractTransaction(string contractAddress, string code, Transaction transaction)
            : base(contractAddress, code, transaction)
        {
        }
        public async Task<L2TransactionReceipt> Wait(int confirmations = 0)
        {
            return await Task.FromResult(new L2TransactionReceipt(new TransactionReceipt()));
        }
    }

    public class RedeemTransaction : L2ContractTransaction
    {
        public RedeemTransaction(string contractAddress, string code, Transaction transaction)
            : base(contractAddress, code, transaction)
        {
        }
        public Task<TransactionReceipt> WaitForRedeem()
        {
            // Implement logic to wait for redeem
            return Task.FromResult(new TransactionReceipt());
        }
    }

    public class L2TransactionReceipt : TransactionReceipt
    {
        public new string To { get; }
        public new string From { get; }
        public new string ContractAddress { get; }
        public new HexBigInteger TransactionIndex { get; }
        public new string Root { get; }
        public new BigInteger GasUsed { get; }
        public new string LogsBloom { get; }
        public new string BlockHash { get; }
        public new string TransactionHash { get; }
        public new JArray Logs { get; }
        public new HexBigInteger BlockNumber { get; }
        //public int Confirmations { get; }
        public new BigInteger CumulativeGasUsed { get; }
        public new BigInteger EffectiveGasPrice { get; }
        // public bool Byzantium { get; }
        public new HexBigInteger Type { get; }
        public new HexBigInteger? Status { get; }

        public L2TransactionReceipt(TransactionReceipt tx)
        {
            To = tx.To;
            From = tx.From;
            ContractAddress = tx.ContractAddress;
            TransactionIndex = tx.TransactionIndex;
            Root = tx.Root;
            GasUsed = tx.GasUsed;
            LogsBloom = tx.LogsBloom;
            BlockHash = tx.BlockHash;
            TransactionHash = tx.TransactionHash;
            Logs = tx.Logs;
            BlockNumber = tx.BlockNumber;
            //Confirmations = tx.Confirmations;
            CumulativeGasUsed = tx.CumulativeGasUsed;
            EffectiveGasPrice = tx.EffectiveGasPrice;
            //Byzantium = tx.Byzantium;
            Type = tx.Type;
            Status = tx.Status;
        }

        public bool GetL2ToL1Events(IEthApiContractService provider)  ///////
        {
            return true;
        }

        public bool GetRedeemScheduledEvents()        ////////
        {
            return true;
        }


    }
}
