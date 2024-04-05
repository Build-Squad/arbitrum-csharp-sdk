﻿using Arbitrum.DataEntities;
using Arbitrum.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.Standards.ERC20.TokenList;
using Nethereum.Web3.Accounts.Managed;
using System.Transactions;
using static Arbitrum.Message.L2ToL1MessageReaderNitro;
using System.Xml;
using Nethereum.Contracts;
using Nethereum.ABI;
using Nethereum.JsonRpc.Client;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using ADRaffy.ENSNormalize;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto.Tls;

namespace Arbitrum.Message
{
    public static class CacheUtils
    {
        // Constants
        public const int ASSERTION_CREATED_PADDING = 50;
        public const int ASSERTION_CONFIRMED_PADDING = 20;

        private static Dictionary<string, object> _l2BlockRangeCache = new Dictionary<string, object>();
        private static object _lock = new object();

        public static string GetL2BlockRangeCacheKey(string l2ChainId, int l1BlockNumber)
        {
            return $"{l2ChainId}-{l1BlockNumber}";
        }

        public static void SetL2BlockRangeCache(string key, object value)
        {
            lock (_lock)
            {
                _l2BlockRangeCache[key] = value;
            }
        }

        public static async Task<object> GetBlockRangesForL1BlockWithCache(Web3 l1Provider, Web3 l2Provider, int forL1Block)
        {
            string l2ChainId = (await l2Provider.Eth.ChainId.SendRequestAsync()).ToString();
            string key = GetL2BlockRangeCacheKey(l2ChainId, forL1Block);

            lock (_lock)
            {
                if (_l2BlockRangeCache.ContainsKey(key))
                {
                    return _l2BlockRangeCache[key];
                }
            }

            // If not found in cache, obtain the block ranges
            var l2BlockRange = await Lib.GetBlockRangesForL1Block(l1Provider, forL1Block);

            // Cache the result
            lock (_lock)
            {
                _l2BlockRangeCache[key] = l2BlockRange;
            }

            return l2BlockRange;
        }
    }

    public class L2BlockRangeCache
    {
        private readonly Dictionary<string, List<int?>> _cache = new Dictionary<string, List<int?>>();

        public string GetCacheKey(int l2ChainId, int l1BlockNumber)
        {
            return $"{l2ChainId}-{l1BlockNumber}";
        }

        public void SetCache(string key, List<int?> value)
        {
            _cache[key] = value;
        }

        public List<int?> GetCache(string key)
        {
            return _cache.TryGetValue(key, out var value) ? value : new List<int?>();
        }
    }

    public class Mutex
    {
        public async Task<IDisposable> Acquire()
        {
            // Implement mutex acquisition
            return await Task.FromResult<IDisposable>(new Lock());
        }

        private class Lock : IDisposable
        {
            public void Dispose()
            {
                // Implement mutex release
            }
        }
    }
    public class L2ToL1TxEvent
    {
        public string Caller { get; set; }
        public string Destination { get; set; }
        public BigInteger Hash { get; set; }
        public BigInteger Position { get; set; }
        public BigInteger ArbBlockNum { get; set; }
        public BigInteger EthBlockNum { get; set; }
        public BigInteger Timestamp { get; set; }
        public BigInteger CallValue { get; set; }
        public string Data { get; set; }
    }

    public class L2ToL1MessageNitro
    {
        public L2ToL1TransactionEvent Event { get; set; }
        protected L2ToL1MessageNitro(L2ToL1TransactionEvent l2ToL1TransactionEvent)
        {
            Event = l2ToL1TransactionEvent;
        }

        public static L2ToL1MessageNitro FromEvent<T>(
            T l1SignerOrProvider,
            L2ToL1TransactionEvent l2ToL1TransactionEvent,
            Web3 l1Provider = null) where T : SignerOrProvider
        {
            if (SignerProviderUtils.IsSigner(l1SignerOrProvider))
            {
                return new L2ToL1MessageWriterNitro(l1SignerOrProvider, l2ToL1TransactionEvent, l1Provider);
            }
            else
            {
                return new L2ToL1MessageReaderNitro(l1SignerOrProvider.Provider, l2ToL1TransactionEvent);
            }
        }

