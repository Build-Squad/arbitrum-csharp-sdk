using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.Web3;
using Arbitrum.Utils;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Transactions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Arbitrum.Message
{
    public class MessageBatchProofInfo
    {
        public byte[]? Proof { get; set; }
        public BigInteger? Path { get; set; }
        public string? L2Sender { get; set; }
        public string? L1Dest { get; set; }
        public BigInteger? L2Block { get; set; }
        public BigInteger? L1Block { get; set; }
        public BigInteger? Timestamp { get; set; }
        public BigInteger? Amount { get; set; }
        public string? CalldataForL1 { get; set; }
    }

    public class L2ToL1MessageClassic
    {
        public BigInteger BatchNumber { get; }
        public BigInteger IndexInBatch { get; }

        public L2ToL1MessageClassic(BigInteger batchNumber, BigInteger indexInBatch)
        {
            BatchNumber = batchNumber;
            IndexInBatch = indexInBatch;
        }

        public static Task<L2ToL1MessageClassic> FromBatchNumber<T>(
            T l1SignerOrProvider,
            BigInteger batchNumber,
            BigInteger indexInBatch,
            Web3? l1Provider = null)
            where T : SignerOrProvider
        {
            if (SignerProviderUtils.IsSigner(l1SignerOrProvider))
            {
                return Task.FromResult<L2ToL1MessageClassic>(new L2ToL1MessageWriterClassic(
                    l1SignerOrProvider,
                    batchNumber,
                    indexInBatch,
                    l1Provider
                ));
            }
            else
            {
                return Task.FromResult<L2ToL1MessageClassic>(new L2ToL1MessageReaderClassic(
                    l1SignerOrProvider.Provider!,
                    batchNumber,
                    indexInBatch
                ));
            }
        }

        public static async Task<List<(L2ToL1TransactionEvent EventArgs, string TransactionHash)>> GetL2ToL1Events(
            IWeb3 l2Provider,
            NewFilterInput filter,
            BigInteger? batchNumber = null,
            string? destination = null,
            BigInteger? uniqueId = null,
            BigInteger? indexInBatch = null)
        {
            var eventFetcher = new EventFetcher(l2Provider);

            var argumentFilters = new Dictionary<string, object>();

            if (batchNumber != null)
            {
                argumentFilters["batchNumber"] = batchNumber;
            }
            if (destination != null)
            {
                argumentFilters["destination"] = destination;
            }
            if (uniqueId != null)
            {
                argumentFilters["uniqueId"] = uniqueId;
            }

            var eventList = await eventFetcher.GetEventsAsync<L2ToL1TransactionEvent>(
                            contractFactory: "ArbSys",
                            eventName: "L2ToL1Transaction",
                            argumentFilters: argumentFilters,
                            filter: new NewFilterInput
                            {
                                FromBlock = filter.FromBlock,
                                ToBlock = filter.ToBlock,
                                Address = new string[] { Constants.ARB_SYS_ADDRESS.EnsureHexPrefix() },
                                Topics = filter.Topics,

                            },
                            isClassic: false
                            );

            var formattedEvents = eventList.Select(e => (e.Event, e.TransactionHash)).ToList();

            if (indexInBatch != null)
            {
                var indexItems = formattedEvents.Where(b => b.Event.IndexInBatch == indexInBatch.Value).ToList();
                if (indexItems.Count > 1)
                {
                    throw new ArbSdkError("More than one indexed item found in batch.");
                }
                else
                {
                    return new List<(L2ToL1TransactionEvent, string)>();
                }
            }
            else
            {
                return formattedEvents;
            }
        }
    }

    public class L2ToL1MessageReaderClassic : L2ToL1MessageClassic
    {
        protected readonly Web3 l1Provider;
        protected string? outboxAddress;
        private MessageBatchProofInfo? proof;

        public L2ToL1MessageReaderClassic(
            Web3 l1Provider,
            BigInteger batchNumber,
            BigInteger indexInBatch) : base(batchNumber, indexInBatch)
        {
            this.l1Provider = l1Provider;
        }

        protected async Task<string> GetOutboxAddress(Web3 l2Provider, int batchNumber)
        {
            if (outboxAddress == null)
            {
                var l2Network = await NetworkUtils.GetL2Network(l2Provider);
                var outboxes = l2Network?.EthBridge?.ClassicOutboxes;
                if (outboxes != null && outboxes.Any())
                {
                    var sortedOutboxes = outboxes.OrderBy(o => o.Value);
                    var res = sortedOutboxes.FirstOrDefault(o => o.Value > batchNumber);
                    outboxAddress = res.Key ?? "0x0000000000000000000000000000000000000000";                 /////////
                }
                else
                {
                    outboxAddress = "0x0000000000000000000000000000000000000000";
                }
            }
            return outboxAddress;
        }

        private async Task<BigInteger> OutboxEntryExists(Web3 l2Provider)
        {
            var outboxAddress = await GetOutboxAddress(l2Provider, (int)BatchNumber);
            var outboxContract = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "Outbox",
                address: outboxAddress,
                isClassic: false
                );

            var getOutboxEntryExistsFunction = outboxContract.GetFunction("outboxEntryExists"); 

            return await getOutboxEntryExistsFunction.CallAsync<BigInteger>();
        }

        public static async Task<MessageBatchProofInfo> TryGetProof(
            Web3 l2Provider,
            BigInteger batchNumber,
            BigInteger indexInBatch)
        {
            var nodeInterfaceContract = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "NodeInterface",
                address: Constants.NODE_INTERFACE_ADDRESS,
                isClassic: false
                );
            try
            {
                var getLegacyLookupMessageBatchProofFunction = nodeInterfaceContract.GetFunction("legacyLookupMessageBatchProof"); 
                return await getLegacyLookupMessageBatchProofFunction.CallAsync<MessageBatchProofInfo>((batchNumber, indexInBatch));
            }
            catch (Exception e)
            {
                var expectedError = "batch doesn't exist";
                if (e.Message.Contains(expectedError))
                    return null!;
                else
                    throw;
            }
        }

        public async Task<MessageBatchProofInfo> TryGetProof(Web3 l2Provider)
        {
            if (proof == null)
            {
                proof = await TryGetProof(l2Provider, BatchNumber, IndexInBatch);
            }
            return proof;
        }

        public async Task<bool> HasExecuted(Web3 l2Provider)
        {
            var proofInfo = await TryGetProof(l2Provider);
            if (proofInfo == null)
                return false;

            var outboxAddress = await GetOutboxAddress(l2Provider, (int)BatchNumber);

            var outboxContract = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "Outbox",
                address: outboxAddress,
                isClassic: false
                );

            try
            {
                var transaction = outboxContract.GetFunction("executeTransaction");

                await transaction.SendTransactionAsync(
                        from: proofInfo.L2Sender,
                        BatchNumber.ToString(),   ///////
                        proofInfo.Proof,
                        proofInfo.Path,
                        proofInfo.L2Sender,
                        proofInfo.L1Dest,
                        proofInfo.L2Block,
                        proofInfo.L1Block,
                        proofInfo.Timestamp,
                        proofInfo.Amount,
                        proofInfo.CalldataForL1
                    );
                return false;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("ALREADY_SPENT"))
                    return true;
                if (e.Message.Contains("NO_OUTBOX_ENTRY"))
                    return false;
                throw;
            }
        }

        public async Task<L2ToL1MessageStatus> Status(Web3 l2Provider)
        {
            try
            {
                var messageExecuted = await HasExecuted(l2Provider);
                if (messageExecuted)
                    return L2ToL1MessageStatus.EXECUTED;

                var outboxEntryExists = await OutboxEntryExists (l2Provider);
                return outboxEntryExists != default ? L2ToL1MessageStatus.CONFIRMED : L2ToL1MessageStatus.UNCONFIRMED;   //////
            }
            catch
            {
                return L2ToL1MessageStatus.UNCONFIRMED;
            }
        }

        public async Task<L2ToL1MessageStatus> WaitUntilOutboxEntryCreated(Web3 l2Provider, int retryDelay = 500)
        {
            var exists = await OutboxEntryExists(l2Provider);
            if (exists != default)
                return await HasExecuted(l2Provider) ? L2ToL1MessageStatus.EXECUTED : L2ToL1MessageStatus.CONFIRMED;   ///////
            else
            {
                await Task.Delay(retryDelay);
                return await WaitUntilOutboxEntryCreated(l2Provider, retryDelay);
            }
        }

        public async Task<BigInteger?> GetFirstExecutableBlock(Web3 l2Provider)
        {
            return await Task.FromResult<BigInteger?>(null);
        }
    }

    public class L2ToL1MessageWriterClassic : L2ToL1MessageReaderClassic
    {
        private readonly SignerOrProvider _l1Signer;

        public L2ToL1MessageWriterClassic(
            SignerOrProvider l1Signer,
            BigInteger batchNumber,
            BigInteger indexInBatch,
            Web3? l1Provider = null) : base(l1Provider ?? l1Signer.Provider, batchNumber, indexInBatch)
        {
            _l1Signer = l1Signer;
        }

        public async Task<TransactionReceipt> Execute(Web3 l2Provider, Dictionary<string, object>? overrides = null)    ////////ContractTransaction
        {
            var status = await Status(l2Provider);
            if (status != L2ToL1MessageStatus.CONFIRMED)
            {
                throw new ArbSdkError($"Cannot execute message. Status is: {status} but must be {L2ToL1MessageStatus.CONFIRMED}.");
            }

            var proofInfo = await TryGetProof(l2Provider);
            if (proofInfo == null)
            {
                throw new ArbSdkError($"Unexpected missing proof: {BatchNumber} {IndexInBatch}");
            }

            var outboxAddress = await GetOutboxAddress(l2Provider, (int)BatchNumber);

            var outboxContract = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "Outbox",
                address: outboxAddress,
                isClassic: false
                );

            if (overrides == null)
            { 
                overrides = new Dictionary<string, object>();
            }

            if (!overrides.ContainsKey("from"))
            {
                overrides["from"] = _l1Signer.Account!.Address;
            }
            var txReceipt = await outboxContract.GetFunction("executeTransaction").SendTransactionAndWaitForReceiptAsync(
                                    from: proofInfo.L2Sender,
                                    receiptRequestCancellationToken: null,
                                    BatchNumber.ToString(),   ///////
                                    proofInfo.Proof,
                                    proofInfo.Path,
                                    proofInfo.L2Sender,
                                    proofInfo.L1Dest,
                                    proofInfo.L2Block,
                                    proofInfo.L1Block,
                                    proofInfo.Timestamp,
                                    proofInfo.Amount,
                                    proofInfo.CalldataForL1,
                                    overrides ?? new Dictionary<string, object>()
                                );
            return txReceipt;
        }
    }


}
