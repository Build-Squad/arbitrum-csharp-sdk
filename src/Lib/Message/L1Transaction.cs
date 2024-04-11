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
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
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

    public class InboxMessageDeliveredEvent 
    {
        public BigInteger MessageNum { get; set; }
        public string Data { get; set; }
    }

    public class MessageDeliveredEvent 
    {
        public BigInteger MessageIndex { get; set; }
        public string BeforeInboxAcc { get; set; }
        public string Inbox { get; set; }
        public int Kind { get; set; }
        public string Sender { get; set; }
        public string MessageDataHash { get; set; }
        public BigInteger BaseFeeL1 { get; set; }
        public BigInteger Timestamp { get; set; }
    }
    public class MessageEvents
    {
        public InboxMessageDeliveredEvent InboxMessageEvent { get; set; }
        public MessageDeliveredEvent BridgeMessageEvent { get; set; }
    }
    public class L1TransactionReceipt : TransactionReceipt
    {
        public new string To { get; set; }
        public new string From { get; set; }
        public new string ContractAddress { get; set; }
        public new BigInteger TransactionIndex { get; set; }
        public new string Root { get; set; }
        public new BigInteger GasUsed { get; set; }
        public new string LogsBloom { get; set; }
        public new string BlockHash { get; set; }
        public new string TransactionHash { get; set; }
        public new JArray Logs { get; set; }
        public new BigInteger BlockNumber { get; set; }
        //public int Confirmations { get; set; }
        public new BigInteger CumulativeGasUsed { get; set; }
        public new BigInteger EffectiveGasPrice { get; set; }
        //public bool Byzantium { get; set; }
        public new BigInteger Type { get; set; }
        public new BigInteger? Status { get; set; }

        public L1TransactionReceipt(TransactionReceipt tx) : base()
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

        public async Task<bool> IsClassic(object l2SignerOrProvider)    ////////
        {
            var provider = SignerProviderUtils.GetProviderOrThrow(l2SignerOrProvider);
            var network = await NetworkUtils.GetL2NetworkAsync(provider);
            return BlockNumber < network.NitroGenesisL1Block;
        }

        public async Task<FilterLog[]> GetMessageDeliveredEvents(Web3 provider)
        {
            return await LogParser.ParseTypedLogs(provider, "Bridge", Logs, "InboxMessageDelivered", isClassic: false);
        }

        public async Task<FilterLog[]> GetInboxMessageDeliveredEvents(Web3 provider)
        {
            return await LogParser.ParseTypedLogs(provider, "Inbox", Logs, "InboxMessageDelivered", isClassic: false);
        }


        public async Task<IEnumerable<MessageEvents>> GetMessageEvents(Web3 provider)
        {
            // Fetch bridge and inbox messages
            var bridgeMessages = await GetMessageDeliveredEvents(provider);
            var inboxMessages = await GetInboxMessageDeliveredEvents(provider);

            // Check if the counts match
            if (bridgeMessages.Count() != inboxMessages.Count())
            {
                throw new ArbSdkError($"Unexpected missing events. Inbox message count: {inboxMessages.Count()} does not equal bridge message count: {bridgeMessages.Count()}.");
            }

            List<MessageEvents> messages = new List<MessageEvents>();

            // Combine bridge and inbox messages
            foreach (var bm in bridgeMessages)
            {
                var im = inboxMessages.FirstOrDefault(i => i.MessageNum == bm.MessageIndex);
                if (im == null)
                {
                    throw new ArbSdkError($"Unexpected missing event for message index: {bm.MessageIndex}.");
                }

                messages.Add(new MessageEvents
                {
                    InboxMessageEvent = im,
                    BridgeMessageEvent = bm
                });
            }
            return messages;
        }

        public async Task<IEnumerable<EthDepositMessage>> GetEthDeposits(Web3 l2Provider)
        {
            var messages = await GetMessageEvents(l2Provider);

            var ethDepositMessages = new List<EthDepositMessage>();

            var ethDepositMessageTasks = messages
                .Where(e => e.BridgeMessageEvent.Kind == (int)InboxMessageKind.L1MessageType_ethDeposit)
                .Select(async m => await EthDepositMessage.FromEventComponents(
                    l2Provider,
                    m.InboxMessageEvent.MessageNum,
                    m.BridgeMessageEvent.Sender,
                    m.InboxMessageEvent.Data
                ));

            foreach (var task in ethDepositMessageTasks)
            {
                ethDepositMessages.Add(await task);
            }

            return ethDepositMessages;
        }

        public async Task<IEnumerable<L1ToL2MessageReaderClassic>> GetL1ToL2MessagesClassic(Web3 l2Provider)
        {
            var network = await NetworkUtils.GetL2NetworkAsync(l2Provider);
            var chainID = network.ChainID;
            var isClassic = await IsClassic(l2Provider);

            // Throw on nitro events
            if (!isClassic)
            {
                throw new Exception("This method is only for classic transactions. Use 'GetL1ToL2Messages' for nitro transactions.");
            }

            var messageNums = (await GetInboxMessageDeliveredEvents(l2Provider)).Select(msg => msg.MessageNum);

            return messageNums.Select(messageNum =>
                new L1ToL2MessageReaderClassic(
                    l2Provider,
                    chainID,
                    messageNum
                )
            );
        }

        public async Task<IEnumerable<L1ToL2Message>> GetL1ToL2Messages<T>(T l2SignerOrProvider) where T : SignerOrProvider
        {

            var provider = SignerProviderUtils.GetProviderOrThrow(l2SignerOrProvider);
            var network = await NetworkUtils.GetL2NetworkAsync(provider);
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
                    e.BridgeMessageEvent.Kind == (int)InboxMessageKind.L1MessageType_submitRetryableTx &&
                    e.BridgeMessageEvent.Inbox.ToLower() == network?.EthBridge?.Inbox?.ToLower())
                .Select(mn =>
                {
                    var messageDataParser = new SubmitRetryableMessageDataParser();
                    var inboxMessageData = SubmitRetryableMessageDataParser.Parse(mn.InboxMessageEvent.Data);

                    return L1ToL2Message.FromEventComponents<T>(      ////////////////
                        l2SignerOrProvider,
                        chainID,
                        mn.BridgeMessageEvent.Sender,
                        mn.InboxMessageEvent.MessageNum,
                        mn.BridgeMessageEvent.BaseFeeL1,
                        inboxMessageData
                    );
                });
        }

        public async Task<IEnumerable> GetTokenDepositEvents(Web3 provider)
        {
            return await LogParser.ParseTypedLogs(provider, "L1ERC20Gateway", Logs, "DepositInitiated", isClassic: true);
        }

        public static L1TransactionReceipt MonkeyPatchWait(TransactionReceipt contractTransaction)
        {
            return new L1TransactionReceipt(contractTransaction);
        }

        public static L1TransactionReceipt MonkeyPatchEthDepositWait(TransactionReceipt contractTransaction)
        {
            return new L1EthDepositTransactionReceipt(contractTransaction);
        }

        public static L1TransactionReceipt MonkeyPatchContractCallWait(TransactionReceipt contractTransaction)
        {
            return new L1ContractCallTransactionReceipt(contractTransaction);
        }
    }

    public class L1EthDepositTransactionReceipt : L1TransactionReceipt
    {
        public L1EthDepositTransactionReceipt(TransactionReceipt tx) : base(tx)
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

            var result = await message.Wait(confirmations, timeout);

            return new L1EthDepositTransactionReceiptResults()
            {
                Complete = result != null,
                Message = message,
                L2TxReceipt = result
            };
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

            public async Task<L1ContractCallTransactionReceiptResults> WaitForL2<T>(
            T l2SignerOrProvider,
            int? confirmations = null,
            int? timeout = null) where T : SignerOrProvider
            {
                var messages = await GetL1ToL2Messages(l2SignerOrProvider);
                var message = messages.FirstOrDefault() as L1ToL2MessageReader;
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
}
