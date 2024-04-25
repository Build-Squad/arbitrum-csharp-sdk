using Arbitrum.Message;
using Arbitrum.Utils;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Arbitrum.Inbox
{
    public class ForceInclusionParams
    {
        public FetchedEvent<MessageDeliveredEvent>? Event { get; set; }
        public string? DelayedAcc { get; set; }
    }
    public class GasComponentsWithL2Part
    {
        public BigInteger? GasEstimate { get; set; }
        public BigInteger? GasEstimateForL1 { get; set; }
        public BigInteger? BaseFee { get; set; }
        public BigInteger? L1BaseFeeEstimate { get; set; }
        public BigInteger? GasEstimateForL2 { get; set; }
    }
    public class InboxTools
    {
        private readonly Web3 _l1Provider;
        private readonly Network _l1Network;
        public InboxTools(Account l1Signer, L2Network l2Network)
        {
            _l1Provider = SignerProviderUtils.GetProviderOrThrow(l1Signer);
            _l1Network = NetworkUtils.l1Networks[l2Network.PartnerChainID];
        }

        private async Task<BlockWithTransactions> FindFirstBlockBelow(int blockNumber, int blockTimestamp)
        {
            var block = await _l1Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(blockNumber));
            var diff = block.Timestamp.Value - blockTimestamp;
            if (diff < 0) return block;

            var diffBlocks = Math.Max((int)Math.Ceiling((double)diff / _l1Network.BlockTime), 10);

            return await FindFirstBlockBelow(blockNumber - diffBlocks, blockTimestamp);
        }

        private bool IsContractCreation(TransactionRequest transactionl2Request)
        {
            return string.IsNullOrEmpty(transactionl2Request.To) || transactionl2Request.To == "0x" || transactionl2Request.To == "0x0000000000000000000000000000000000000000";
        }

        private async Task<GasComponentsWithL2Part> EstimateArbitrumGas(TransactionRequest transactionl2Request, Web3 l2Provider)
        {
            var nodeInterface = "";
            var contractCreation = IsContractCreation(transactionl2Request);
            var gasComponents = (
                transactionl2Request.To ?? "0x0000000000000000000000000000000000000000",
                contractCreation,
                transactionl2Request.Data,
                new CallInput()
                {
                    From = transactionl2Request.From,
                    Value = transactionl2Request.Value
                });

            var gasEstimateForL2 = gasComponents.GasEstimate - gasComponents.GasEstimateForL1;
            return new GasComponentsWithL2Part
            {
                GasEstimate = gasComponents.GasEstimate,
                GasEstimateForL1 = gasComponents.GasEstimateForL1,
                BaseFee = gasComponents.BaseFee,
                L1BaseFeeEstimate = gasComponents.L1BaseFeeEstimate,
                GasEstimateForL2 = gasEstimateForL2
            };
        }

        private async Task<(int, int)> GetForceIncludableBlockRange(int blockNumberRangeSize)
        {
            var sequencerInbox = "";
            var multicall = MultiCaller.FromProvider(_l1Provider);
            var multicallInput = new JArray(
                sequencerInbox.MaxTimeVariation.GetInput<SequencerInbox.MaxTimeVariationFunction>(),
                multicall.GetBlockNumberInput(),
                multicall.GetCurrentBlockTimestampInput()
            );

            var multicallOutput = await multicall.MultiCall<JArray>(multicallInput, true);
            var maxTimeVariation = multicallOutput[0].ToObject<int>();
            var currentBlockNumber = multicallOutput[1].ToObject<int>();
            var currentBlockTimestamp = multicallOutput[2].ToObject<int>();

            var firstEligibleBlockNumber = currentBlockNumber - maxTimeVariation;
            var firstEligibleTimestamp = currentBlockTimestamp - maxTimeVariation;

            var firstEligibleBlock = await FindFirstBlockBelow(firstEligibleBlockNumber, firstEligibleTimestamp);

            return (firstEligibleBlockNumber, firstEligibleBlockNumber - blockNumberRangeSize);
        }

        private async Task<List<FetchedEvent<MessageDeliveredEvent>>> GetEventsAndIncreaseRange(Bridge bridge, int searchRangeBlocks, int maxSearchRangeBlocks, int rangeMultiplier)
        {
            var eFetcher = new EventFetcher(_l1Provider);
            var cappedSearchRangeBlocks = Math.Min(searchRangeBlocks, maxSearchRangeBlocks);
            var blockRange = await GetForceIncludableBlockRange(cappedSearchRangeBlocks);
            var events = await eFetcher.GetEvents<Bridge>(bridge, b => b.Filters.MessageDelivered(), blockRange.Item2, blockRange.Item1);

            if (events.Count() != 0) return events;
            else if (cappedSearchRangeBlocks == maxSearchRangeBlocks) return new List<FetchedEvent<MessageDeliveredEvent>>();
            else
            {
                return await GetEventsAndIncreaseRange(bridge, searchRangeBlocks * rangeMultiplier, maxSearchRangeBlocks, rangeMultiplier);
            }
        }

        public async Task<ForceInclusionParams?> GetForceIncludableEvent(int maxSearchRangeBlocks = 3 * 6545, int startSearchRangeBlocks = 100, int rangeMultiplier = 2)
        {
            var bridge = new Bridge(_l2Network.EthBridge.Bridge, _l1Provider);
            var events = await GetEventsAndIncreaseRange(bridge, startSearchRangeBlocks, maxSearchRangeBlocks, rangeMultiplier);

            if (events.Count == 0) return null;

            var eventInfo = events[^1];
            var sequencerInbox = new SequencerInbox(_l2Network.EthBridge.SequencerInbox, _l1Provider);
            var totalDelayedRead = await sequencerInbox.TotalDelayedMessagesRead();
            if (totalDelayedRead > eventInfo.Event.MessageIndex) return null;

            var delayedAcc = await bridge.DelayedInboxAccs(eventInfo.Event.MessageIndex);
            return new ForceInclusionParams { Event = eventInfo, DelayedAcc = delayedAcc };
        }

        public async Task<ContractTransaction?> ForceInclude(ForceInclusionParams? messageDeliveredEvent = null, Overrides? overrides = null)
        {
            var sequencerInbox = new SequencerInbox(_l2Network.EthBridge.SequencerInbox, _l1Provider);
            var eventInfo = messageDeliveredEvent ?? await GetForceIncludableEvent();

            if (eventInfo == null) return null;
            var block = await _l1Provider.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(eventInfo.Event!.BlockHash);

            return await sequencerInbox.ForceInclusion.SendTransactionAsync(eventInfo.Event.MessageIndex + 1, eventInfo.Event.Kind,
                new List<object> { eventInfo.Event.BlockNumber, block.Timestamp, }, eventInfo.Event.BaseFeeL1,
                eventInfo.Event.Sender, eventInfo.Event.MessageDataHash, overrides ?? new Overrides());
        }

        public async Task<ContractTransaction?> SendL2SignedTx(string signedTx)
        {
            var delayedInbox = new IInbox(_l2Network.EthBridge.Inbox, _l1Provider);
            var sendData = new[] { (byte)InboxMessageKind.L2MessageType_signedTx, signedTx };

            return await delayedInbox.SendL2Message.SendTransactionAsync(sendData);
        }

        public async Task<string> SignL2Tx(TransactionRequest txRequest, Signer l2Signer)
        {
            var tx = new TransactionRequest
            {
                Data = txRequest.Data,
                Value = txRequest.Value,
                To = txRequest.To,
                Gas = txRequest.Gas,
                GasPrice = txRequest.GasPrice,
                MaxFeePerGas = txRequest.MaxFeePerGas,
                MaxPriorityFeePerGas = txRequest.MaxPriorityFeePerGas,
                Nonce = txRequest.Nonce,
                ChainId = txRequest.ChainId,
                Type = txRequest.Type,
                From = await l2Signer.GetAddress(),
            };

            if (string.IsNullOrEmpty(tx.To)) tx.To = "0x0000000000000000000000000000000000000000";

            if (tx.Type == 1 || tx.GasPrice != null)
            {
                if (tx.GasPrice == null) tx.GasPrice = await l2Signer.GetGasPrice();
            }
            else
            {
                if (tx.MaxFeePerGas == null)
                {
                    var feeData = await l2Signer.GetFeeData();
                    tx.MaxPriorityFeePerGas = feeData.MaxPriorityFeePerGas;
                    tx.MaxFeePerGas = feeData.MaxFeePerGas;
                }
                tx.Type = 2;
            }

            if (tx.Nonce == null) tx.Nonce = await l2Signer.GetTransactionCount();

            if (tx.To == "0x0000000000000000000000000000000000000000") tx.To = null;

            if (tx.To == null) tx.Gas = await EstimateArbitrumGas(tx, l2Signer.Provider!).Result.GasEstimateForL2;

            return await l2Signer.SignTransaction(tx);
        }
    }
