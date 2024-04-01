using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading.Tasks;
using Arbitrum.Message;
using System.Reflection.Metadata.Ecma335;
using System.Reflection;

namespace Arbitrum.Message
{
    public class L2ToL1Message
    {
        public static bool IsClassic(L2ToL1TransactionEvent e)
        {
            if (e is ClassicL2ToL1TransactionEvent classicEvent)
            {
                return classicEvent.IndexInBatch != default;
            }
            else
            {
                return false;
            }
        }

        public static Task<L2ToL1Message> FromEvent<T>(
            T l1SignerOrProvider,
            L2ToL1TransactionEvent eventObj,
            Web3 l1Provider = null) where T : SignerOrProvider
        {
            if (SignerProviderUtils.IsSigner(l1SignerOrProvider))
            {
                var writer = new L2ToL1MessageWriter(l1SignerOrProvider, eventObj, l1Provider);
                return Task.FromResult<L2ToL1Message>(writer);
            }
            else
            {
                var reader = new L2ToL1MessageReader(l1SignerOrProvider.Provider, eventObj);
                return Task.FromResult<L2ToL1Message>(reader);
            }
        }

        public static async Task<List<L2ToL1TransactionEvent[]>> GetL2ToL1Events(
            Web3 l2Provider,
            NewFilterInput filter,
            BigInteger? position = null,
            string destination = null,
            BigInteger? hash = null,
            BigInteger? indexInBatch = null)
        {
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider);
            dynamic inClassicRange(dynamic blockTag, int nitroGenBlock)
            {
                if (blockTag is string)
                {
                    switch (blockTag)
                    {
                        case "earliest":
                            return 0;
                        case "latest":
                            return nitroGenBlock;
                        case "pending":
                            return nitroGenBlock;
                        default:
                            throw new ArbSdkError($"Unrecognised block tag. {blockTag}");
                    }
                }
                return Math.Min(blockTag, nitroGenBlock);
            }

            dynamic inNitroRange(dynamic blockTag, int nitroGenBlock)
            {
                if (blockTag is string)
                {
                    switch (blockTag)
                    {
                        case "earliest":
                            return nitroGenBlock;
                        case "latest":
                            return "latest";
                        case "pending":
                            return "pending";
                        default:
                            throw new ArbSdkError($"Unrecognised block tag. {blockTag}");
                    }
                }
                return Math.Max(blockTag, nitroGenBlock);
            }

            var classicFilter = new NewFilterInput
            {
                FromBlock = inClassicRange(filter.FromBlock, l2Network.NitroGenesisBlock),
                ToBlock = inClassicRange(filter.ToBlock, l2Network.NitroGenesisBlock)
            };

            var nitroFilter = new NewFilterInput
            {
                FromBlock = inNitroRange(filter.FromBlock, l2Network.NitroGenesisBlock),
                ToBlock = inNitroRange(filter.ToBlock, l2Network.NitroGenesisBlock)
            };

            var logQueries = new List<Task<List<L2ToL1TransactionEvent[]>>>();
            if (classicFilter.FromBlock != classicFilter.ToBlock)
            {
                logQueries.Add(L2ToL1MessageClassic.GetL2ToL1Events(
                    l2Provider,
                    classicFilter,
                    position,
                    destination,
                    hash,
                    indexInBatch));
            }

            if (nitroFilter.FromBlock != nitroFilter.ToBlock)
            {
                logQueries.Add(L2ToL1MessageNitro.GetL2ToL1Events(
                    l2Provider,
                    nitroFilter,
                    position,
                    destination,
                    hash));
            }

            var results = await Task.WhenAll(logQueries);
            var flattenedEvents = new List<L2ToL1TransactionEvent[]>();
            foreach (var result in results)
            {
                flattenedEvents.AddRange(result);
            }
            return flattenedEvents;
        }
    }

    public class L2ToL1MessageReader : L2ToL1Message
    {
        private readonly L2ToL1MessageReaderClassic classicReader;
        private readonly L2ToL1MessageReaderNitro nitroReader;

        public L2ToL1MessageReader(Web3 l1Provider, L2ToL1TransactionEvent l2ToL1TransactionEvent) : base()
        {
            if (IsClassic(l2ToL1TransactionEvent))
            {
                classicReader = new L2ToL1MessageReaderClassic(
                                        l1Provider,
                                        l2ToL1TransactionEvent.BatchNumber,
                                        l2ToL1TransactionEvent.IndexInBatch
                                        );
            }
            else
            {
                nitroReader = new L2ToL1MessageReaderNitro(
                                        l1Provider,
                                        l2ToL1TransactionEvent
                                        );
            }
        }

        public async Task<MessageBatchProofInfo> GetOutboxProof(Web3 l2Provider)
        {
            if (nitroReader != null)
            {
                return await nitroReader.GetOutboxProof(l2Provider);
            }
            else
            {
                return await classicReader.TryGetProof(l2Provider);
            }
        }

        public async Task<L2ToL1MessageStatus> Status(Web3 l2Provider)
        {
            if (nitroReader != null)
            {
                return await nitroReader.Status(l2Provider);
            }
            else
            {
                return await classicReader.Status(l2Provider);
            }
        }

        public async Task<L2ToL1MessageStatus> WaitUntilReadyToExecute(Web3 l2Provider, int retryDelay = 500)
        {
            if (nitroReader != null)
            {
                return await nitroReader.WaitUntilReadyToExecute(l2Provider, retryDelay);
            }
            else
            {
                return await classicReader.WaitUntilOutboxEntryCreated(l2Provider, retryDelay);
            }
        }

        public async Task<BigInteger?> GetFirstExecutableBlock(Web3 l2Provider)
        {
            if (nitroReader != null)
            {
                return await nitroReader.GetFirstExecutableBlock(l2Provider);
            }
            else
            {
                return await classicReader.GetFirstExecutableBlock(l2Provider);
            }
        }
    }

    public class L2ToL1MessageWriter : L2ToL1MessageReader
    {
        private readonly ClassicL2ToL1MessageWriterClassic classicWriter;
        private readonly NitroL2ToL1MessageWriterNitro nitroWriter;

        public L2ToL1MessageWriter(SignerOrProvider l1Signer, L2ToL1TransactionEvent l2ToL1TransactionEvent, Web3 l1Provider) : base(l1Provider ?? l1Signer.Provider, l2ToL1TransactionEvent)
        {
            if (IsClassic(l2ToL1TransactionEvent))
            {
                classicWriter = new ClassicL2ToL1MessageWriterClassic(
                    l1Signer
                classicWriter = new ClassicL2ToL1MessageWriterClassic(
                    l1Signer,
                    l2ToL1TransactionEvent.BatchNumber,
                    l2ToL1TransactionEvent.IndexInBatch,
                    l1Provider
                );
            }
            else
            {
                nitroWriter = new NitroL2ToL1MessageWriterNitro(
                    l1Signer,
                    l2ToL1TransactionEvent,
                    l1Provider
                );
            }
        }
    }
}
