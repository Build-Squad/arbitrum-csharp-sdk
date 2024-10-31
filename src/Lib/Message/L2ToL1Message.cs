using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.src.Lib.DataEntities;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;
using static Nethereum.RPC.Eth.DTOs.BlockParameter;

namespace Arbitrum.Message
{
    public class L2ToL1Message
    {
        private static IEventDTO? _event;
        private readonly L2ToL1MessageWriterClassic? classicWriter;
        private readonly L2ToL1MessageWriterNitro? nitroWriter;

        public L2ToL1Message(SignerOrProvider l1Signer, IEventDTO eventObj, Web3? l1Provider = null)
        {
            if (eventObj is L2ToL1TransactionEventDTO classicEvent)
            {
                classicWriter = new L2ToL1MessageWriterClassic(l1Signer, classicEvent.BatchNumber, classicEvent.IndexInBatch, l1Provider);
            }
            else if (eventObj is L2ToL1TxEventDTO nitroEvent)
            {
                nitroWriter = new L2ToL1MessageWriterNitro(l1Signer, nitroEvent, l1Provider);
            }
        }

        public async Task<L2ToL1MessageStatus> StatusBase(SignerOrProvider signerOrProvider)
        {
            if (signerOrProvider.Account != null)
            {
                var writer = new L2ToL1MessageWriter(signerOrProvider, _event, signerOrProvider.Provider);
                return await writer.Status(signerOrProvider.Provider);
            }
            else
            {
                var reader = new L2ToL1MessageReader(signerOrProvider, _event);
                return await reader.Status(signerOrProvider.Provider);
            }
        }

        public async Task<L2ToL1MessageStatus> WaitUntilReadyToExecuteBase(dynamic signerOrProvider)
        {
            if (signerOrProvider.Account is SignerOrProvider)
            {
                var writer = new L2ToL1MessageWriter(signerOrProvider, _event, signerOrProvider.Provider);
                return await writer.WaitUntilReadyToExecute(signerOrProvider.Provider);
            }
            else
            {
                var reader = new L2ToL1MessageReader(signerOrProvider.Provider, _event);
                return await reader.WaitUntilReadyToExecute(signerOrProvider.Provider);
            }
        }

        public async Task<BigInteger?> GetFirstExecutableBlockBase(SignerOrProvider signerOrProvider)
        {
            if (signerOrProvider.Account != null)
            {
                var writer = new L2ToL1MessageWriter(signerOrProvider, _event, signerOrProvider.Provider);
                return await writer.GetFirstExecutableBlock(signerOrProvider.Provider);
            }
            else
            {
                var reader = new L2ToL1MessageReader(signerOrProvider, _event);
                return await reader.GetFirstExecutableBlock(signerOrProvider.Provider);
            }
        }

        public async Task<MessageBatchProofInfo> GetOutboxProofBase(SignerOrProvider signerOrProvider)
        {
            if (signerOrProvider.Account != null)
            {
                var writer = new L2ToL1MessageWriter(signerOrProvider, _event, signerOrProvider.Provider);
                return await writer.GetOutboxProof(signerOrProvider.Provider);
            }
            else
            {
                var reader = new L2ToL1MessageReader(signerOrProvider, _event);
                return await reader.GetOutboxProof(signerOrProvider.Provider);
            }
        }

        public static bool IsClassic(L2ToL1TransactionEventDTO e)
        {
            return e is L2ToL1TransactionEventDTO classicEvent && classicEvent.IndexInBatch != default;
        }

        public static Task<L2ToL1Message> FromEvent<T>(
            T l1SignerOrProvider,
            IEventDTO eventObj,
            Web3? l1Provider = null) where T : SignerOrProvider
        {
            _event = eventObj;

            if (SignerProviderUtils.IsSigner(l1SignerOrProvider))
            {
                var writer = new L2ToL1MessageWriter(l1SignerOrProvider, eventObj, l1Provider);
                return Task.FromResult<L2ToL1Message>(writer);
            }
            else
            {
                var reader = new L2ToL1MessageReader(l1SignerOrProvider, eventObj);
                return Task.FromResult<L2ToL1Message>(reader);
            }
        }

