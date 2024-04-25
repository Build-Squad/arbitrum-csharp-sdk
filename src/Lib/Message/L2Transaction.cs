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
using Nethereum.ABI.FunctionEncoding.Attributes;

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
    public class LifetimeExtendedEvent
    {
        public string? TicketId { get; set; }
        public BigInteger? NewTimeout { get; set; }
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
        private readonly L2ContractTransaction _transaction;
        private readonly Web3 _l2Provider;

        public RedeemTransaction(L2ContractTransaction transaction, Web3 l2Provider)
        {
            _transaction = transaction;
            _l2Provider = l2Provider;
        }

        public L2ContractTransaction Wait()
        {
            return _transaction;
        }

        public async Task<TransactionReceipt> WaitForRedeem()
        {
            var l2Receipt = new L2TransactionReceipt(_transaction);
            var redeemScheduledEvents = await l2Receipt.GetRedeemScheduledEvents(_l2Provider);

            if (redeemScheduledEvents.Count() != 1)
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

        public async Task<FilterLog[]> GetL2ToL1Events(Web3 provider)
        {
            var classicLogs = await LogParser.ParseTypedLogs(provider, "ArbSys", Logs, "L2ToL1Transaction", isClassic: false);
            var nitroLogs = await LogParser.ParseTypedLogs(provider, "ArbSys", Logs, "L2ToL1Tx", isClassic: false);

            return classicLogs.Concat(nitroLogs).ToArray();
        }

        public async Task<FilterLog[]> GetRedeemScheduledEvents(Web3 provider)
        {
            var redeemScheduledEvents = await LogParser.ParseTypedLogs(provider, "ArbRetryableTx", Logs, "RedeemScheduled");
            return redeemScheduledEvents.ToArray();
        }

        public async Task<IEnumerable<L2ToL1Message>> GetL2ToL1Messages<T>(T l1SignerOrProvider) where T : SignerOrProvider

        {
            var provider = SignerProviderUtils.GetProvider(l1SignerOrProvider);

            if (provider == null)
            {
                throw new ArbSdkError("Signer not connected to provider.");
            }

            var events = await GetL2ToL1Events(provider);

            var messages = new List<L2ToL1Message>();

            foreach (var log in events)
            {
                messages.Add(await L2ToL1Message.FromEvent<T>(l1SignerOrProvider, log));
            }

            return messages;
        }

        public async Task<BigInteger> GetBatchConfirmations(Web3 l2Provider)
        {
            var nodeInterfaceContract = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    provider: l2Provider,
                                    isClassic: false
                                );
            var nodeInterfaceContractFunction = nodeInterfaceContract.GetFunction("nodeInterfaceContract");
            return await nodeInterfaceContractFunction.CallAsync<BigInteger>(BlockHash);
        }

        public async Task<BigInteger> GetBatchNumber(IClient l2Provider)
        {
            var arbProvider = new ArbitrumProvider(l2Provider);
            var nodeInterfaceContract = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    provider: l2Provider,
                                    isClassic: false
                                );
            TransactionReceipt rec = await arbProvider.GetTransactionReceipt(TransactionHash);

            if (rec == null)
            {
                throw new ArbSdkError("No receipt available for current transaction");
            }
            var nodeInterfaceContractFunction = nodeInterfaceContract.GetFunction("findBatchContainingBlock");

            return await nodeInterfaceContractFunction.CallAsync<BigInteger>(BlockNumber);
        }

        public async Task<bool> IsDataAvailable(Web3 l2Provider, int confirmations = 10)
        {
            var batchConfirmations = await GetBatchConfirmations(l2Provider);
            return (int)batchConfirmations > confirmations;
        }

        public static L2ContractTransaction MonkeyPatchWait(TransactionReceipt contractTransaction)   /////
        {

            return new L2TransactionReceipt(contractTransaction);
        }

        public static RedeemTransaction ToRedeemTransaction(L2ContractTransaction redeemTx, Web3 l2Provider)
        {
            return new RedeemTransaction(redeemTx, l2Provider);

        }

    }
}
