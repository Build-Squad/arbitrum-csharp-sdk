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

namespace Arbitrum.Message
{
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
        public L2ToL1TransactionEvent Event {get; set; }
        protected L2ToL1MessageNitro(L2ToL1TransactionEvent l2ToL1TransactionEvent)
        {
            Event = l2ToL1TransactionEvent;
        }

        public static L2ToL1MessageNitro FromEvent<T>(
            T l1SignerOrProvider,
            L2ToL1TransactionEvent l2ToL1TransactionEvent,
            Web3 l1Provider = null) where T: SignerOrProvider
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
                filter: new Dictionary<string, object>
                {
                    { "fromBlock", filter.FromBlock },
                    { "toBlock", filter.ToBlock },
                    { "address", Constants.ARB_SYS_ADDRESS },
                    { "**filter", filter }
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
            if (sendProps.sendRootSize == 0)
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
                return await arbitrumProvider.GetBlock(new HexBigInteger(BigInteger.Zero));
            }

            var parsedLog = ParseNodeCreatedAssertion(log);

            var l2Block = await arbitrumProvider.GetBlock(parsedLog.));
            if (l2Block == null)
            {
                throw new ArbSdkError($"Block not found. {parsedLog.blockHash}");
            }
            if (l2Block.SendRoot != parsedLog.sendRoot)
            {
                throw new ArbSdkError($"L2 block send root doesn't match parsed log. {l2Block.SendRoot} {parsedLog.sendRoot}");
            }
            return l2Block;
        }

        private async Task<ArbBlock> GetBlockFromNodeNum(RollupUserLogic rollup, BigInteger nodeNum, Web3 l2Provider)
        {
            var createdAtBlock = (await rollup.GetNode(nodeNum)).CreatedAtBlock;

            var createdFromBlock = createdAtBlock;
            var createdToBlock = createdAtBlock;

            if (await IsArbitrumChain(l1Provider))
            {
                try
                {
                    var nodeInterface = new NodeInterface(NODE_INTERFACE_ADDRESS, l1Provider);
                    var l2BlockRangeFromNode = await nodeInterface.CallFunctionCallAsync<(BigInteger firstBlock, BigInteger lastBlock)>("l2BlockRangeForL1", createdAtBlock);
                    createdFromBlock = l2BlockRangeFromNode.firstBlock;
                    createdToBlock = l2BlockRangeFromNode.lastBlock;
                }
                catch
                {
                    try
                    {
                        var l2BlockRange = await GetBlockRangesForL1BlockWithCache((JsonRpcProvider)l1Provider, (JsonRpcProvider)l2Provider, (int)createdAtBlock);
                        var startBlock = l2BlockRange[0];
                        var endBlock = l2BlockRange[1];
                        if (!startBlock.HasValue || !endBlock.HasValue)
                        {
                            throw new Exception();
                        }
                        createdFromBlock = startBlock.Value;
                        createdToBlock = endBlock.Value;
                    }
                    catch
                    {
                        createdFromBlock = createdAtBlock;
                        createdToBlock = createdAtBlock;
                    }
                }
            }

            var eventFetcher = new EventFetcher(rollup.Provider);
            var logs = (await eventFetcher.GetEvents(RollupUserLogic__factory.CreateContract(), t => t.NodeCreated(nodeNum), createdFromBlock, createdToBlock)).OrderBy(l => l.Log.BlockNumber);

            if (logs.Count() > 1)
            {
                throw new ArbSdkError($"Unexpected number of NodeCreated events. Expected 0 or 1, got {logs.Count()}.");
            }

            return await GetBlockFromNodeLog(l2Provider, logs.FirstOrDefault());
        }

        protected async Task<int?> GetBatchNumber(Web3 l2Provider)
        {
            if (L1BatchNumber == null)
            {
                try
                {
                    var nodeInterface = new NodeInterface(NODE_INTERFACE_ADDRESS, l2Provider);
                    var res = await nodeInterface.CallFunctionCallAsync<BigInteger>("findBatchContainingBlock", Event.Event.arbBlockNum);
                    L1BatchNumber = (int)res;
                }
                catch
                {
                    // do nothing - errors are expected here
                }
            }

            return L1BatchNumber;
        }

        protected async Task<(BigInteger sendRootSize, string sendRootHash, bool sendRootConfirmed)> GetSendProps(Web3 l2Provider)
        {
            if (!SendRootConfirmed)
            {
                var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);

                var rollup = new RollupUserLogic(l2Network.ethBridge.rollup, l1Provider);

                var latestConfirmedNodeNum = await rollup.CallFunctionCallAsync<BigInteger>("latestConfirmed");
                var l2BlockConfirmed = await GetBlockFromNodeNum(rollup, latestConfirmedNodeNum, l2Provider);

                var sendRootSizeConfirmed = BigInteger.Parse(l2BlockConfirmed.SendCount);
                if (sendRootSizeConfirmed > Event.Position)
                {
                    SendRootSize = sendRootSizeConfirmed;
                    SendRootHash = l2BlockConfirmed.SendRoot;
                    SendRootConfirmed = true;
                }
                else
                {
                    var latestNodeNum = await rollup.CallFunctionCallAsync<BigInteger>("latestNodeCreated");
                    if (latestNodeNum > latestConfirmedNodeNum)
                    {
                        var l2Block = await GetBlockFromNodeNum(rollup, latestNodeNum, l2Provider);

                        var sendRootSize = BigInteger.Parse(l2Block.SendCount);
                        if (sendRootSize > Event.Position)
                        {
                            SendRootSize = sendRootSize;
                            SendRootHash = l2Block.SendRoot;
                        }
                    }
                }
                return (SendRootSize, SendRootHash, SendRootConfirmed);
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
                var l2Network = await GetL2Network(l2Provider);

                var rollup = new RollupUserLogic(l2Network.ethBridge.rollup, l1Provider);

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
                var eventFetcher = new EventFetcher(l1Provider);
                var logs = await eventFetcher.GetEvents<RollupUserLogic, RollupUserLogic.NodeCreatedEventDTO>(null, e => e.NodeCreated, new BlockParameter(Math.Max(latestBlock.Value - l2Network.confirmPeriodBlocks.Value - ASSERTION_CONFIRMED_PADDING, 0)), BlockParameter.CreateLatest());

                logs = logs.OrderBy(e => e.Log.BlockNumber).ToArray();

                var lastL2Block = logs.Length == 0 ? null : await GetBlockFromNodeLog(l2Provider, logs.Last());
                var lastSendCount = lastL2Block != null ? BigInteger.Parse(lastL2Block.SendCount) : BigInteger.Zero;

                if (lastSendCount <= Event.Position)
                {
                    return BigInteger.Add(BigInteger.Add(BigInteger.Add(l2Network.confirmPeriodBlocks, ASSERTION_CREATED_PADDING), ASSERTION_CONFIRMED_PADDING), latestBlock);
                }

                // use binary search to find the first node with sendCount > this.event.position
                // default to the last node since we already checked above
                FetchedEvent<RollupUserLogic.NodeCreatedEventDTO> foundLog = logs.Last();
                int left = 0;
                int right = logs.Length - 1;
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
                var node = await rollup.GetNode(earliestNodeWithExit);
                return BigInteger.Add(node.DeadlineBlock, ASSERTION_CONFIRMED_PADDING);
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
                var status = await Status(l2Provider);
                if (status != L2ToL1MessageStatus.CONFIRMED)
                {
                    throw new ArbSdkError($"Cannot execute message. Status is: {status} but must be {L2ToL1MessageStatus.CONFIRMED}.");
                }

                var proof = await GetOutboxProof(l2Provider);
                var l2Network = await GetL2Network(l2Provider);
                var outbox = new Outbox(l2Network.ethBridge.outbox, l1Signer);

                return await outbox.ExecuteTransactionAndWaitForReceiptAsync(
                    proof,
                    Event.position,
                    Event.caller,
                    Event.destination,
                    Event.arbBlockNum,
                    Event.ethBlockNum,
                    Event.timestamp,
                    Event.callvalue,
                    Event.data,
                    overrides ?? new TransactionOptions()
                );
            }
        }

    }