        public static async Task<List<(IEventDTO EventArgs, string TransactionHash)>> GetL2ToL1Events(
            IWeb3 l2Provider,
            NewFilterInput filter,
            BigInteger? position = null,
            string? destination = null,
            BigInteger? hash = null,
            BigInteger? indexInBatch = null)
        {
            var l2Network = await NetworkUtils.GetL2Network(l2Provider);

            BlockParameter InClassicRange(dynamic blockTag, dynamic nitroGenBlock)
            {
                if (blockTag.ParameterType is BlockParameterType blockTagString)
                {
                    switch (blockTagString)
                    {
                        case BlockParameterType.earliest:
                            return new BlockParameter(BigInteger.Zero.ToHexBigInteger());
                        case BlockParameterType.latest:
                        case BlockParameterType.pending:
                            return new BlockParameter(nitroGenBlock);
                        case BlockParameterType.blockNumber:
                            return new BlockParameter(new HexBigInteger(BigInteger.Min(blockTag.BlockNumber.Value, nitroGenBlock.Value)));
                        default:
                            throw new ArbSdkError($"Unrecognised block tag. {blockTagString}");
                    }
                }

                return null;
            }

            BlockParameter InNitroRange(dynamic blockTag, dynamic nitroGenBlock)
            {
                if (blockTag.ParameterType is BlockParameterType blockTagString)
                {
                    switch (blockTagString)
                    {
                        case BlockParameterType.earliest:
                            return new BlockParameter(nitroGenBlock);
                        case BlockParameterType.latest:
                            return CreateLatest();
                        case BlockParameterType.pending:
                            return CreatePending();
                        case BlockParameterType.blockNumber:
                            return new BlockParameter(new HexBigInteger(BigInteger.Max(blockTag.BlockNumber.Value, nitroGenBlock.Value)));
                        default:
                            throw new ArbSdkError($"Unrecognised block tag. {blockTagString}");
                    }
                }

                return null;
            }

            var classicFilter = new NewFilterInput
            {
                FromBlock = InClassicRange(filter.FromBlock, l2Network.NitroGenesisBlock.ToHexBigInteger()),
                ToBlock = InClassicRange(filter.ToBlock, l2Network.NitroGenesisBlock.ToHexBigInteger())
            };

            var nitroFilter = new NewFilterInput
            {
                FromBlock = InNitroRange(filter.FromBlock, l2Network.NitroGenesisBlock.ToHexBigInteger()),
                ToBlock = InNitroRange(filter.ToBlock, l2Network.NitroGenesisBlock.ToHexBigInteger())
            };

            var classicLogQueries = new List<Task<List<(L2ToL1TransactionEventDTO EventArgs, string TransactionHash)>>>();
            var nitroLogQueries = new List<Task<List<(L2ToL1TxEventDTO EventArgs, string TransactionHash)>>>();

            if (classicFilter.FromBlock.BlockNumber != classicFilter.ToBlock.BlockNumber)
            {
                classicLogQueries.Add(L2ToL1MessageClassic.GetL2ToL1Events(
                    l2Provider,
                    classicFilter,
                    position,
                    destination,
                    hash,
                    indexInBatch
                ));
            }

            if (nitroFilter.FromBlock.BlockNumber != nitroFilter.ToBlock.BlockNumber)
            {
                nitroLogQueries.Add(L2ToL1MessageNitro.GetL2ToL1Events(
                    l2Provider,
                    nitroFilter,
                    position,   
                    destination,
                    hash
                ));
            }

            var classicResults = await Task.WhenAll(classicLogQueries); 
            var nitroResults = await Task.WhenAll(nitroLogQueries);

            var combinedResults = new List<(IEventDTO EventArgs, string TransactionHash)>();

            combinedResults.AddRange(classicResults.SelectMany(events => events)
                .Select(eventTuple => ((IEventDTO)eventTuple.EventArgs, eventTuple.TransactionHash)));

            combinedResults.AddRange(nitroResults.SelectMany(events => events)
                .Select(eventTuple => ((IEventDTO)eventTuple.EventArgs, eventTuple.TransactionHash)));

            return combinedResults;
        }

