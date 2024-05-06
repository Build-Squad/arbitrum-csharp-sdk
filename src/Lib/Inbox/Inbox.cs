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
using Nethereum.Contracts;
using Nethereum.RPC.Eth;
using Nethereum.Contracts.CQS;
using Nethereum.Util;
using Nethereum.ABI.Model;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.QueryHandlers.MultiCall;

namespace Arbitrum.Inbox
{
    //correct my classes
    public class ForceInclusionParams
    {
        public FetchedEvent<MessageDeliveredEvent>? Event { get; set; }
        public string? DelayedAcc { get; set; }


        public class GasComponents
        {
            public BigInteger? GasEstimate { get; set; }
            public BigInteger? GasEstimateForL1 { get; set; }
            public BigInteger? BaseFee { get; set; }
            public BigInteger? L1BaseFeeEstimate { get; set; }
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
            private readonly L1Network _l1Network;
            private readonly L2Network _l2Network;
            private readonly Account _l1Signer;
            public InboxTools(Account l1Signer, L2Network l2Network)
            {
                _l1Provider = SignerProviderUtils.GetProviderOrThrow(l1Signer);
                _l1Network = NetworkUtils.l1Networks[l2Network.PartnerChainID];
                _l2Network = l2Network;
                _l1Signer = l1Signer;
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
                var nodeInterface = await LoadContractUtils.LoadContract(
                                            contractName: "NodeInterface",
                                            provider: l2Provider,
                                            address: Constants.NODE_INTERFACE_ADDRESS,
                                            isClassic: false
                                        );

                var contractCreation = IsContractCreation(transactionl2Request);

                var gasComponents = await nodeInterface.GetFunction("gasEstimateComponents").CallAsync<GasComponents>(
                                    transactionl2Request.To ?? "0x0000000000000000000000000000000000000000",
                                    contractCreation,
                                    transactionl2Request.Data,
                                    new CallInput()
                                    {
                                        From = transactionl2Request.From,
                                        Value = new HexBigInteger(transactionl2Request.Value.ToString())
                                    });

                gasComponents = LoadContractUtils.FormatContractOutput(nodeInterface, "gasEstimateComponents", gasComponents);

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

            //just for testing
            private async Task<(int, int)> GetForceIncludableBlockRange(int blockNumberRangeSize)
            {
                return (1, 3);
            }
            //private async Task<(int, int)> GetForceIncludableBlockRange(int blockNumberRangeSize)
            //{
            //    var sequencerInbox = await LoadContractUtils.LoadContract(
            //                                contractName: "SequencerInbox",
            //                                provider: _l1Provider,
            //                                address: _l2Network?.EthBridge?.SequencerInbox,
            //                                isClassic: false
            //                            );

            //    ABIEncode encoder = new ABIEncode();


            //    var multicall = await MultiCaller.FromProvider(_l1Provider);

            //    var multicallInput = new List<NewCallInput<ParameterOutput>>
            //{
            //    new NewCallInput<ParameterOutput>
            //    {
            //        TargetAddr = sequencerInbox.Address,
            //        Encoder = new Func<byte[]>(() =>
            //        {
            //            var functionAbi = sequencerInbox.ContractBuilder.GetFunctionAbi("getBlockNumber");
            //            return encoder.GetABIEncoded(functionAbi);
            //        }),
            //        Decoder = new Func<string, ParameterOutput>((returnData) =>
            //        {
            //            var decodedData = sequencerInbox.GetFunction("getBlockNumber").DecodeInput(returnData);
            //            return decodedData.FirstOrDefault();
            //        })
            //    },
            //    await multicall.GetBlockNumberInput(),
            //    await multicall.GetCurrentBlockTimestampInput()

            //};

            //    var a = new MulticallInputOutput<>

            //    var multicallOutput = await new MultiQueryHandler(_l1Provider.Client).MultiCallAsync();


            //    //var multicallOutput = await multicall.MultiCall(multicallInput, true);
            //    var maxTimeVariation = multicallOutput[0];
            //    var currentBlockNumber = multicallOutput[1];
            //    var currentBlockTimestamp = multicallOutput[2];

            //    var firstEligibleBlockNumber = currentBlockNumber - maxTimeVariation.DelayBlocks;
            //    var firstEligibleTimestamp = currentBlockTimestamp - maxTimeVariation.DelaySeconds;

            //    var firstEligibleBlock = await FindFirstBlockBelow(firstEligibleBlockNumber, firstEligibleTimestamp);

            //    return (firstEligibleBlock.Number, firstEligibleBlock.Number - blockNumberRangeSize);
            //}

            private async Task<List<FetchedEvent<MessageDeliveredEvent>>> GetEventsAndIncreaseRange(Contract bridge, int searchRangeBlocks, int maxSearchRangeBlocks, int rangeMultiplier)
            {
                //if (bridge == null)              ///////////
                //{
                //    bridge = await LoadContractUtils.LoadContract(
                //                contractName: "Bridge",
                //                provider: _l1Provider,
                //                address: _l2Network?.EthBridge?.Bridge,
                //                isClassic: false
                //            );
                //}

                var eFetcher = new EventFetcher(_l1Provider);

                var cappedSearchRangeBlocks = Math.Min(searchRangeBlocks, maxSearchRangeBlocks);

                var argumentFilters = new Dictionary<string, object>();

                var blockRange = await GetForceIncludableBlockRange(cappedSearchRangeBlocks);

                var events = await eFetcher.GetEventsAsync<MessageDeliveredEvent>(
                    contractFactory: bridge,
                    eventName: "MessageDelivered",
                    argumentFilters: argumentFilters,
                    filter: new NewFilterInput()
                    {
                        FromBlock = new BlockParameter(blockRange.Item1.ToHexBigInteger()),
                        ToBlock = new BlockParameter(blockRange.Item2.ToHexBigInteger()),
                        Address = new string[] { bridge?.Address }
                    });

                if (events.Count() != 0) return events!;

                else if (cappedSearchRangeBlocks == maxSearchRangeBlocks) return new List<FetchedEvent<MessageDeliveredEvent>>();
                else
                {
                    return await GetEventsAndIncreaseRange(bridge, searchRangeBlocks * rangeMultiplier, maxSearchRangeBlocks, rangeMultiplier);
                }
            }

            public async Task<ForceInclusionParams?> GetForceIncludableEvent(int maxSearchRangeBlocks = 3 * 6545, int startSearchRangeBlocks = 100, int rangeMultiplier = 2)
            {
                var bridge = await LoadContractUtils.LoadContract(
                                            contractName: "Bridge",
                                            provider: _l1Provider,
                                            address: _l2Network?.EthBridge?.Bridge,
                                            isClassic: false
                                        );

                var events = await GetEventsAndIncreaseRange(bridge, startSearchRangeBlocks, maxSearchRangeBlocks, rangeMultiplier);

                if (events.Count == 0) return null;

                var eventInfo = events[^1];

                var sequencerInbox = await LoadContractUtils.LoadContract(
                                            contractName: "SequencerInbox",
                                            provider: _l1Provider,
                                            address: _l2Network?.EthBridge?.Bridge,
                                            isClassic: false
                                        );

                var totalDelayedRead = await sequencerInbox.GetFunction("totalDelayedMessagesRead").CallAsync<BigInteger>();

                if (totalDelayedRead > eventInfo.Event.MessageIndex) return null;

                var delayedAcc = await bridge.GetFunction("delayedInboxAccs").CallAsync<string>(eventInfo.Event.MessageIndex);

                return new ForceInclusionParams { Event = eventInfo, DelayedAcc = delayedAcc };
            }

            public async Task<TransactionReceipt?> ForceInclude(ForceInclusionParams? messageDeliveredEvent = null, Overrides? overrides = null)
            {
                var sequencerInbox = await LoadContractUtils.LoadContract(
                                            contractName: "SequencerInbox",
                                            provider: _l1Provider,
                                            address: _l2Network?.EthBridge?.Bridge,
                                            isClassic: false
                                        );

                var eventInfo = messageDeliveredEvent ?? await GetForceIncludableEvent();

                if (eventInfo == null) return null;
                var block = await _l1Provider.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(eventInfo?.Event?.BlockHash);

                return await sequencerInbox.GetFunction("forceInclusion").SendTransactionAndWaitForReceiptAsync(
                    from: _l1Signer?.Address,      ////////////////
                    receiptRequestCancellationToken: null,
                    eventInfo?.Event?.Event?.MessageIndex + 1,
                    eventInfo?.Event?.Event?.Kind,
                    new List<dynamic> { eventInfo?.Event?.BlockNumber, block?.Timestamp, },
                    eventInfo?.Event?.Event?.BaseFeeL1,
                    eventInfo?.Event?.Event?.Sender,
                    eventInfo?.Event?.Event?.MessageDataHash,
                    // we need to pass in {} because if overrides is undefined it thinks we've provided too many params
                    overrides ?? new Overrides());
            }

            public async Task<TransactionReceipt?> SendL2SignedTx(string signedTx)
            {
                var delayedInbox = await LoadContractUtils.LoadContract(
                                            contractName: "IInbox",
                                            provider: _l1Provider,
                                            address: _l2Network?.EthBridge?.Inbox,
                                            isClassic: false
                                        );

                var messageType = (byte)InboxMessageKind.L2MessageType_signedTx;

                var packedMessageType = new byte[] { messageType };

                var sendDataBytes = ConcatArrays(packedMessageType, signedTx);

                var txReceipt = await delayedInbox.GetFunction("sendL2Message").SendTransactionAndWaitForReceiptAsync(
                    from: _l1Signer?.Address,      ////////////////
                    receiptRequestCancellationToken: null,
                    sendDataBytes
                    );
                return txReceipt;
            }

            public async Task<string> SignL2Tx(TransactionRequest txRequest, Account l2Signer)
            {
                var tx = new TransactionRequest
                {
                    Data = txRequest?.Data,
                    Value = txRequest?.Value,
                    To = txRequest?.To,
                    Gas = txRequest?.Gas,
                    GasPrice = txRequest?.GasPrice,
                    MaxFeePerGas = txRequest?.MaxFeePerGas,
                    MaxPriorityFeePerGas = txRequest?.MaxPriorityFeePerGas,
                    Nonce = txRequest?.Nonce,
                    ChainId = new HexBigInteger(l2Signer?.ChainId.ToString()),
                    Type = txRequest?.Type,
                    From = l2Signer?.Address,
                    AccessList = txRequest?.AccessList,
                };

                var contractCreation = IsContractCreation(tx);

                //if (tx.Nonce == null) tx.Nonce = await l2Signer?.NonceService.GetNextNonceAsync(); ////////

                if (tx.Nonce == null) tx.Nonce = await new Web3(l2Signer?.TransactionManager.Client).Eth.Transactions.GetTransactionCount.SendRequestAsync(l2Signer?.Address);  ////////

                if (string.IsNullOrEmpty(tx.To)) tx.To = AddressUtil.Current.ConvertToChecksumAddress("0x0000000000000000000000000000000000000000");

                if (tx.Type.Value == 1 || tx.GasPrice != null)
                {
                    if (tx.GasPrice == null) tx.GasPrice = await new Web3(l2Signer?.TransactionManager?.Client)?.Eth?.GasPrice.SendRequestAsync();
                }
                else
                {
                    if (tx.MaxFeePerGas == null)
                    {
                        var feeHistory = new Web3(l2Signer?.TransactionManager?.Client)?.Eth?.FeeHistory;

                        var feeData = await feeHistory?.SendRequestAsync(blockCount: new HexBigInteger(1), BlockParameter.CreateLatest(), rewardPercentiles: new decimal[] { });

                        var baseFee = feeData?.BaseFeePerGas[0];

                        var priorityFee = Nethereum.Util.UnitConversion.Convert.ToWei(2, Nethereum.Util.UnitConversion.EthUnit.Gwei);

                        tx.MaxPriorityFeePerGas = new HexBigInteger(priorityFee);

                        tx.MaxFeePerGas = new HexBigInteger(baseFee + priorityFee);
                    }
                    tx.Type = new HexBigInteger(2);
                }

                tx.From = l2Signer?.Address;

                tx.ChainId = new HexBigInteger(l2Signer?.ChainId.ToString());

                // if this is contract creation, user might not input the to address,
                // however, it is needed when we call to estimateArbitrumGas, so
                // we add a zero address here.
                if (tx.To == null)
                {
                    tx.To = "0x0000000000000000000000000000000000000000";
                }

                try
                {
                    if (tx.To == null)
                    {
                        tx.Gas = new HexBigInteger((await EstimateArbitrumGas(tx, new Web3(l2Signer?.TransactionManager?.Client)))?.GasEstimateForL2.ToString());

                    }
                }
                catch (Exception)
                {
                    throw new ArbSdkError("Execution failed (estimate gas failed)");
                }

                if (contractCreation)
                {
                    tx.To = null;
                }
                return await l2Signer?.TransactionManager.SignTransactionAsync(tx);
            }

            private byte[] ConcatArrays(byte[] array1, string array2)
            {
                var result = new byte[array1.Length + array2.Length];
                array1.CopyTo(result, 0);
                System.Text.Encoding.ASCII.GetBytes(array2).CopyTo(result, array1.Length);
                return result;
            }
        }
    }
}
