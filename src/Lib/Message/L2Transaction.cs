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

using Arbitrum.DataEntities;
using Arbitrum.Utils;

namespace Arbitrum.Message
{
    public class RedeemScheduledEvent
    {
        public string? TicketId { get; set; }
        public string? RetryTxHash { get; set; }
        public HexBigInteger? SequenceNum { get; set; }
        public HexBigInteger? DonatedGas { get; set; }
        public string? GasDonor { get; set; }
        public HexBigInteger? MaxRefund { get; set; }
        public HexBigInteger? SubmissionFeeRefund { get; set; }
    }
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

    public class RedeemTransaction
    {
        private readonly Transaction _transaction;
        private readonly Web3 _l2Provider;

        public RedeemTransaction(Transaction transaction, Web3 l2Provider)
        {
            _transaction = transaction;
            _l2Provider = l2Provider;
        }

        public Transaction Wait()
        {
            return _transaction;
        }

        public async Task<TransactionReceipt> WaitForRedeem()
        {
            var l2Receipt = new L2TransactionReceipt(_transaction);
            var redeemScheduledEvents = l2Receipt.GetRedeemScheduledEvents(_l2Provider);

            if (redeemScheduledEvents.Count != 1)
            {
                throw new ArbSdkError($"Transaction is not a redeem transaction: {_transaction.TransactionHash}");
            }

            return await Lib.GetTransactionReceiptAsync(redeemScheduledEvents[0]["retryTxHash"]);
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

        public List<L2ToL1TransactionEvent> GetL2ToL1Events()
        {
            var classicLogs = ParseTypedLogs<ArbSys__factory.L2ToL1TransactionEvent>(this.Logs, "L2ToL1Transaction");
            var nitroLogs = ParseTypedLogs<ArbSys__factory.L2ToL1TransactionEvent>(this.Logs, "L2ToL1Tx");

            return classicLogs.Concat(nitroLogs).ToList();
        }

        public List<RedeemScheduledEvent> GetRedeemScheduledEvents()
        {
            var redeemScheduledEvents = LogParser.ParseTypedLogs(provider: Web3 provider, logs: this.Logs,contractName: "RedeemScheduled");
            return redeemScheduledEvents.Select(log => new EventArgs<RedeemScheduledEvent>(log)).ToList();
        }

        public static L2ContractTransaction MonkeyPatchWait(TransactionReceipt contractTransaction)
        {
            Func<Task<TransactionReceipt>> wait = contractTransaction.WaitAsync();
            contractTransaction.WaitAsync = async (_confirmations) =>
            {
                // Ignore the confirmations for now
                var result = await wait();
                return new L2TransactionReceipt(result);
            };

            return contractTransaction as L2ContractTransaction;
        }

        public static RedeemTransaction ToRedeemTransaction(L2ContractTransaction redeemTx, Web3 l2Provider)
        {

            RedeemTransaction returnRec = new RedeemTransaction();

            Func<Task<TransactionReceipt>> waitForRedeemFunc = async () =>
            {
                TransactionReceipt rec = await redeemTx.Wait();

                L2TransactionReceipt l2Rec = new L2TransactionReceipt(rec); // Create an instance of L2TransactionReceipt

                var redeemScheduledEvents = await l2Rec.GetRedeemScheduledEvents(l2Provider);

                if (redeemScheduledEvents.Length != 1)
                {
                    throw new Exception($"Transaction is not a redeem transaction: {rec.TransactionHash}");
                }

                return await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(redeemScheduledEvents[0].RetryTxHash);
            };

            returnRec.WaitForRedeem = waitForRedeemFunc;

            return returnRec;
        }

    }
}
