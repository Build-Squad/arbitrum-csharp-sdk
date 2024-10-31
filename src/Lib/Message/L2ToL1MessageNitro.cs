using Arbitrum.ContractFactory;
using Arbitrum.ContractFactory.RollupAdminLogic;
using Arbitrum.DataEntities;
using Arbitrum.src.Lib.DataEntities;
using Arbitrum.Utils;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;

namespace Arbitrum.Message
{
    public static class CacheUtils
    {
        public const int ASSERTION_CREATED_PADDING = 50;
        public const int ASSERTION_CONFIRMED_PADDING = 20;

        private static Dictionary<string, int[]> _l2BlockRangeCache = new Dictionary<string, int[]>();
        private static object _lock = new object();

        public static string GetL2BlockRangeCacheKey(int l2ChainId, ulong l1BlockNumber)
        {
            return $"{l2ChainId}-{l1BlockNumber}";
        }

        public static void SetL2BlockRangeCache(string key, int[] value)
        {
            lock (_lock)
            {
                _l2BlockRangeCache[key] = value;
            }
        }

        public static async Task<int[]> GetBlockRangesForL1BlockWithCache(Web3 l1Provider, Web3 l2Provider, dynamic forL1Block)
        {
            int l2ChainId = (int)(await l2Provider.Eth.ChainId.SendRequestAsync()).Value;
            string key = GetL2BlockRangeCacheKey(l2ChainId, forL1Block);

            lock (_lock)
            {
                if (_l2BlockRangeCache.TryGetValue(key, out int[]? value))
                {
                    return value;
                }
            }

            int[] l2BlockRange = await Lib.GetBlockRangesForL1Block(l1Provider, forL1Block);

            lock (_lock)
            {
                _l2BlockRangeCache[key] = l2BlockRange;
            }

            return l2BlockRange!;
        }
    }

    public abstract class L2ToL1MessageNitro
    {
        protected readonly L2ToL1TxEventDTO _event;

        protected L2ToL1MessageNitro(L2ToL1TxEventDTO @event)
        {
            _event = @event;
        }

