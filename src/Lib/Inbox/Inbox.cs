using Arbitrum.ContractFactory.Bridge;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.src.Lib.DataEntities;
using Arbitrum.Utils;
using NBitcoin.Logging;
using Nethereum.ABI;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;

namespace Arbitrum.Inbox
{
    //correct my classes
    public class ForceInclusionParams
    {
        public FetchedEvent<MessageDeliveredEventDTO>? Event { get; set; }
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
            private readonly SignerOrProvider _l1Signer;
            public InboxTools(SignerOrProvider l1Signer, L2Network l2Network)
            {
                _l1Provider = SignerProviderUtils.GetProviderOrThrow(l1Signer);
                _l1Network = NetworkUtils.l1Networks[l2Network.PartnerChainID];
                _l2Network = l2Network;
                _l1Signer = l1Signer;
            }

            private async Task<BlockWithTransactions> FindFirstBlockBelow(int blockNumber, int blockTimestamp)
            {
                var block = await _l1Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockNumber.ToHexBigInteger());
                var diff = block.Timestamp.Value - blockTimestamp;
                if (diff < 0) return block;

                var diffBlocks = Math.Max((int)Math.Ceiling((double)diff / _l1Network.BlockTime), 10);

                return await FindFirstBlockBelow(blockNumber - diffBlocks, blockTimestamp);
            }

            private bool IsContractCreation(TransactionRequest transactionl2Request)
            {
                return string.IsNullOrEmpty(transactionl2Request.To) || transactionl2Request.To == "0x" || 
                    transactionl2Request.To == "0x0000000000000000000000000000000000000000";
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
                                        Value = transactionl2Request.Value
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

            private async Task<List<FetchedEvent<MessageDeliveredEventDTO>>> GetEventsAndIncreaseRange(Contract bridge, int searchRangeBlocks, int maxSearchRangeBlocks, int rangeMultiplier)
            {
                var eFetcher = new EventFetcher(_l1Provider);

                var cappedSearchRangeBlocks = Math.Min(searchRangeBlocks, maxSearchRangeBlocks);

                var argumentFilters = new Dictionary<string, object>();

                var blockRange = await GetForceIncludableBlockRange(cappedSearchRangeBlocks);

                var events = await eFetcher.GetEventsAsync<MessageDeliveredEventDTO>(
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

                else if (cappedSearchRangeBlocks == maxSearchRangeBlocks) return new List<FetchedEvent<MessageDeliveredEventDTO>>();
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
                    from: _l1Signer?.Account.Address,      ////////////////
                    receiptRequestCancellationToken: null,
                    eventInfo?.Event?.Event?.MessageIndex + 1,
                    eventInfo?.Event?.Event?.Kind,
                    new List<dynamic> { eventInfo?.Event?.BlockNumber, block?.Timestamp, },
                    eventInfo?.Event?.Event?.BaseFeeL1,
                    eventInfo?.Event?.Event?.Sender,
                    eventInfo?.Event?.Event?.MessageDataHash,
                    overrides ?? new Overrides());
            }

            public async Task<TransactionReceipt?> SendL2SignedTx(string signedTx)
            {
                var messageType = (byte)InboxMessageKind.L2MessageType_signedTx;
                var packedMessageType = "0x" + messageType.ToString("X2");

                var sendDataBytes = SendDataBytes(packedMessageType, signedTx);

                var delayedInbox = await LoadContractUtils.DeployAbiContract(
                                            contractName: "Inbox",
                                            provider: _l1Provider,
                                            deployer: _l1Signer,
                                            isClassic: false,
                                            constructorArgs: new object[] { new BigInteger(sendDataBytes.Length) }
                                        );                

                var fn = delayedInbox.GetFunction("sendL2Message");

                var tx = new TransactionInput()
                {
                    From = _l1Signer?.Account?.Address,
                    Gas = await fn.EstimateGasAsync(new object[] { sendDataBytes })
                };

                var txReceipt = await fn.SendTransactionAndWaitForReceiptAsync(tx, functionInput: new object[] { sendDataBytes });

                return txReceipt;
            }

            public async Task<string> SignL2Tx(TransactionRequest txRequest, SignerOrProvider l2Signer)
            {
                // Initialize the transaction by copying properties from txRequest
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
                    ChainId = txRequest.ChainId,
                    Type = txRequest?.Type,
                    From = l2Signer?.Account.Address,
                    AccessList = txRequest?.AccessList,
                };

                // Determine if this is a contract creation transaction
                var contractCreation = IsContractCreation(tx);

                // Check and set the nonce if not provided
                tx.Nonce ??= await l2Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(l2Signer?.Account.Address);

                // Handle gas price or fee parameters based on transaction type
                if (tx.Type?.Value == 1 || tx.GasPrice != null)
                {
                    tx.GasPrice ??= await l2Signer.Provider?.Eth?.GasPrice.SendRequestAsync();
                }
                else
                {
                    if (tx.MaxFeePerGas == null)
                    {
                        var feeHistory = l2Signer.Provider?.Eth?.FeeHistory;
                        var feeData = await feeHistory?.SendRequestAsync(blockCount: BigInteger.One.ToHexBigInteger(), BlockParameter.CreateLatest(), rewardPercentiles: new decimal[] { });
                        var baseFee = feeData?.BaseFeePerGas[0];
                        var priorityFee = UnitConversion.Convert.ToWei(2, UnitConversion.EthUnit.Gwei);

                        tx.MaxPriorityFeePerGas = priorityFee.ToHexBigInteger();
                        tx.MaxFeePerGas = (baseFee.Value + priorityFee).ToHexBigInteger();
                    }

                    tx.Type = 2.ToHexBigInteger();
                }

                tx.From = l2Signer?.Account.Address;
                tx.ChainId = await l2Signer?.Provider.Eth.ChainId.SendRequestAsync();

                // Check and set the "to" address if not provided
                if (string.IsNullOrEmpty(tx.To))
                {
                    tx.To = AddressUtil.Current.ConvertToChecksumAddress("0x0000000000000000000000000000000000000000");
                }

                // Estimate gas if required and handle exceptions
                try
                {
                    tx.Gas ??= (await EstimateArbitrumGas(tx, l2Signer.Provider))?.GasEstimateForL2.Value.ToHexBigInteger();
                }
                catch (Exception)
                {
                    throw new ArbSdkError("Execution failed (estimate gas failed)");
                }

                // Remove the "to" field for contract creation transactions
                if (contractCreation)
                {
                    tx.To = null;
                }

                // Sign the transaction
                return await l2Signer?.Provider.TransactionManager.SignTransactionAsync(tx);
            }

            private static byte[] SendDataBytes(string messageType, string data)
            {
                byte[] messageTypeBytes = HexStringToByteArray(messageType.Substring(2));
                byte[] dataBytes = HexStringToByteArray(data);
                byte[] result = new byte[messageTypeBytes.Length + dataBytes.Length];

                Buffer.BlockCopy(messageTypeBytes, 0, result, 0, messageTypeBytes.Length);
                Buffer.BlockCopy(dataBytes, 0, result, messageTypeBytes.Length, dataBytes.Length);

                return result;
            }

            private static byte[] HexStringToByteArray(string hex)
            {
                int numberChars = hex.Length;
                byte[] bytes = new byte[numberChars / 2];
                for (int i = 0; i < numberChars; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }
                return bytes;
            }
        }
    }
}