        public static async Task<List<L2ToL1TransactionEvent[]>> GetL2ToL1Events(
            Web3 l2Provider,
            NewFilterInput filter,
            BigInteger? position = null,
            string? destination = null,
            BigInteger? hash = null)
        {
            var eventFetcher = new EventFetcher(l2Provider);

            var argumentFilters = new Dictionary<string, object>();

            if (position != default)
            {
                argumentFilters["position"] = position;
            }
            if (destination != null)
            {
                argumentFilters["destination"] = destination;
            }
            if (hash != default)
            {
                argumentFilters["hash"] = hash;
            }

            var events = new List<L2ToL1TransactionEvent[]>();

            var eventList = await eventFetcher.GetEventsAsync(
                contractFactory: "ArbSys",
                eventName: "L2ToL1Tx",
                argumentFilters: argumentFilters,
                filter: new NewFilterInput
                {
                    FromBlock = filter.FromBlock,
                    ToBlock = filter.ToBlock,
                    Address = new string[] { Constants.ARB_SYS_ADDRESS }
                },
                isClassic: false
                );

            foreach (var ev in eventList)
            {
                var item = ev.Event;
                events.Add(item);
            }

            return events;
        }
    }

    public class L2ToL1MessageReaderNitro : L2ToL1MessageNitro
    {
        protected string? SendRootHash { get; set; }
        protected BigInteger SendRootSize { get; set; }
        protected bool SendRootConfirmed { get; set; }
        protected string? OutboxAddress { get; set; }
        protected int? L1BatchNumber { get; set; }

        protected readonly Web3 l1Provider;

        public L2ToL1MessageReaderNitro(Web3 l1Provider, L2ToL1TransactionEvent l2ToL1TransactionEvent) : base(l2ToL1TransactionEvent)
        {
            this.l1Provider = l1Provider;
        }

        public async Task<byte[]> GetOutboxProof(Web3 l2Provider)
        {
            var sendProps = await GetSendProps(l2Provider);
            if (sendProps.SendRootSize == 0)
            {
                throw new ArbSdkError("Node not yet created, cannot get proof.");
            }


            var nodeInterface = LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    provider: l2Provider,
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    isClassic: false);

            var outboxProofParams = await nodeInterface.GetFunction("constructOutboxProof").CallAsync<byte[]>(
                                        sendProps.sendRootSize, Event.Position);

            return outboxProofParams.proof;
        }

        protected async Task<bool> HasExecuted(Web3 l2Provider)
        {
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);