        public static L2ToL1MessageNitro FromEvent<T>(
            T l1SignerOrProvider,
            L2ToL1TxEventDTO l2ToL1TransactionEvent,
            Web3? l1Provider = null) where T : SignerOrProvider
        {
            if (SignerProviderUtils.IsSigner(l1SignerOrProvider))
            {
                return new L2ToL1MessageWriterNitro(l1SignerOrProvider, l2ToL1TransactionEvent, l1Provider);
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

        public static async Task<List<(L2ToL1TxEventDTO EventArgs, string TransactionHash)>> GetL2ToL1Events(
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

            var eventList = await eventFetcher.GetEventsAsync<L2ToL1TxEventDTO>(
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

        public L2ToL1MessageReaderNitro(Web3 l1Provider, L2ToL1TxEventDTO l2ToL1TransactionEvent) : base(l2ToL1TransactionEvent)
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

            var outboxProofParam = new ConstructOutboxProofFunction
            {
                Size = (ulong)sendProps.SendRootSize.Value,
                Leaf = (ulong)_event.Position
            };

            var outboxProofFunc = l2Provider.Eth.GetContractQueryHandler<ConstructOutboxProofFunction>();
            var outboxProof = await outboxProofFunc.QueryAsync<ConstructOutboxProofOutputDTO>
                (Constants.NODE_INTERFACE_ADDRESS, outboxProofParam);


            var nodeInterface = await LoadContractUtils.LoadContract(
                                    contractName: "NodeInterface",
                                    provider: l2Provider,
                                    address: Constants.NODE_INTERFACE_ADDRESS,
                                    isClassic: false);

            /*
            var outboxProofParams = await nodeInterface.GetFunction("constructOutboxProof").CallAsync<MessageBatchProofInfo>(
                                        sendProps.SendRootSize, _event.Position);
            */
            var result = LoadContractUtils.FormatContractOutput(nodeInterface, "constructOutboxProof", outboxProof);

            return result.Proof;
        }

        protected async Task<bool> HasExecuted(Web3 l2Provider)
        {
            var l2Network = await NetworkUtils.GetL2Network(l2Provider);

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

        public Dictionary<string, string> ParseNodeCreatedAssertion(FetchedEvent<NodeCreatedEventDTO> nodeCreatedEvent)
        {
            var assertion = nodeCreatedEvent.Event.Assertion;
            var afterState = assertion!.AfterState!.GlobalState;
            var blockHash = afterState?.Bytes32Vals![0];
            var sendRoot = afterState?.Bytes32Vals![1];

            return new Dictionary<string, string>
            {
                { "blockHash", blockHash.ToHex() },
                { "sendRoot", sendRoot.ToHex() }
            };
        }

        private async Task<ArbBlock> GetBlockFromNodeLog(Web3 l2Provider, FetchedEvent<NodeCreatedEventDTO> log)
        {
            var arbitrumProvider = new ArbitrumProvider(l2Provider);

            if (log == null)
            {
                Console.WriteLine("No NodeCreated events found, defaulting to block 0");
                return await arbitrumProvider.GetBlock(BigInteger.One.ToHexBigInteger()); 
            }

            var parsedLog = ParseNodeCreatedAssertion(log);

            var l2Block = await arbitrumProvider.GetBlock(BigInteger.One.ToHexBigInteger());

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

        private async Task<ArbBlock> GetBlockFromNodeNum(Contract rollup, ulong nodeNum, Web3 l2Provider)
        {
            var contractHandler = l2Provider.Eth.GetContractHandler(rollup.Address);

            var funcParam = new GetNodeFunction
            {
                NodeNum = nodeNum
            };

            var node = await contractHandler.QueryDeserializingToObjectAsync<GetNodeFunction, GetNodeOutputDTO>(funcParam);
            var createdAtBlock = node?.ReturnValue1?.CreatedAtBlock;

            var createdFromBlock = createdAtBlock ?? BigInteger.Zero;
            var createdToBlock = createdAtBlock ?? BigInteger.Zero;


            if (await Lib.IsArbitrumChain(l1Provider))
            {
                try
                {
                    var l2BlockRange = await CacheUtils.GetBlockRangesForL1BlockWithCache(l1Provider, l2Provider, createdAtBlock.Value);

                    var startBlock = l2BlockRange[0];
                    var endBlock = l2BlockRange[l2BlockRange.Length-1];

                    if (startBlock == null || endBlock == null)
                    {
                        throw new Exception();
                    }

                    createdFromBlock = startBlock;
                    createdToBlock = endBlock;
                }
                catch (Exception)
                {
                    createdFromBlock = createdAtBlock ?? BigInteger.Zero;
                    createdToBlock = createdAtBlock ?? BigInteger.Zero;
                }
            }

            var eventFetcher = new EventFetcher(l2Provider);
            var argumentFilters = new Dictionary<string, object> { { "nodeNum", nodeNum } };

            var logs = await eventFetcher.GetEventsAsync<NodeCreatedEventDTO>(
                        contractFactory: rollup,
                        eventName: "NodeCreated",
                        argumentFilters: argumentFilters,
                        filter: new NewFilterInput
                        {
                            FromBlock = new BlockParameter(createdFromBlock.ToHexBigInteger()),
                            ToBlock = new BlockParameter(createdToBlock.ToHexBigInteger()),
                            Address = new string[] { rollup.Address }
                        });

            if (logs?.Count > 1)
            {
                throw new ArbSdkError($"Unexpected number of NodeCreated events. Expected 0 or 1, got {logs?.Count}.");
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
                {}
            }
            return L1BatchNumber;
        }

        protected async Task<L2ToL1MessageReaderNitroResults> GetSendProps(Web3 l2Provider)
        {
            if (!SendRootConfirmed)
            {
                var l2Network = await NetworkUtils.GetL2Network(l2Provider);

                var rollupContract = await LoadContractUtils.LoadContract(
                    provider: l2Provider,
                    contractName: "RollupUserLogic",
                    address: l2Network?.EthBridge?.Rollup,
                    isClassic: false
                    );

                var latestConfirmedNodeNum = await rollupContract.GetFunction<LatestConfirmedFunction>().CallAsync<ulong>();
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
                    var latestNodeNum = await rollupContract.GetFunction<LatestNodeCreatedFunction>().CallAsync<ulong>();
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

        public async Task<L2ToL1MessageStatus> WaitUntilReadyToExecute(Web3 l2Provider, int retryDelay = 1500)
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
            var l2Network = await NetworkUtils.GetL2Network(l2Provider);

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

            int latestBlockNumber = (int)latestBlock.Value;

            var eventFetcher = new EventFetcher(l1Provider);


            var argumentFilters = new Dictionary<string, object>();


            var logs = await eventFetcher.GetEventsAsync<NodeCreatedEventDTO>(
                contractFactory: rollupContract,
                eventName: "NodeCreated",
                argumentFilters: argumentFilters,
                filter: new NewFilterInput
                {
                    FromBlock = new BlockParameter((int.Max(latestBlockNumber - l2Network!.ConfirmPeriodBlocks - CacheUtils.ASSERTION_CONFIRMED_PADDING, 0)).ToHexBigInteger()),
                    ToBlock = BlockParameter.CreateLatest(),
                    Address = new string[] { rollupContract.Address }
                },
                isClassic: false);

            logs.OrderBy(log => log.Event.NodeNum);

            ArbBlock lastL2Block = await GetBlockFromNodeLog(l2Provider, logs.LastOrDefault()!);

            var lastSendCount = lastL2Block != null ? lastL2Block.SendCount : BigInteger.Zero;

            if (lastSendCount <= _event.Position)
            {
                return l2Network?.ConfirmPeriodBlocks + CacheUtils.ASSERTION_CREATED_PADDING + CacheUtils.ASSERTION_CONFIRMED_PADDING + latestBlockNumber;
            }

            var left = 0;
            var right = logs.Count - 1;
            var foundLog = logs.LastOrDefault();
            while (left <= right)
            {
                int mid = (left + right) / 2;
                var log = logs[mid];
                var l2Block = await GetBlockFromNodeLog(l2Provider, log);
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

            var contractHandler = l2Provider.Eth.GetContractHandler(rollupContract.Address);

            var funcParam = new GetNodeFunction
            {
                NodeNum = foundLog.Event.NodeNum
            };

            var node = await contractHandler.QueryDeserializingToObjectAsync<GetNodeFunction, GetNodeOutputDTO>(funcParam);

            return node.ReturnValue1.DeadlineBlock + CacheUtils.ASSERTION_CONFIRMED_PADDING;
        }
    }

    public class L2ToL1MessageWriterNitro : L2ToL1MessageReaderNitro
    {
        private readonly SignerOrProvider l1Signer;

        public L2ToL1MessageWriterNitro(SignerOrProvider l1Signer, L2ToL1TxEventDTO eventArgs, Web3? l1Provider = null)
            : base(l1Provider ?? l1Signer.Provider, eventArgs)
        {
            this.l1Signer = l1Signer ?? throw new ArgumentNullException(nameof(l1Signer));
        }

        public async Task<TransactionReceipt> Execute(Web3 l2Provider, Overrides? overrides = null)
        {
            var status = await Status(l2Provider);
            /*if (status != L2ToL1MessageStatus.CONFIRMED)
            {
                throw new ArbSdkError($"Cannot execute message. Status is: {status} but must be {L2ToL1MessageStatus.CONFIRMED}.");
            }*/

            var proof = await GetOutboxProof(l2Provider);
            var l2Network = await NetworkUtils.GetL2Network(l2Provider);


            var outboxContract = await LoadContractUtils.LoadContract(
                                                        contractName: "Outbox",
                                                        provider: l1Signer.Provider,
                                                        address: l2Network?.EthBridge?.Outbox,
                                                        isClassic: false
                                                    );

            var txReceipt = await outboxContract.GetFunction("executeTransaction").SendTransactionAndWaitForReceiptAsync(
                from: l1Signer?.Account?.Address,
                receiptRequestCancellationToken: null,
                proof,
                _event?.Position,
                _event?.Caller,
                _event?.Destination,
                _event?.ArbBlockNum,
                _event?.EthBlockNum,
                _event?.Timestamp,
                _event?.Callvalue,
                _event?.Data);

            return txReceipt!;
        }
    }
}
