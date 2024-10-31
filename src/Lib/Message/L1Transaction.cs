using Arbitrum.ContractFactory;
using Arbitrum.ContractFactory.Bridge;
using Arbitrum.ContractFactory.L1ERC20Gateway;
using Arbitrum.DataEntities;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.Message
{
    public class MessageEvents
    {
        public EventLog<InboxMessageDeliveredEventDTO>? InboxMessageEvent { get; set; }
        public EventLog<MessageDeliveredEventDTO>? BridgeMessageEvent { get; set; }
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

        public async Task<IEnumerable<EventLog<MessageDeliveredEventDTO>>> GetMessageDeliveredEvents(Web3 provider, string address)
        {
            return LogParser.ParseTypedLogs<MessageDeliveredEventDTO>(provider, Logs, address);
        }

        public async Task<IEnumerable<EventLog<InboxMessageDeliveredEventDTO>>> GetInboxMessageDeliveredEvents(Web3 provider, string address)
        {
            return LogParser.ParseTypedLogs<InboxMessageDeliveredEventDTO>(provider, Logs, address);
        }

        public async Task<IEnumerable<MessageEvents>> GetMessageEvents(Web3 provider, string? address = null)
        {
            var bridgeMessages = await GetMessageDeliveredEvents(provider, address);
            var inboxMessages = await GetInboxMessageDeliveredEvents(provider, address);

            var bridgeMessageDict = bridgeMessages
                .GroupBy(m => m.Event.MessageIndex)
                .ToDictionary(g => g.Key, g => g.First());

            var inboxMessageDict = inboxMessages
                .GroupBy(m => m.Event.MessageNum)
                .ToDictionary(g => g.Key, g => g.First());

            if (bridgeMessageDict.Count != inboxMessageDict.Count)
            {
                throw new ArbSdkError($"Unexpected missing events. Inbox message count: {inboxMessageDict.Count} does not equal bridge message count: {bridgeMessageDict.Count}.");
            }

            var messages = new List<MessageEvents>();

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
                    m.InboxMessageEvent.Event.Data.ToHex()
                ));

            foreach (var task in ethDepositMessageTasks)
            {
                ethDepositMessages.Add(await task);
            }

            return ethDepositMessages;
        }

        public async Task<IEnumerable<L1ToL2MessageReaderClassic>> GetL1ToL2MessagesClassic(Web3 l2Provider, string address)
        {
            var network = await NetworkUtils.GetL2Network(l2Provider);
            var chainID = network.ChainID;
            var isClassic = await IsClassic(l2Provider);

            if (!isClassic)
            {
                throw new Exception("This method is only for classic transactions. Use 'GetL1ToL2Messages' for nitro transactions.");
            }

            var messageNums = (await GetInboxMessageDeliveredEvents(l2Provider, address)).Select(msg => msg.Event.MessageNum);

            return messageNums.Select(messageNum =>
                new L1ToL2MessageReaderClassic(
                    l2Provider,
                    chainID,
                    messageNum
                )
            );
        }

        public async Task<IEnumerable<L1ToL2MessageReaderOrWriter>> GetL1ToL2Messages(dynamic l2SignerOrProvider, string? address = null)
        {
            var provider = SignerProviderUtils.GetProviderOrThrow(l2SignerOrProvider);
            var network = await NetworkUtils.GetL2Network(provider);

            var chainID = network.ChainID;
            var isClassic = await IsClassic(provider);

            if (isClassic)
            {
                throw new Exception("This method is only for nitro transactions. Use 'GetL1ToL2MessagesClassic' for classic transactions.");
            }

            var events = await GetMessageEvents(provider, address);

            return ((IEnumerable<MessageEvents>)events)
                .Where(e =>
                    e.BridgeMessageEvent.Event.Kind == (int)InboxMessageKind.L1MessageType_submitRetryableTx &&
                    e.BridgeMessageEvent.Event.Inbox.ToLower() == network?.EthBridge?.Inbox?.ToLower())
                .Select((Func<MessageEvents, L1ToL2MessageReaderOrWriter>)(mn =>
                {
                    var inboxMessageData = SubmitRetryableMessageDataParser.Parse(mn.InboxMessageEvent.Event.Data.ToHex());

                    return L1ToL2Message.FromEventComponents(
                        l2SignerOrProvider,
                        chainID,
                        mn?.BridgeMessageEvent?.Event?.Sender,
                        mn.InboxMessageEvent.Event.MessageNum,
                        mn.BridgeMessageEvent.Event.BaseFeeL1,
                        inboxMessageData
                    );
                }));
        }

        public async Task<IEnumerable<EventLog<DepositInitiatedEventDTO>>> GetTokenDepositEvents(Web3 provider, string address)
        {
            return LogParser.ParseTypedLogs<DepositInitiatedEventDTO>(provider, Logs, address);
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
            CumulativeGasUsed = tx?.CumulativeGasUsed;
            EffectiveGasPrice = tx?.EffectiveGasPrice;
            Type = tx?.Type;
            Status = tx?.Status;
        }

        public async Task<L1EthDepositTransactionReceiptResults> WaitForL2(Web3 l2Provider, int? confirmations = null, int? timeout = null)
        {
            var ethDeposits = await GetEthDeposits(l2Provider);

            if (!ethDeposits.Any())
            {
                throw new ArbSdkError("Unexpected missing Eth Deposit message.");
            }

            var message = ethDeposits.FirstOrDefault() ?? throw new ArbSdkError("Unexpected missing Eth Deposit message.");

            var result = await message.Wait(confirmations, timeout);

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
            CumulativeGasUsed = tx.CumulativeGasUsed;
            EffectiveGasPrice = tx.EffectiveGasPrice;
            Type = tx.Type;
            Status = tx.Status;
        }

        public async Task<L1ContractCallTransactionReceiptResults> WaitForL2(
        dynamic l2SignerOrProvider,
        int? confirmations = null,
        int? timeout = null)
        {
            IEnumerable<L1ToL2MessageReaderOrWriter> messages = await GetL1ToL2Messages(l2SignerOrProvider)
                           ?? throw new ArbSdkError("Unexpected missing L1ToL2 message.");

            var res = await messages.FirstOrDefault().WaitForStatus(confirmations, timeout);

            return new L1ContractCallTransactionReceiptResults
            {
                Complete = res.Status == L1ToL2MessageStatus.REDEEMED,
                Message = messages.FirstOrDefault(),
                Result = res
            };
        }

    }
}