            var outboxContract = LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "Outbox",
                address: l2Network?.EthBridge?.Outbox,
                isClassic: false
                );

            var outboxContractFunction = outboxContract.GetFunction("isSpent");

            return await outboxContractFunction.CallAsync<bool>(Event.Position);
        }

        public async Task<L2ToL1MessageStatus> GetStatus(Web3 l2Provider)
        {
            var sendProps = await GetSendProps(l2Provider);
            if (!sendProps.sendRootConfirmed)
            {
                return L2ToL1MessageStatus.UNCONFIRMED;
            }

            return await HasExecuted(l2Provider) ? L2ToL1MessageStatus.EXECUTED : L2ToL1MessageStatus.CONFIRMED;
        }

        public Dictionary<string, string> ParseNodeCreatedAssertion(FetchedEvent<NodeCreatedEvent> nodeCreatedEvent)
        {
            var assertion = nodeCreatedEvent.Event.Assertion;
            var afterState = assertion.AfterState.GlobalState;
            var blockHash = afterState.Bytes32Vals[0];
            var sendRoot = afterState.Bytes32Vals[1];

            return new Dictionary<string, string>
            {
                { "blockHash", blockHash },
                { "sendRoot", sendRoot }
            };
        }

        private async Task<ArbBlock> GetBlockFromNodeLog(Web3 l2Provider, FetchedEvent<NodeCreatedEvent> log)
        {
            var arbitrumProvider = new ArbitrumProvider(l2Provider);

            if (log == null)
            {
                Console.WriteLine("No NodeCreated events found, defaulting to block 0");
                return await arbitrumProvider.GetBlock(new HexBigInteger(BigInteger.Zero));   /////////
            }

            var parsedLog = ParseNodeCreatedAssertion(log);

            var l2Block = await arbitrumProvider.GetBlock(new HexBigInteger(parsedLog.TryGetValue("blockHash", out var value) ? value : null));
            if (l2Block == null)
            {
                throw new ArbSdkError($"Block not found. {parsedLog["blockHash"]}");
            }
            if (l2Block.SendRoot != parsedLog["sendRoot"])
            {
                throw new ArbSdkError($"L2 block send root doesn't match parsed log. {l2Block.SendRoot} {parsedLog["sendRoot"]}");
            }
            return l2Block;
        }

        private async Task<ArbBlock> GetBlockFromNodeNum(dynamic rollup, BigInteger nodeNum, Web3 l2Provider)   /////
        {
            // Get node details
            var node = (await rollup.GetNode(nodeNum)).createdAtBlock;
            var formattedNode = LoadContractUtils.FormatContractOutput(rollup, "getNode", node);

            // Get created at block
            var createdAtBlock = BigInteger.Parse(formattedNode["createdAtBlock"]);

            // Set created block range
            BigInteger createdFromBlock = createdAtBlock;
            BigInteger createdToBlock = createdAtBlock;


            if (await Lib.IsArbitrumChain(l1Provider))
            {
                try
                {
                    // Get L2 block range for the given L1 block
                    var l2BlockRange = await CacheUtils.GetBlockRangesForL1BlockWithCache(l1Provider, l2Provider, createdAtBlock);

                    BigInteger? startBlock = l2BlockRange.startBlock;
                    BigInteger? endBlock = l2BlockRange.endBlock;

                    if (startBlock == null || endBlock == null)
                    {
                        throw new Exception();
                    }

                    createdFromBlock = startBlock.Value;
                    createdToBlock = endBlock.Value;
                }
                catch (Exception ex)
                {
                    createdFromBlock = createdAtBlock;
                    createdToBlock = createdAtBlock;
                }
            }

            var eventFetcher = new EventFetcher(rollup.Provider);
            var argumentFilters = new Dictionary<string, object> { { "nodeNum", nodeNum } };

            var logs = await eventFetcher.GetEventsAsync(
            contractFactory: rollup,
            eventName: "NodeCreated",
            argumentFilters: argumentFilters,
            filter: new NewFilterInput
            {
                FromBlock = new BlockParameter(createdFromBlock.ToHexBigInteger()),
                ToBlock = new BlockParameter(createdToBlock.ToHexBigInteger()),
                Address = rollup.Address
            }
        );

            if (logs.Count() > 1)
            {
                throw new ArbSdkError($"Unexpected number of NodeCreated events. Expected 0 or 1, got {logs.Count()}.");
            }

            return await GetBlockFromNodeLog(l2Provider, logs.FirstOrDefault());
        }

        public async Task<BigInteger?> GetBatchNumber(Web3 l2Provider)
        {
            var res = BigInteger.Zero;
            if (L1BatchNumber == null)
            {
                try
                {
                    var nodeInterfaceContract = LoadContractUtils.LoadContract(
                        provider: l2Provider,
                        contractName: "NodeInterface",
                        address: Constants.NODE_INTERFACE_ADDRESS,
                        isClassic: false
                    );

                    res = await nodeInterfaceContract.GetFunction("findBatchContainingBlock").CallAsync<BigInteger>(Event.ArbBlockNum);
                    L1BatchNumber = (int)res;
                }
                catch (Exception)
                {
                    // do nothing - errors are expected here
                }
            }
            return L1BatchNumber;
        }

        protected async Task<object> GetSendProps(Web3 l2Provider)
        {
            if (!SendRootConfirmed)
            {
                var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);

                var rollupContract = LoadContractUtils.LoadContract(
                    provider: l2Provider,
                    contractName: "RollupUserLogic",
                    address: l2Network?.EthBridge?.Rollup,
                    isClassic: false
                    );


                var latestConfirmedNodeNum = await rollupContract.GetFunction("latestConfirmed").CallAsync<BigInteger>();
                var l2BlockConfirmed = await GetBlockFromNodeNum(rollupContract, latestConfirmedNodeNum, l2Provider);

                var sendRootSizeConfirmed = l2BlockConfirmed.SendCount;
                if (sendRootSizeConfirmed > Event.Position)
                {
                    SendRootSize = sendRootSizeConfirmed;
                    SendRootHash = l2BlockConfirmed.SendRoot;
                    SendRootConfirmed = true;
                }
                else
                {
                    // if the node has yet to be confirmed we'll still try to find proof info from unconfirmed nodes
                    var latestNodeNum = await rollupContract.GetFunction("latestNodeCreated").CallAsync<BigInteger>();
                    if (latestNodeNum > latestConfirmedNodeNum)
                    {
                        var l2Block = await GetBlockFromNodeNum(rollupContract, latestNodeNum, l2Provider);

                        var sendRootSize = l2Block.SendCount;
                        if (sendRootSize > Event.Position)
                        {
                            SendRootSize = sendRootSize;
                            SendRootHash = l2Block.SendRoot;
                        }
                    }
                }
            }
            return new
            {
                sendRootSize = SendRootSize,
                sendRootHash = SendRootHash,
                sendRootConfirmed = SendRootConfirmed
            };
        }

        public async Task<L2ToL1MessageStatus> WaitUntilReadyToExecute(Web3 l2Provider, int retryDelay = 500)
        {
            var status = await GetStatus(l2Provider);
            if (status == L2ToL1MessageStatus.CONFIRMED || status == L2ToL1MessageStatus.EXECUTED)
            {
                return status;
            }
            else
            {
                await Task.Delay(retryDelay);
                return await WaitUntilReadyToExecute(l2Provider, retryDelay);
            }
        }

        public async Task<BigInteger?> GetFirstExecutableBlock(Web3 l2Provider)
        {
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);

            var rollupContract = LoadContractUtils.LoadContract(
                provider: l1Provider,
                contractName: "RollupUserLogic",
                address: l2Network?.EthBridge?.Rollup,
                isClassic: false
                );

            var status = await GetStatus(l2Provider);
            if (status == L2ToL1MessageStatus.EXECUTED)
            {
                return null;
            }
            if (status == L2ToL1MessageStatus.CONFIRMED)
            {
                return null;
            }

            if (status != L2ToL1MessageStatus.UNCONFIRMED)
            {
                throw new ArbSdkError("L2ToL1Msg expected to be unconfirmed");
            }

            var latestBlock = await l1Provider.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            // Convert HexBigInteger to BigInteger
            int latestBlockNumber = (int)latestBlock.Value;

            var eventFetcher = new EventFetcher(l1Provider);


            var argumentFilters = new Dictionary<string, object>();


            var logs = await eventFetcher.GetEventsAsync(
                                                    contractFactory: rollupContract,
                                                    eventName: "NodeCreated",
                                                    argumentFilters: argumentFilters,
                                                    filter: new NewFilterInput
                                                    {
                                                        FromBlock = new BlockParameter((int.Max(latestBlockNumber - l2Network.ConfirmPeriodBlocks - CacheUtils.ASSERTION_CONFIRMED_PADDING, 0)).ToHexBigInteger()),
                                                        ToBlock = BlockParameter.CreateLatest(), // Set to latest block
                                                        Address = new string[] { rollupContract.Address }
                                                    },
                                                    isClassic: false
                                                    );

            logs.OrderBy(log => log.Event.NodeNum);

            ArbBlock lastL2Block = await this.GetBlockFromNodeLog(l2Provider, logs.LastOrDefault());

            var lastSendCount = lastL2Block != null ? lastL2Block.SendCount : BigInteger.Zero;

            if (lastSendCount <= Event.Position)
            {
                return l2Network?.ConfirmPeriodBlocks + CacheUtils.ASSERTION_CREATED_PADDING + CacheUtils.ASSERTION_CONFIRMED_PADDING + latestBlockNumber;
            }

            // use binary search to find the first node with sendCount > this.event.position
            // default to the last node since we already checked above
            var left = 0;
            var right = logs.Count - 1;
            var foundLog = logs.LastOrDefault();
            while (left <= right)
            {
                int mid = (left + right) / 2;
                var log = logs[mid];
                var l2Block = await GetBlockFromNodeLog(l2Provider, log);
                var sendCount = BigInteger.Parse(l2Block.SendCount);
                if (sendCount > Event.Position)
                {
                    foundLog = log;
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            var earliestNodeWithExit = foundLog.Event.NodeNum;
            var node = await rollupContract.GetFunction("getNode").CallAsync<BigInteger>(earliestNodeWithExit);

            return (node.DeadlineBlock + CacheUtils.ASSERTION_CONFIRMED_PADDING);
        }
    }

    public class L2ToL1MessageWriterNitro : L2ToL1MessageReaderNitro
    {
        private readonly SignerOrProvider l1Signer;

        public L2ToL1MessageWriterNitro(SignerOrProvider l1Signer, L2ToL1TransactionEvent eventArgs, Web3 l1Provider = null)
            : base(l1Provider ?? l1Signer.Provider, eventArgs)
        {
            this.l1Signer = l1Signer ?? throw new ArgumentNullException(nameof(l1Signer));
        }

        public async Task<TransactionReceipt> Execute(Web3 l2Provider, Dictionary<string, object>? overrides = null)
        {
            var status = await GetStatus(l2Provider);
            if (status != L2ToL1MessageStatus.CONFIRMED)
            {
                throw new ArbSdkError($"Cannot execute message. Status is: {status} but must be {L2ToL1MessageStatus.CONFIRMED}.");
            }

            var proof = await GetOutboxProof(l2Provider);
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);


            var outboxContract = LoadContractUtils.LoadContract(
                                                        contractName: "Outbox",
                                                        provider: l1Signer.Provider,
                                                        address: l2Network?.EthBridge?.Outbox,
                                                        isClassic: false
                                                    );
            if (overrides == null)
            {
                overrides = new Dictionary<string, object>();
            }

            if (!overrides.ContainsKey("from"))
            {
                overrides["from"] = l1Signer.Account.Address;
            }

            var txReceipt = await outboxContract.GetFunction("executeTransaction").SendTransactionAndWaitForReceiptAsync(
                from: proof.L2Sender,
                receiptRequestCancellationToken: null,
                L1BatchNumber.ToString(),
                proof,
                Event.Position
                Event.Caller,
                Event.Destination,
                Event.ArbBlockNum,
                Event.EthBlockNum,
                Event.Timestamp,
                Event.CallValue,
                Event.Data,
                overrides ?? new Dictionary<string, object>()
                );
            return txReceipt;
        }
    }
}
