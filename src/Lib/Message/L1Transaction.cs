using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json.Linq;
using static Arbitrum.Message.L1EthDepositTransactionReceipt;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.Message
{
    public class L1Transaction
    {
    }
    //public class L1ContractTransaction<TReceipt> where TReceipt : L1TransactionReceipt
    //{
    //    Task<TReceipt> Wait(int confirmations = 1);
    //}

    //public class L1EthDepositTransaction : L1ContractTransaction<L1EthDepositTransactionReceipt>
    //{
    //    public async Task<L1EthDepositTransactionReceipt> Wait(int confirmations = 1)
    //    {
    //        // Implement wait logic for L1EthDepositTransactionReceipt
    //        // Example:
    //        return await Task.FromResult(new L1EthDepositTransactionReceipt());
    //    }
    //}

    public class InboxMessageDeliveredEvent : IEventDTO
    {
        public BigInteger MessageNum { get; set; }
        public string? Data { get; set; }
    }

    public class MessageDeliveredEvent : IEventDTO
    {
        public BigInteger MessageIndex { get; set; }
        public string? BeforeInboxAcc { get; set; }
        public string? Inbox { get; set; }
        public int Kind { get; set; }
        public string? Sender { get; set; }
        public string? MessageDataHash { get; set; }
        public BigInteger BaseFeeL1 { get; set; }
        public BigInteger? Timestamp { get; set; }
    }
    public class MessageEvents
    {
        public EventLog<InboxMessageDeliveredEvent>? InboxMessageEvent { get; set; }
        public EventLog<MessageDeliveredEvent>? BridgeMessageEvent { get; set; }
    }

    public class TokenDepositEvent
    {
        public string? L1Token { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public BigInteger? SequenceNumber { get; set; }
        public BigInteger? Amount { get; set; }
    }


    public class L1ContractTransaction : ContractTransactionVO
    {
        public L1ContractTransaction(string contractAddress, string code, Transaction transaction)
            : base(contractAddress, code, transaction)
        {
        }
        public async Task<L1TransactionReceipt> Wait(int confirmations = 0)
        {
            return await Task.FromResult(new L1TransactionReceipt(new TransactionReceipt()));
        }
    }

    public class L1TransactionReceipt : TransactionReceipt
    {

        public L1TransactionReceipt(TransactionReceipt tx) : base()
        {
            To = tx?.To;
            From = tx?.From;
            ContractAddress = tx?.ContractAddress;
            TransactionIndex = tx?.TransactionIndex;
            Root = tx?.Root;
            GasUsed = tx?.GasUsed;
            LogsBloom = tx?.LogsBloom;
            BlockHash = tx?.BlockHash;
            TransactionHash = tx?.TransactionHash;
            Logs = tx?.Logs;
            BlockNumber = tx?.BlockNumber;
            CumulativeGasUsed = tx?.CumulativeGasUsed;
            EffectiveGasPrice = tx?.EffectiveGasPrice;
            Type = tx?.Type;
            Status = tx?.Status;

        }

        public async Task<bool> IsClassic(object l2SignerOrProvider)
        {
            var provider = SignerProviderUtils.GetProviderOrThrow(l2SignerOrProvider);
            var network = await NetworkUtils.GetL2Network(provider);
            return BlockNumber.Value < network.NitroGenesisL1Block;
        }

        public async Task<IEnumerable<EventLog<MessageDeliveredEvent>>> GetMessageDeliveredEvents(Web3 provider)
        {
            return await LogParser.ParseTypedLogs<MessageDeliveredEvent, Contract>(provider, "Bridge", Logs, "MessageDelivered", isClassic: false);
        }

        public async Task<IEnumerable<EventLog<InboxMessageDeliveredEvent>>> GetInboxMessageDeliveredEvents(Web3 provider)
        {
            return await LogParser.ParseTypedLogs<InboxMessageDeliveredEvent, Contract>(provider, "Inbox", Logs, "InboxMessageDelivered", isClassic: false);
        }


        public async Task<IEnumerable<MessageEvents>> GetMessageEvents(Web3 provider)
        {
            // Fetch bridge and inbox messages
            var bridgeMessages = await GetMessageDeliveredEvents(provider);
            var inboxMessages = await GetInboxMessageDeliveredEvents(provider);

            // Converting the events to dictionaries for efficient lookup
            var bridgeMessageDict = bridgeMessages.ToDictionary(m => m.Event.MessageIndex);
            var inboxMessageDict = inboxMessages.ToDictionary(m => m.Event.MessageNum);

            // Check if the counts match
            if (bridgeMessageDict.Count != inboxMessageDict.Count)
            {
                throw new ArbSdkError($"Unexpected missing events. Inbox message count: {inboxMessageDict.Count} does not equal bridge message count: {bridgeMessageDict.Count}.");
            }

            List<MessageEvents> messages = new List<MessageEvents>();

            // Combining bridge and inbox messages
            foreach (var bridgeMessage in bridgeMessageDict.Values)
            {
                if (!inboxMessageDict.TryGetValue(bridgeMessage.Event.MessageIndex, out var inboxMessage))
                {
                    throw new ArbSdkError($"Unexpected missing event for message index: {bridgeMessage.Event.MessageIndex}.");
                }

                messages.Add(new MessageEvents
                {
                    InboxMessageEvent = inboxMessage,
                    BridgeMessageEvent = bridgeMessage
                });
            }
            return messages;
        }

        public async Task<IEnumerable<EthDepositMessage>> GetEthDeposits(Web3 l2Provider)
        {
            var messages = await GetMessageEvents(l2Provider);

            var ethDepositMessages = new List<EthDepositMessage>();

            var ethDepositMessageTasks = messages
                .Where(e => e.BridgeMessageEvent.Event.Kind == (int)InboxMessageKind.L1MessageType_ethDeposit)
                .Select(async m => await EthDepositMessage.FromEventComponents(
                    l2Provider,
                    m.InboxMessageEvent.Event.MessageNum,
                    m.BridgeMessageEvent.Event.Sender,
                    m.InboxMessageEvent.Event.Data
                ));

            foreach (var task in ethDepositMessageTasks)
            {
                ethDepositMessages.Add(await task);
            }

            return ethDepositMessages;
        }

        public async Task<IEnumerable<L1ToL2MessageReaderClassic>> GetL1ToL2MessagesClassic(Web3 l2Provider)
        {
            var network = await NetworkUtils.GetL2Network(l2Provider);
            var chainID = network.ChainID;
            var isClassic = await IsClassic(l2Provider);

            // Throw on nitro events
            if (!isClassic)
            {
                throw new Exception("This method is only for classic transactions. Use 'GetL1ToL2Messages' for nitro transactions.");
            }

            var messageNums = (await GetInboxMessageDeliveredEvents(l2Provider)).Select(msg => msg.Event.MessageNum);

            return messageNums.Select(messageNum =>
                new L1ToL2MessageReaderClassic(
                    l2Provider,
                    chainID,
                    messageNum
                )
            );
        }

        public async Task<IEnumerable<L1ToL2MessageReaderOrWriter>> GetL1ToL2Messages(Web3 l2SignerOrProvider)
        {

            var provider = SignerProviderUtils.GetProviderOrThrow(l2SignerOrProvider);
            var network = await NetworkUtils.GetL2Network(provider);

            var chainID = network.ChainID;
            var isClassic = await IsClassic(provider);

            // Throw on classic events
            if (isClassic)
            {
                throw new Exception("This method is only for nitro transactions. Use 'GetL1ToL2MessagesClassic' for classic transactions.");
            }

            var events = await GetMessageEvents(provider);

            return events
                .Where(e =>
                    e.BridgeMessageEvent.Event.Kind == (int)InboxMessageKind.L1MessageType_submitRetryableTx &&
                    e.BridgeMessageEvent.Event.Inbox.ToLower() == network?.EthBridge?.Inbox?.ToLower())
                .Select(mn =>
                {
                    var inboxMessageData = SubmitRetryableMessageDataParser.Parse(mn.InboxMessageEvent.Event.Data);

                    return L1ToL2Message.FromEventComponents(    
                        l2SignerOrProvider,
                        chainID,
                        mn?.BridgeMessageEvent?.Event?.Sender,
                        mn.InboxMessageEvent.Event.MessageNum,
                        mn.BridgeMessageEvent.Event.BaseFeeL1,
                        inboxMessageData
                    );
                });
        }

        public async Task<IEnumerable<EventLog<TokenDepositEvent>>> GetTokenDepositEvents(Web3 provider)
        {
            return await LogParser.ParseTypedLogs<TokenDepositEvent, Contract>(provider, "L1ERC20Gateway", Logs, "DepositInitiated", isClassic: true);
        }

        public static L1TransactionReceipt MonkeyPatchWait(TransactionReceipt contractTransaction)
        {
            return new L1TransactionReceipt(contractTransaction);
        }

        public static L1EthDepositTransactionReceipt MonkeyPatchEthDepositWait(TransactionReceipt contractTransaction)
        {
            return new L1EthDepositTransactionReceipt(contractTransaction);
        }

        public static L1ContractCallTransactionReceipt MonkeyPatchContractCallWait(TransactionReceipt contractTransaction)
        {
            return new L1ContractCallTransactionReceipt(contractTransaction);
        }
    }

    public class L1EthDepositTransactionReceipt : L1TransactionReceipt
    {
        public L1EthDepositTransactionReceipt(TransactionReceipt tx) : base(tx)
        {
            To = tx?.To;
            From = tx?.From;
            ContractAddress = tx?.ContractAddress;
            TransactionIndex = tx?.TransactionIndex;
            Root = tx?.Root;
            GasUsed = tx?.GasUsed;
            LogsBloom = tx?.LogsBloom;
            BlockHash = tx?.BlockHash;
            TransactionHash = tx?.TransactionHash;
            Logs = tx?.Logs;
            BlockNumber = tx?.BlockNumber;
            //Confirmations = tx.Confirmations;
            CumulativeGasUsed = tx?.CumulativeGasUsed;
            EffectiveGasPrice = tx?.EffectiveGasPrice;
            //Byzantium = tx.Byzantium;
            Type = tx?.Type;
            Status = tx?.Status;
        }
        public async Task<L1EthDepositTransactionReceiptResults> WaitForL2(
            Web3 l2Provider,
            int? confirmations = null,
            int? timeout = null)
        {
            var ethDeposits = await GetEthDeposits(l2Provider);
            if (ethDeposits.Count() == 0)
            {
                throw new ArbSdkError("Unexpected missing Eth Deposit message.");
            }

            var message = ethDeposits.FirstOrDefault();
            if (message == null)
            {
                throw new ArbSdkError("Unexpected missing Eth Deposit message.");
            }

            //var result = await message.Wait(confirmations, timeout);
            TransactionReceipt result = null;

            return new L1EthDepositTransactionReceiptResults()
            {
                Complete = result != null,
                Message = message,
                L2TxReceipt = result
            };
        }
    }

    public class L1ContractCallTransactionReceipt : L1TransactionReceipt
    {
        public L1ContractCallTransactionReceipt(TransactionReceipt tx) : base(tx)
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

        public async Task<L1ContractCallTransactionReceiptResults> WaitForL2(
        Web3 l2SignerOrProvider,
        int? confirmations = null,
        int? timeout = null)
        {
            var messages = await GetL1ToL2Messages(l2SignerOrProvider);
            var message = messages.FirstOrDefault();
            if (message == null)
            {
                throw new ArbSdkError("Unexpected missing L1ToL2 message.");
            }
            var res = await message.WaitForStatus(confirmations, timeout);

            return new L1ContractCallTransactionReceiptResults
            {
                Complete = res.Status == L1ToL2MessageStatus.REDEEMED,
                Message = message,
                Result = res
            };
        }

    }
}
