using Arbitrum.DataEntities;
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
using Nethereum.Web3.Accounts;
using Nethereum.ABI.FunctionEncoding.Attributes;

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

        public static async Task<object> GetBlockRangesForL1BlockWithCache(IClient l1Provider, IClient l2Provider, int forL1Block)
        {
            string l2ChainId = (await new Web3(l2Provider).Eth.ChainId.SendRequestAsync()).ToString();
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

            return l2BlockRange!;
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

    public class NodeStructOutput
    {
        public string? StateHash { get; set; }
        public string? ChallengeHash { get; set; }
        public string? ConfirmData { get; set; }
        public BigInteger? PrevNum { get; set; }
        public BigInteger? DeadlineBlock { get; set; }
        public BigInteger? NoChildConfirmedBeforeBlock { get; set; }
        public BigInteger? StakerCount { get; set; }
        public BigInteger? ChildStakerCount { get; set; }
        public BigInteger? FirstChildBlock { get; set; }
        public BigInteger? LatestChildNumber { get; set; }
        public BigInteger? CreatedAtBlock { get; set; }
        public string? NodeHash { get; set; }
    }

    public class L2ToL1TxEvent : IEventDTO
    {
        public string? Caller { get; set; }
        public string? Destination { get; set; }
        public BigInteger? Hash { get; set; }
        public BigInteger? Position { get; set; }
        public BigInteger? ArbBlockNum { get; set; }
        public BigInteger? EthBlockNum { get; set; }
        public BigInteger? Timestamp { get; set; }
        public BigInteger? CallValue { get; set; }
        public string? Data { get; set; }
    }

    public abstract class L2ToL1MessageNitro
    {
        protected readonly L2ToL1TxEvent _event;

        protected L2ToL1MessageNitro(L2ToL1TxEvent @event)
        {
            _event = @event;
        }

        public static L2ToL1MessageNitro FromEvent<T>(
            T l1SignerOrProvider,
            L2ToL1TxEvent l2ToL1TransactionEvent,
            Web3? l1Provider = null) where T : SignerOrProvider
        {
            if (SignerProviderUtils.IsSigner(l1SignerOrProvider))
            {
                return new L2ToL1MessageWriterNitro(l1SignerOrProvider?.Account, l2ToL1TransactionEvent, l1Provider);
            }
            else if (l1SignerOrProvider is Web3)
            {
                return new L2ToL1MessageReaderNitro(l1SignerOrProvider?.Provider, l2ToL1TransactionEvent);
            }
            else
            {
                throw new ArgumentException("Invalid type for l1SignerOrProvider");
            }
        }

        public static async Task<List<(L2ToL1TxEvent EventArgs, string TransactionHash)>> GetL2ToL1Events(
            IWeb3 l2Provider,
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
                argumentFilters["hash"] = hash!;
            }

            var eventList = await eventFetcher.GetEventsAsync<L2ToL1TxEvent>(
                contractFactory: "ArbSys",
                eventName: "L2ToL1Tx",
                argumentFilters: argumentFilters,
                filter: new NewFilterInput
                {
                    FromBlock = filter?.FromBlock,
                    ToBlock = filter?.ToBlock,
                    Address = new string[] { Constants.ARB_SYS_ADDRESS },
                    Topics = filter?.Topics
                },
                isClassic: false
                );

            return eventList.Select(e => (e.Event, e.TransactionHash)).ToList();
        }
    }
    public class L2ToL1MessageReaderNitroResults
    {
        public string? SendRootHash { get; set; }
        public BigInteger? SendRootSize { get; set; }
        public bool SendRootConfirmed { get; set; }
    }

    public class L2ToL1MessageReaderNitro : L2ToL1MessageNitro
    {
        protected string? SendRootHash { get; set; }
        protected BigInteger? SendRootSize { get; set; }
        protected bool SendRootConfirmed { get; set; }
        protected string? OutboxAddress { get; set; }
        protected int? L1BatchNumber { get; set; }

        protected readonly Web3 l1Provider;

        public L2ToL1MessageReaderNitro(Web3 l1Provider, L2ToL1TxEvent l2ToL1TransactionEvent) : base(l2ToL1TransactionEvent)
        {
            this.l1Provider = l1Provider;
        }

        public async Task<MessageBatchProofInfo> GetOutboxProof(Web3 l2Provider)
        {
            var sendProps = await GetSendProps(l2Provider);
            if (sendProps.SendRootSize == 0)
            {
                throw new ArbSdkError("Node not yet created, cannot get proof.");
            }


            var nodeInterface = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    provider: l2Provider,
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    isClassic: false);

            var outboxProofParams = await nodeInterface.GetFunction("constructOutboxProof").CallAsync<MessageBatchProofInfo>(
                                        sendProps.SendRootSize, _event.Position);

            var result = LoadContractUtils.FormatContractOutput(nodeInterface, "constructOutboxProof", outboxProofParams);

            return result.Proof;
        }

        protected async Task<bool> HasExecuted(Web3 l2Provider)
        {
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);

            var outboxContract = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "Outbox",
                address: l2Network?.EthBridge?.Outbox,
                isClassic: false
                );

            var outboxContractFunction = outboxContract.GetFunction("isSpent");

            return await outboxContractFunction.CallAsync<bool>(_event.Position);
        }

        public async Task<L2ToL1MessageStatus> Status(Web3 l2Provider)
        {
            var sendProps = await GetSendProps(l2Provider);
            if (!sendProps.SendRootConfirmed)
            {
                return L2ToL1MessageStatus.UNCONFIRMED;
            }

            return await HasExecuted(l2Provider) ? L2ToL1MessageStatus.EXECUTED : L2ToL1MessageStatus.CONFIRMED;
        }

        public Dictionary<string, string> ParseNodeCreatedAssertion(FetchedEvent<NodeCreatedEvent> nodeCreatedEvent)
        {
            var assertion = nodeCreatedEvent.Event.Assertion;
            var afterState = assertion!.AfterState!.GlobalState;
            var blockHash = afterState?.Bytes32Vals![0];
            var sendRoot = afterState?.Bytes32Vals![1];

            return new Dictionary<string, string>
            {
                { "blockHash", blockHash! },
                { "sendRoot", sendRoot! }
            };
        }

        private async Task<ArbBlock> GetBlockFromNodeLog(IClient l2Provider, FetchedEvent<NodeCreatedEvent> log)
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
                    var l2BlockRange = await CacheUtils.GetBlockRangesForL1BlockWithCache(l1Provider.Client, l2Provider.Client, createdAtBlock);

                    BigInteger? startBlock = l2BlockRange.startBlock;
                    BigInteger? endBlock = l2BlockRange.endBlock;

                    if (startBlock == null || endBlock == null)
                    {
                        throw new Exception();
                    }

                    createdFromBlock = startBlock.Value;
                    createdToBlock = endBlock.Value;
                }
                catch (Exception)
                {
                    createdFromBlock = createdAtBlock;
                    createdToBlock = createdAtBlock;
                }
            }

            var eventFetcher = new EventFetcher(rollup.Provider);
            var argumentFilters = new Dictionary<string, object> { { "nodeNum", nodeNum } };

            List<FetchedEvent<NodeCreatedEvent>> logs = await eventFetcher.GetEventsAsync<NodeCreatedEvent>(
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

            if (logs.Count > 1)
            {
                throw new ArbSdkError($"Unexpected number of NodeCreated events. Expected 0 or 1, got {logs.Count}.");
            }

            return await GetBlockFromNodeLog(l2Provider.Client, logs.FirstOrDefault()!);
        }

        public async Task<BigInteger?> GetBatchNumber(Web3 l2Provider)
        {
            var res = BigInteger.Zero;
            if (L1BatchNumber == null)
            {
                try
                {
                    var nodeInterfaceContract = await LoadContractUtils.LoadContract(
                        provider: l2Provider,
                        contractName: "NodeInterface",
                        address: Constants.NODE_INTERFACE_ADDRESS,
                        isClassic: false
                    );

                    res = await nodeInterfaceContract.GetFunction("findBatchContainingBlock").CallAsync<BigInteger>(_event.ArbBlockNum);
                    L1BatchNumber = (int)res;
                }
                catch (Exception)
                {
                    // do nothing - errors are expected here
                }
            }
            return L1BatchNumber;
        }

        protected async Task<L2ToL1MessageReaderNitroResults> GetSendProps(Web3 l2Provider)
        {
            if (!SendRootConfirmed)
            {
                var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);

                var rollupContract = await LoadContractUtils.LoadContract(
                    provider: l2Provider,
                    contractName: "RollupUserLogic",
                    address: l2Network?.EthBridge?.Rollup,
                    isClassic: false
                    );

                var latestConfirmedNodeNum = await rollupContract.GetFunction("latestConfirmed").CallAsync<BigInteger>();
                var l2BlockConfirmed = await GetBlockFromNodeNum(rollupContract, latestConfirmedNodeNum, l2Provider);

                var sendRootSizeConfirmed = l2BlockConfirmed.SendCount;
                if (sendRootSizeConfirmed > _event.Position)
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
                        if (sendRootSize > _event.Position)
                        {
                            SendRootSize = sendRootSize;
                            SendRootHash = l2Block.SendRoot;
                        }
                    }
                }
            }
            return new L2ToL1MessageReaderNitroResults
            {
                SendRootSize = SendRootSize,
                SendRootHash = SendRootHash,
                SendRootConfirmed = SendRootConfirmed
            };
        }

        public async Task<L2ToL1MessageStatus> WaitUntilReadyToExecute(Web3 l2Provider, int retryDelay = 500)
        {
            var status = await Status(l2Provider);
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

            var rollupContract = await LoadContractUtils.LoadContract(
                provider: l1Provider,
                contractName: "RollupUserLogic",
                address: l2Network?.EthBridge?.Rollup,
                isClassic: false
                );

            var status = await Status(l2Provider);
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


            var logs = await eventFetcher.GetEventsAsync<NodeCreatedEvent>(
                                                    contractFactory: rollupContract,
                                                    eventName: "NodeCreated",
                                                    argumentFilters: argumentFilters,
                                                    filter: new NewFilterInput
                                                    {
                                                        FromBlock = new BlockParameter((int.Max(latestBlockNumber - l2Network!.ConfirmPeriodBlocks - CacheUtils.ASSERTION_CONFIRMED_PADDING, 0)).ToHexBigInteger()),
                                                        ToBlock = BlockParameter.CreateLatest(), // Set to latest block
                                                        Address = new string[] { rollupContract.Address }
                                                    },
                                                    isClassic: false
                                                    );

            logs.OrderBy(log => log.Event.NodeNum);

            ArbBlock lastL2Block = await this.GetBlockFromNodeLog(l2Provider.Client, logs.LastOrDefault()!);

            var lastSendCount = lastL2Block != null ? lastL2Block.SendCount : BigInteger.Zero;

            if (lastSendCount <= _event.Position)
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
                var l2Block = await GetBlockFromNodeLog(l2Provider.Client, log);
                var sendCount = l2Block.SendCount;
                if (sendCount > _event.Position)
                {
                    foundLog = log;
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            var earliestNodeWithExit = foundLog!.Event.NodeNum;
            var node = await rollupContract.GetFunction("getNode").CallAsync<NodeStructOutput>(earliestNodeWithExit);

            return node.DeadlineBlock + CacheUtils.ASSERTION_CONFIRMED_PADDING;
        }
    }

    public class L2ToL1MessageWriterNitro : L2ToL1MessageReaderNitro
    {
        private readonly Account l1Signer;

        public L2ToL1MessageWriterNitro(Account l1Signer, L2ToL1TxEvent eventArgs, Web3? l1Provider = null)
            : base(l1Provider ?? new Web3(l1Signer?.TransactionManager?.Client), eventArgs)
        {
            this.l1Signer = l1Signer ?? throw new ArgumentNullException(nameof(l1Signer));
        }

        public async Task<TransactionReceipt> Execute(Web3 l2Provider, Overrides? overrides = null)
        {
            var status = await Status(l2Provider);
            if (status != L2ToL1MessageStatus.CONFIRMED)
            {
                throw new ArbSdkError($"Cannot execute message. Status is: {status} but must be {L2ToL1MessageStatus.CONFIRMED}.");
            }

            var proof = await GetOutboxProof(l2Provider);
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);


            var outboxContract = await LoadContractUtils.LoadContract(
                                                        contractName: "Outbox",
                                                        provider: new Web3(l1Signer?.TransactionManager?.Client),
                                                        address: l2Network?.EthBridge?.Outbox,
                                                        isClassic: false
                                                    );

            var txReceipt = await outboxContract.GetFunction("executeTransaction").SendTransactionAndWaitForReceiptAsync(
                from: l1Signer?.Address,
                receiptRequestCancellationToken: null,
                L1BatchNumber.ToString(),
                proof,
                _event?.Position,
                _event?.Caller,
                _event?.Destination,
                _event?.ArbBlockNum,
                _event?.EthBlockNum,
                _event?.Timestamp,
                _event?.CallValue,
                _event?.Data,
                overrides ?? new Overrides()
                );
            return txReceipt!;
        }
    }
}