        public async Task<TransactionReceipt> Execute(Web3 l2Provider, Overrides? overrides = null)
        {
            if (_event is L2ToL1TxEventDTO)
            {
                return await nitroWriter.Execute(l2Provider, overrides);
            }
            else
            {
                return await classicWriter.Execute(l2Provider, overrides);
            }
        }

        public class L2ToL1MessageReader : L2ToL1Message
        {
            private readonly L2ToL1MessageReaderClassic? classicReader;
            private readonly L2ToL1MessageReaderNitro? nitroReader;

            public L2ToL1MessageReader(SignerOrProvider l1Signer, IEventDTO eventObj) : base(l1Signer, eventObj)
            {
                if (eventObj is L2ToL1TransactionEventDTO classicEvent)
                {
                    classicReader = new L2ToL1MessageReaderClassic(l1Signer.Provider, classicEvent.BatchNumber, classicEvent.IndexInBatch);
                    nitroReader = null;
                }
                else if (eventObj is L2ToL1TxEventDTO nitroEvent)
                {
                    nitroReader = new L2ToL1MessageReaderNitro(l1Signer.Provider, nitroEvent);
                    classicReader = null;
                }
            }

            public async Task<MessageBatchProofInfo> GetOutboxProof(Web3 l2Provider)
            {
                return nitroReader != null ? await nitroReader.GetOutboxProof(l2Provider) : await classicReader.TryGetProof(l2Provider);
            }

            public async Task<L2ToL1MessageStatus> Status(Web3 l2Provider)
            {
                return nitroReader != null ? await nitroReader.Status(l2Provider) : await classicReader.Status(l2Provider);
            }

            public async Task<L2ToL1MessageStatus> WaitUntilReadyToExecute(Web3 l2Provider)
            {
                return nitroReader != null ? await nitroReader.WaitUntilReadyToExecute(l2Provider) : await classicReader.WaitUntilOutboxEntryCreated(l2Provider);
            }

            public async Task<BigInteger?> GetFirstExecutableBlock(Web3 l2Provider)
            {
                return nitroReader != null ? await nitroReader.GetFirstExecutableBlock(l2Provider) : await classicReader.GetFirstExecutableBlock(l2Provider);
            }
        }

        public class L2ToL1MessageWriter : L2ToL1MessageReader
        {
            private L2ToL1MessageWriterClassic? classicWriter;
            private L2ToL1MessageWriterNitro? nitroWriter;

            public L2ToL1MessageWriter(SignerOrProvider l1Signer, IEventDTO eventObj, Web3? l1Provider = null)
                : base(l1Signer, eventObj)
            {
                if (eventObj is L2ToL1TransactionEventDTO classicEvent)
                {
                    classicWriter = new L2ToL1MessageWriterClassic(l1Signer, classicEvent.BatchNumber, classicEvent.IndexInBatch, l1Provider);
                }
                else if (eventObj is L2ToL1TxEventDTO nitroEvent)
                {
                    nitroWriter = new L2ToL1MessageWriterNitro(l1Signer, nitroEvent, l1Provider);
                }
            }

            public async Task<TransactionReceipt> Execute(Web3 l2Provider, Overrides? overrides = null)
            {
                return nitroWriter != null
                    ? await nitroWriter.Execute(l2Provider, overrides)
                    : await classicWriter.Execute(l2Provider, overrides);
            }
        }
    }
}
