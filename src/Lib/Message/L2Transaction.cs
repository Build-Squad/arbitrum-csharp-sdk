using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;

namespace Arbitrum.Message
{
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
        private readonly L2TransactionReceipt _transaction;
        private readonly Web3 _l2Provider;

        public RedeemTransaction(L2TransactionReceipt transaction, Web3 l2Provider)
        {
            _transaction = transaction;
            _l2Provider = l2Provider;
        }

        public async Task<L2TransactionReceipt> Wait()
        {
            return await Task.FromResult(_transaction);
        }

        public async Task<TransactionReceipt> WaitForRedeem()
        {
            var l2Receipt = new L2TransactionReceipt(_transaction);
            var redeemScheduledEvents = await l2Receipt.GetRedeemScheduledEvents(_l2Provider);

            if (redeemScheduledEvents.Count() != 1)
            {
                throw new ArbSdkError($"Transaction is not a redeem transaction: {_transaction.TransactionHash}");
            }

            return await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(redeemScheduledEvents?.FirstOrDefault()?.Event?.RetryTxHash?.ToHex());
        }
    }

    public class L2TransactionReceipt : TransactionReceipt
    {

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
            CumulativeGasUsed = tx.CumulativeGasUsed;
            EffectiveGasPrice = tx.EffectiveGasPrice;
            Type = tx.Type;
            Status = tx.Status;
        }

        public async Task<IEnumerable<EventLog<RedeemScheduledEventDTO>>> GetRedeemScheduledEvents(Web3 provider, string? address = null)
        {
            var redeemScheduledEvents = LogParser.ParseTypedLogs<RedeemScheduledEventDTO>(provider, Logs, address);
            return redeemScheduledEvents;
        }

        public async Task<IEnumerable<EventLog<T>>> GetL2ToL1Events<T>(Web3 provider, string? address = null) where T : IEventDTO
        {
            var combinedLogs = new List<EventLog<IEventDTO>>();

            var classicLogs = LogParser.ParseTypedLogs<L2ToL1TransactionEventDTO>(provider, Logs, address);
            combinedLogs.AddRange(classicLogs.Select(log => new EventLog<IEventDTO>(log.Event, log.Log)));

            var nitroLogs = LogParser.ParseTypedLogs<L2ToL1TxEventDTO>(provider, Logs, address);
            combinedLogs.AddRange(nitroLogs.Select(log => new EventLog<IEventDTO>(log.Event, log.Log)));

            return combinedLogs
                .Where(log => log.Event is T)
                .Select(log => new EventLog<T>((T)log.Event, log.Log))
                .ToList();
        }

        public async Task<IEnumerable<L2ToL1Message>> GetL2ToL1Messages(SignerOrProvider l1SignerOrProvider, string? address = null)
        {
            var provider = SignerProviderUtils.GetProvider(l1SignerOrProvider)
                ?? throw new ArbSdkError("Signer not connected to provider.");

            var eventLogs = await GetL2ToL1Events<IEventDTO>(provider, address);

            var messages = new List<L2ToL1Message>();

            foreach (var log in eventLogs)
            {
                var message = await L2ToL1Message.FromEvent(l1SignerOrProvider, log.Event);
                messages.Add(message);
            }

            return messages;
        }


        public async Task<BigInteger> GetBatchConfirmations(SignerOrProvider l2Signer)
        {
            var nodeInterfaceContract = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    provider: l2Signer.Provider,
                                    isClassic: false
                                );

            var nodeInterfaceContractFunction = nodeInterfaceContract.GetFunction("getL1Confirmations");
            var byteValue = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(BlockHash);
            return await nodeInterfaceContractFunction.CallAsync<BigInteger>(byteValue);
        }

        public async Task<BigInteger> GetBatchNumber(Web3 l2Provider)
        {
            var arbProvider = new ArbitrumProvider(l2Provider); 
            var nodeInterfaceContract = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    provider: l2Provider,
                                    isClassic: false
                                );

            var nodeInterfaceContractFunction = nodeInterfaceContract.GetFunction("findBatchContainingBlock");
            var rec = await arbProvider.GetTransactionReceipt(TransactionHash) 
                ?? throw new ArbSdkError("No receipt available for current transaction");


            return await nodeInterfaceContractFunction.CallAsync<BigInteger>(BlockNumber);
        }

        public async Task<bool> IsDataAvailable(SignerOrProvider l2Provider, int confirmations = 10)
        {
            var batchConfirmations = await GetBatchConfirmations(l2Provider);
            return Convert.ToInt64(batchConfirmations) > confirmations;
        }

        public static L2TransactionReceipt MonkeyPatchWait(TransactionReceipt contractTransaction)   /////
        {

            return new L2TransactionReceipt(contractTransaction);
        }

        public static RedeemTransaction ToRedeemTransaction(L2TransactionReceipt redeemTx, Web3 l2Provider)
        {
            return new RedeemTransaction(redeemTx, l2Provider);

        }

    }
}
