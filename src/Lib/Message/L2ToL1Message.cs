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
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using System.Net;

namespace Arbitrum.Message
{
    // Define a conditional type for Signer or Provider
    public class L2ToL1MessageReaderOrWriter<T>
    {
        // Use conditional type to determine the actual type based on T
        public Type GetReaderOrWriterType()
        {
            if (typeof(T) == typeof(Web3))
            {
                return typeof(L2ToL1MessageReader);
            }
            else if (typeof(T) == typeof(Account))
            {
                return typeof(L2ToL1MessageWriter);
            }
            else
            {
                throw new ArgumentException("Invalid type parameter. T must be either Web3 or Account.");
            }
        }
    }
    public class L2ToL1Message
    {
        //Generic method for optional reader or writer type
        public async Task<L2ToL1MessageStatus> StatusBase(SignerOrProvider signerOrProvider)
        {
            // Check if Account is set
            if (signerOrProvider.Account != null)
            {
                // Create an instance of L2ToL1MessageWriter
                var writer = new L2ToL1MessageWriter(signerOrProvider, new L2ToL1TransactionEvent(), signerOrProvider.Provider);

                // Call the Status method of L2ToL1MessageWriter
                return await writer.Status(signerOrProvider.Provider);
            }
            // Check if Provider is set
            else
            {
                // Create an instance of L2ToL1MessageReader
                var reader = new L2ToL1MessageReader(signerOrProvider.Provider, new L2ToL1TransactionEvent());

                // Call the Status method of L2ToL1MessageReader
                return await reader.Status(signerOrProvider.Provider);
            }
        }
        public async Task<L2ToL1MessageStatus> WaitUntilReadyToExecuteBase(SignerOrProvider signerOrProvider)
        {
            // Check if Account is set
            if (signerOrProvider.Account != null)
            {
                // Create an instance of L2ToL1MessageWriter
                var writer = new L2ToL1MessageWriter(signerOrProvider, new L2ToL1TransactionEvent(), signerOrProvider.Provider);

                // Call the Status method of L2ToL1MessageWriter
                return await writer.WaitUntilReadyToExecute(signerOrProvider.Provider);
            }
            // Check if Provider is set
            else
            {
                // Create an instance of L2ToL1MessageReader
                var reader = new L2ToL1MessageReader(signerOrProvider.Provider, new L2ToL1TransactionEvent());

                // Call the Status method of L2ToL1MessageReader
                return await reader.WaitUntilReadyToExecute(signerOrProvider.Provider);
            }
        }
        public async Task<BigInteger?> GetFirstExecutableBlockBase(SignerOrProvider signerOrProvider)
        {
            // Check if Account is set
            if (signerOrProvider.Account != null)
            {
                // Create an instance of L2ToL1MessageWriter
                var writer = new L2ToL1MessageWriter(signerOrProvider, new L2ToL1TransactionEvent(), signerOrProvider.Provider);

                // Call the Status method of L2ToL1MessageWriter
                return await writer.GetFirstExecutableBlock(signerOrProvider.Provider);
            }
            // Check if Provider is set
            else
            {
                // Create an instance of L2ToL1MessageReader
                var reader = new L2ToL1MessageReader(signerOrProvider.Provider, new L2ToL1TransactionEvent());

                // Call the Status method of L2ToL1MessageReader
                return await reader.GetFirstExecutableBlock(signerOrProvider.Provider);
            }
        }
        public async Task<MessageBatchProofInfo> GetOutboxProofBase(SignerOrProvider signerOrProvider)
        {
            // Check if Account is set
            if (signerOrProvider.Account != null)
            {
                // Create an instance of L2ToL1MessageWriter
                var writer = new L2ToL1MessageWriter(signerOrProvider, new L2ToL1TransactionEvent(), signerOrProvider.Provider);

                // Call the Status method of L2ToL1MessageWriter
                return await writer.GetOutboxProof(signerOrProvider.Provider);
            }
            // Check if Provider is set
            else
            {
                // Create an instance of L2ToL1MessageReader
                var reader = new L2ToL1MessageReader(signerOrProvider.Provider, new L2ToL1TransactionEvent());

                // Call the Status method of L2ToL1MessageReader
                return await reader.GetOutboxProof(signerOrProvider.Provider);
            }
        }

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
            Web3? l1Provider = null) where T : SignerOrProvider
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
            IWeb3 l2Provider,
            NewFilterInput filter,
            BigInteger? position = null,
            string? destination = null,
            BigInteger? hash = null,
            BigInteger? indexInBatch = null)
        {
            var l2Network = await NetworkUtils.GetL2Network(l2Provider);

            // Define a function to determine the range in classic block numbers
            BlockParameter inClassicRange(dynamic blockTag, dynamic nitroGenBlock)
            {
                switch (blockTag.ParameterType.ToString())
                {
                    case "earliest":
                        return new BlockParameter(0.ToHexBigInteger());
                    case "latest":
                        return new BlockParameter(nitroGenBlock);
                    case "pending":
                        return new BlockParameter(nitroGenBlock);
                    case "blockNumber":
                        return new BlockParameter(Math.Min((int)blockTag, (int)nitroGenBlock).ToHexBigInteger());
                    default:
                        throw new ArbSdkError($"Unrecognised block tag. {blockTag}");
                }
            }

            // Define a function to determine the range in nitro block numbers
            BlockParameter inNitroRange(dynamic blockTag, dynamic nitroGenBlock)
            {
                switch (blockTag.ParameterType.ToString())
                    {
                        case "earliest":
                            return new BlockParameter(nitroGenBlock);
                        case "latest":
                            return BlockParameter.CreateLatest();
                        case "pending":
                            return BlockParameter.CreatePending();
                        case "blockNumber":
                            return new BlockParameter(Math.Max((int)blockTag.BlockNumber.Value, (int)nitroGenBlock.Value).ToHexBigInteger());
                        default: 
                            throw new ArbSdkError($"Unrecognised block tag. {blockTag}");
                    }
            }

            // Determine the block range for classic and nitro events
            var classicFilter = new NewFilterInput
            {
                FromBlock = inClassicRange(filter.FromBlock, l2Network.NitroGenesisBlock.ToHexBigInteger()),
                ToBlock = inClassicRange(filter.ToBlock, l2Network.NitroGenesisBlock.ToHexBigInteger())
            };

            var nitroFilter = new NewFilterInput
            {
                FromBlock = inNitroRange(filter.FromBlock, l2Network.NitroGenesisBlock.ToHexBigInteger()),
                ToBlock = inNitroRange(filter.ToBlock, l2Network.NitroGenesisBlock.ToHexBigInteger())
            };

            // List to store tasks for fetching logs
            var logQueries = new List<Task<L2ToL1TransactionEvent[]>>();

            // Check if there are classic events to fetch
            if (classicFilter.FromBlock != classicFilter.ToBlock)
            {
                // Fetch classic events
                var classicEvents = (await L2ToL1MessageClassic.GetL2ToL1Events(
                    l2Provider,
                    classicFilter,
                    position,
                    destination!,
                    hash,
                    indexInBatch)).Select(tuple => tuple.EventArgs).ToArray();

                logQueries.Add(Task.FromResult(classicEvents));

            }

            if (nitroFilter.FromBlock != nitroFilter.ToBlock)
            {
                //To convert L2ToL1TxEvent instances to L2ToL1TransactionEvent instances, we need to map the corresponding properties
                var nitroEvents = (await L2ToL1MessageNitro.GetL2ToL1Events(
                    l2Provider,
                    nitroFilter,
                    position,
                    destination,
                    hash)).Select(tuple => new L2ToL1TransactionEvent
                    {
                        Caller = tuple.EventArgs.Caller,
                        Destination = tuple.EventArgs.Destination,
                        ArbBlockNum = tuple.EventArgs.ArbBlockNum,
                        EthBlockNum = tuple.EventArgs.EthBlockNum,
                        Timestamp = tuple.EventArgs.Timestamp,
                        CallValue = tuple.EventArgs.CallValue,
                        Data = tuple.EventArgs.Data,
                        Hash = tuple.EventArgs.Hash,
                        Position = tuple.EventArgs.Position
                        // Map other properties as needed
                    }).ToArray();

                logQueries.Add(Task.FromResult(nitroEvents));
            }

            // Wait for all tasks to complete
            var results = await Task.WhenAll(logQueries);


            // Return the results as a list
            return results.ToList();
        }
    }

    public class L2ToL1MessageReader : L2ToL1Message
    {
        private readonly L2ToL1MessageReaderClassic? classicReader;
        private readonly L2ToL1MessageReaderNitro? nitroReader;

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
                                        new L2ToL1TxEvent
                                        {
                                            // Mapping properties from L2ToL1TransactionEvent to L2ToL1TxEvent
                                            Caller = l2ToL1TransactionEvent.Caller,
                                            Destination = l2ToL1TransactionEvent.Destination,
                                            ArbBlockNum = l2ToL1TransactionEvent.ArbBlockNum,
                                            EthBlockNum = l2ToL1TransactionEvent.EthBlockNum,
                                            Timestamp = l2ToL1TransactionEvent.Timestamp,
                                            CallValue = l2ToL1TransactionEvent.CallValue,
                                            Data = l2ToL1TransactionEvent.Data,
                                            Hash = l2ToL1TransactionEvent.Hash,
                                            Position = l2ToL1TransactionEvent.Position
                                        });
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
        private readonly L2ToL1MessageWriterClassic? classicWriter;
        private readonly L2ToL1MessageWriterNitro? nitroWriter;

        public L2ToL1MessageWriter(SignerOrProvider l1Signer, L2ToL1TransactionEvent l2ToL1TransactionEvent, Web3 l1Provider) : base(l1Provider ?? l1Signer.Provider, l2ToL1TransactionEvent)
        {
            if (IsClassic(l2ToL1TransactionEvent))
            {
                classicWriter = new L2ToL1MessageWriterClassic(
                    l1Signer,
                    l2ToL1TransactionEvent.BatchNumber,
                    l2ToL1TransactionEvent.IndexInBatch,
                    l1Provider
                );
            }
            else
            {
                nitroWriter = new L2ToL1MessageWriterNitro(
                    l1Signer: l1Signer,
                    eventArgs:new L2ToL1TxEvent()
                    {
                        // Mapping properties from L2ToL1TransactionEvent to L2ToL1TxEvent
                        Caller = l2ToL1TransactionEvent.Caller,
                        Destination = l2ToL1TransactionEvent.Destination,
                        ArbBlockNum = l2ToL1TransactionEvent.ArbBlockNum,
                        EthBlockNum = l2ToL1TransactionEvent.EthBlockNum,
                        Timestamp = l2ToL1TransactionEvent.Timestamp,
                        CallValue = l2ToL1TransactionEvent.CallValue,
                        Data = l2ToL1TransactionEvent.Data,
                        Hash = l2ToL1TransactionEvent.Hash,
                        Position = l2ToL1TransactionEvent.Position
                    },
                    l1Provider: l1Provider
                );
            }
        }
    }
}
