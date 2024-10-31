using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Serilog;
using System.Numerics;
using static Arbitrum.DataEntities.NetworkUtils;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.Message
{
    public static class L1ToL2MessageUtils
    {
        public enum L1ToL2MessageStatus
        {
            /**
             * The retryable ticket has yet to be created
             */
            NOT_YET_CREATED = 1,
            /**
             * An attempt was made to create the retryable ticket, but it failed.
             * This could be due to not enough submission cost being paid by the L1 transaction
             */
            CREATION_FAILED = 2,
            /**
             * The retryable ticket has been created but has not been redeemed. This could be due to the
             * auto redeem failing, or if the params (max l2 gas price) * (max l2 gas) = 0 then no auto
             * redeem tx is ever issued. An auto redeem is also never issued for ETH deposits.
             * A manual redeem is now required.
             */
            FUNDS_DEPOSITED_ON_L2 = 3,
            /**
             * The retryable ticket has been redeemed (either by auto, or manually) and the
             * l2 transaction has been executed
             */
            REDEEMED = 4,
            /**
             * The message has either expired or has been canceled. It can no longer be redeemed.
             */
            EXPIRED = 5
        }

        public enum EthDepositStatus
        {
            /**
             * ETH is not deposited on L2 yet
             */
            PENDING = 1,
            /**
             * ETH is deposited successfully on L2
             */
            DEPOSITED = 2
        }

        /** 
          * Left-pads a byte array with zeros to reach the specified length.
          * <param name="value">The byte array to pad.</param>
          * <param name="length">The desired length of the padded byte array.</param>
          * <returns>A new byte array padded with zeros on the left.</returns>
         **/
        public static byte[] ZeroPad(byte[] value, int length)
        {
            // If the original byte array's length is already greater than or equal to the desired length,
            // simply return the original array.
            if (value.Length >= length)
            {
                return value;
            }

            // If the original byte array's length is less than the desired length, pad it with zeros.
            else
            {
                // Create a new byte array with the desired length.
                byte[] paddedBytes = new byte[length];

                // Copy the original bytes into the new byte array, leaving the leading bytes as zeros.
                Array.Copy(value, 0, paddedBytes, length - value.Length, value.Length);

                // Return the padded byte array.
                return paddedBytes;
            }
        }

        public static byte[] HexToBytes(string value)
        {
            return value.HexToByteArray();
        }


        public static byte[] Concat(params object[] args)
        {
            IEnumerable<byte[]> objects;

            if (args.Length == 1 && (args[0] is IList<byte> || args[0] is IList<byte[]>))
            {
                objects = ((IEnumerable<byte[]>)args[0]).Select(item => item.ToArray());
            }
            else
            {
                objects = args.Select(arg =>
                {
                    if (arg is byte)
                    {
                        return new byte[] { (byte)arg };
                    }
                    else if (arg is byte[])
                    {
                        return (byte[])arg;
                    }
                    else if (arg is IList<byte>)
                    {
                        return ((IEnumerable<byte>)arg).ToArray();
                    }
                    else if (arg is IList<byte[]>)
                    {
                        return ((IEnumerable<byte[]>)arg).SelectMany(b => b).ToArray();
                    }
                    else
                    {
                        throw new ArgumentException("Invalid argument type: " + arg.GetType());
                    }
                });
            }

            var length = objects.Sum(item => item.Length);

            var result = new byte[length];

            var offset = 0;
            foreach (var obj in objects)
            {
                Array.Copy(obj, 0, result, offset, obj.Length);
                offset += obj.Length;
            }

            return result;
        }
    }

    /**
     * Conditional type for Signer or Provider. If T is of type Provider
     * then L1ToL2MessageReaderOrWriter<T> will be of type L1ToL2MessageReader.
     * If T is of type Signer then L1ToL2MessageReaderOrWriter<T> will be of
     * type L1ToL2MessageWriter.
     */
    public class L1ToL2MessageReaderOrWriter : L1ToL2Message
    {
        private readonly L1ToL2MessageReader? reader;
        private readonly L1ToL2MessageWriter? writer;

        public BigInteger ChainId { get; }
        public string? Sender { get; }
        public BigInteger MessageNumber { get; }
        public BigInteger L1BaseFee { get; }
        public RetryableMessageParams? MessageData { get; }
        public string? RetryableCreationId { get; }


        public L1ToL2MessageReaderOrWriter(Web3 l2Provider, BigInteger chainId, string sender, BigInteger messageNumber, BigInteger l1BaseFee, RetryableMessageParams messageData)
            : base(chainId, sender, messageNumber, l1BaseFee, messageData)
        {
            reader = new L1ToL2MessageReader(l2Provider, chainId, sender, messageNumber, l1BaseFee, messageData);
            writer = null;
            ChainId = chainId;
            Sender = sender;
            MessageNumber = messageNumber;
            L1BaseFee = l1BaseFee;
            MessageData = messageData;
            RetryableCreationId = CalculateSubmitRetryableId(
                ChainId,
                Sender,
                MessageNumber,
                L1BaseFee,
                MessageData?.DestAddress.ToString(),
                MessageData.L2CallValue,
                MessageData.L1Value,
                MessageData.MaxSubmissionFee,
                MessageData?.ExcessFeeRefundAddress.ToString(),
                MessageData?.CallValueRefundAddress.ToString(),
                MessageData!.GasLimit,
                MessageData.MaxFeePerGas,
                MessageData?.Data.ToString());
        }

        public L1ToL2MessageReaderOrWriter(SignerOrProvider l2SignerOrProvider, BigInteger chainId, string sender, BigInteger messageNumber, BigInteger l1BaseFee, RetryableMessageParams messageData)
            : base(chainId, sender, messageNumber, l1BaseFee, messageData)
        {
            reader = null;
            writer = new L1ToL2MessageWriter(l2SignerOrProvider, chainId, sender, messageNumber, l1BaseFee, messageData);
            ChainId = chainId;
            Sender = sender;
            MessageNumber = messageNumber;
            L1BaseFee = l1BaseFee;
            MessageData = messageData;
            RetryableCreationId = CalculateSubmitRetryableId(
                ChainId,
                Sender,
                MessageNumber,
                L1BaseFee,
                MessageData?.DestAddress.ToString(),
                MessageData.L2CallValue,
                MessageData.L1Value,
                MessageData.MaxSubmissionFee,
                MessageData?.ExcessFeeRefundAddress.ToString(),
                MessageData?.CallValueRefundAddress.ToString(),
                MessageData!.GasLimit,
                MessageData.MaxFeePerGas,
                MessageData?.Data.ToString());
        }

        public async Task<string> GetBeneficiary()
        {
            return await writer.GetBeneficiary();
        }

        public async Task<BigInteger> GetTimeout()
        {
            return await writer.GetTimeout();
        }

        public async Task<BigInteger> GetLifetime(Web3 l2Provider)
        {
            return await writer.GetLifetime(l2Provider);
        }

        public async Task<L1ToL2MessageWaitResult> WaitForStatus(int? confirmations = null, int? timeout = null)
        {
            return reader != null
                ? await reader.WaitForStatus(confirmations, timeout)
                : await writer.WaitForStatus(confirmations, timeout);
        }

        public async Task<L1ToL2MessageStatus> Status()
        {
            return await writer.Status();
        }

        public async Task<L1ToL2MessageWaitResult> GetSuccessfulRedeem()
        {
            return await writer.GetSuccessfulRedeem();
        }

        public async Task<TransactionReceipt?> GetAutoRedeemAttempt()
        {
            return await writer.GetAutoRedeemAttempt();
        }
        public async Task<TransactionReceipt?> GetRetryableCreationReceipt(int? confirmations = null, int? timeout = null)
        {
            return await writer.GetRetryableCreationReceipt(confirmations, timeout);
        }

        public async Task<RedeemTransaction> Redeem(Dictionary<string, object>? overrides = null)
        {
            return await writer.Redeem(overrides);
        }

        public async Task<TransactionReceipt> Cancel(Dictionary<string, object>? overrides = null)
        {
            return await writer.Cancel(overrides);
        }

        public async Task<TransactionReceipt> KeepAlive(Dictionary<string, object>? overrides = null)
        {
            return await writer.KeepAlive(overrides);
        }
    }

    public class L1ToL2Message
    {
        /**
         * When messages are sent from L1 to L2 a retryable ticket is created on L2.
         * The retryableCreationId can be used to retrieve information about the success or failure of the
         * creation of the retryable ticket.
         */
        public BigInteger ChainId { get; }
        public string Sender { get; }
        public BigInteger MessageNumber { get; }
        public BigInteger L1BaseFee { get; }
        public RetryableMessageParams? MessageData { get; }
        public string RetryableCreationId { get; }

        public L1ToL2Message(
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData)
        {
            ChainId = chainId;
            Sender = sender;
            MessageNumber = messageNumber;
            L1BaseFee = l1BaseFee;
            MessageData = messageData;
            RetryableCreationId = CalculateSubmitRetryableId(
                ChainId,
                Sender,
                MessageNumber,
                L1BaseFee,
                MessageData?.DestAddress.ToString(),
                MessageData.L2CallValue,
                MessageData.L1Value,
                MessageData.MaxSubmissionFee,
                MessageData?.ExcessFeeRefundAddress.ToString(),
                MessageData?.CallValueRefundAddress.ToString(),
                MessageData!.GasLimit,
                MessageData.MaxFeePerGas,
                MessageData?.Data.ToString());
        }

        /**
         * The submit retryable transactions use the typed transaction envelope 2718.
         * The id of these transactions is the hash of the RLP encoded transaction.
         * @param l2ChainId
         * @param fromAddress the aliased address that called the L1 inbox as emitted in the bridge event.
         * @param messageNumber
         * @param l1BaseFee
         * @param destAddress
         * @param l2CallValue
         * @param l1Value
         * @param maxSubmissionFee
         * @param excessFeeRefundAddress refund address specified in the retryable creation. Note the L1 inbox aliases this address if it is a L1 smart contract. The user is expected to provide this value already aliased when needed.
         * @param callValueRefundAddress refund address specified in the retryable creation. Note the L1 inbox aliases this address if it is a L1 smart contract. The user is expected to provide this value already aliased when needed.
         * @param gasLimit
         * @param maxFeePerGas
         * @param data
         * @returns
         */
        public static string CalculateSubmitRetryableId(
            BigInteger l2ChainId,
            string fromAddress,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            string destAddress,
            BigInteger l2CallValue,
            BigInteger l1Value,
            BigInteger maxSubmissionFee,
            string excessFeeRefundAddress,
            string callValueRefundAddress,
            BigInteger gasLimit,
            BigInteger maxFeePerGas,
            string data)
        {
            string fromAddr = AddressUtil.Current.ConvertToChecksumAddress(fromAddress);
            string destAddr = destAddress == "0x" ? "0x" : AddressUtil.Current.ConvertToChecksumAddress(destAddress);
            string callValueRefundAddr = AddressUtil.Current.ConvertToChecksumAddress(callValueRefundAddress);
            string excessFeeRefundAddr = AddressUtil.Current.ConvertToChecksumAddress(excessFeeRefundAddress);

            var fields = new List<byte[]>
            {
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(l2ChainId))),
                RLP.EncodeElement(ZeroPad(FormatNumber(messageNumber), 32)),
                RLP.EncodeElement(fromAddr.HexToByteArray()),
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(l1BaseFee))),
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(l1Value))),
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(maxFeePerGas))),
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(gasLimit))),
                destAddr != "0x" ? RLP.EncodeElement(destAddr.HexToByteArray()) : new byte[0],
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(l2CallValue))),
                RLP.EncodeElement(callValueRefundAddr.HexToByteArray()),
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(maxSubmissionFee))),
                RLP.EncodeElement(excessFeeRefundAddr.HexToByteArray()),
                RLP.EncodeElement(data.HexToByteArray())
            };

            var rlpEncodedFields = RLP.EncodeList(fields.ToArray());

            var rlpWithTypePrefix = new byte[] { 0x69 }.Concat(rlpEncodedFields).ToArray();
            var retryableTxId = new Sha3Keccack().CalculateHash(rlpWithTypePrefix);

            return retryableTxId.ToHex();
        }

        private static byte[] FormatNumber(BigInteger number)
        {
            return number.ToByteArray().Reverse().ToArray();
        }

        private static byte[] ZeroPad(byte[] input, int length)
        {
            if (input.Length >= length) return input;
            var padded = new byte[length];
            Array.Copy(input, 0, padded, length - input.Length, input.Length);
            return padded;
        }

        private static byte[] TrimLeadingZeros(byte[] input)
        {
            int i = 0;
            while (i < input.Length && input[i] == 0) i++;
            return input.Skip(i).ToArray();
        }

        public async Task<L1ToL2MessageWaitResult> WaitForStatus(Web3 l2Provider, int? confirmations = null, int? timeout = null)
        {
            var reader = new L1ToL2MessageReader(l2Provider, ChainId, Sender, MessageNumber, L1BaseFee, MessageData);
            return await reader.WaitForStatus();
        }

        public static L1ToL2MessageReaderOrWriter FromEventComponents(
            dynamic l2SignerOrProvider,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData)
        {
            if (SignerProviderUtils.IsSigner(l2SignerOrProvider))
            {
                return new L1ToL2MessageReaderOrWriter(
                    l2SignerOrProvider,
                    chainId,
                    sender,
                    messageNumber,
                    l1BaseFee,
                    messageData);
            }
            else
            {
                return new L1ToL2MessageReaderOrWriter(
                    l2SignerOrProvider,
                    chainId,
                    sender,
                    messageNumber,
                    l1BaseFee,
                    messageData);
            }
        }
    }

    /**
     * If the status is redeemed an l2TxReceipt is populated.
     * For all other statuses l2TxReceipt is not populated
     */
    public class L1ToL2MessageWaitResult
    {
        public L1ToL2MessageStatus Status { get; set; }
        public TransactionReceipt? L2TxReceipt { get; set; }
    }
    public class L1EthDepositTransactionReceiptResults
    {
        public bool Complete { get; set; }
        public EthDepositMessage? Message { get; set; }
        public TransactionReceipt? L2TxReceipt { get; set; }
    }

    public class L1ContractCallTransactionReceiptResults
    {
        public bool Complete { get; set; }
        public L1ToL2MessageReaderOrWriter? Message { get; set; }
        public L1ToL2MessageWaitResult? Result { get; set; }
    }

    public class EthDepositMessageWaitResult : TransactionReceipt
    {
        public TransactionReceipt? L2TxReceipt { get; set; }
    }

    public class L1ToL2MessageReader : L1ToL2Message
    {
        public Web3 _l2Provider;
        public TransactionReceipt? _retryableCreationReceipt;

        public TransactionReceipt? RetryableCreationReceipt
        {
            get { return _retryableCreationReceipt; }
            set { _retryableCreationReceipt = value; }
        }
        public L1ToL2MessageReader(
            Web3 l2Provider,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData) : base(chainId, sender, messageNumber, l1BaseFee, messageData)
        {
            _l2Provider = l2Provider;
        }

        /**
         * Try to get the receipt for the retryable ticket creation.
         * This is the L2 transaction that creates the retryable ticket.
         * If confirmations or timeout is provided, this will wait for the ticket to be created
         * @returns Null if retryable has not been created
         */
        public async Task<TransactionReceipt?> GetRetryableCreationReceipt(int? confirmations = null, int? timeout = null)
        {
            _retryableCreationReceipt ??= await Lib.GetTransactionReceiptAsync(_l2Provider, RetryableCreationId, confirmations, timeout);
            return _retryableCreationReceipt;
        }

        /**
         * When retryable tickets are created, and gas is supplied to it, an attempt is
         * made to redeem the ticket straight away. This is called an auto redeem.
         * @returns TransactionReceipt of the auto redeem attempt if exists, otherwise null
         */
        public async Task<TransactionReceipt?> GetAutoRedeemAttempt()
        {
            var creationReceipt = await GetRetryableCreationReceipt();

            if (creationReceipt != null)
            {
                var l2Receipt = new L2TransactionReceipt(creationReceipt);
                var redeemEvents = await l2Receipt.GetRedeemScheduledEvents(_l2Provider);

                if (redeemEvents.Count() == 1)
                {
                    return await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(redeemEvents?.FirstOrDefault()?.Event?.RetryTxHash.ToHex());
                }
                else if (redeemEvents.Count() > 1)
                {
                    throw new ArbSdkError($"Unexpected number of redeem events for retryable creation tx. {creationReceipt} {redeemEvents}");
                }
            }

            return null;
        }

        /**
         * Receipt for the successful l2 transaction created by this message.
         * @returns TransactionReceipt of the first successful redeem if exists, otherwise the current status of the message.
         */
        public async Task<L1ToL2MessageWaitResult> GetSuccessfulRedeem()
        {
            var l2Network = await GetL2Network(_l2Provider);
            var eventFetcher = new EventFetcher(_l2Provider);
            var creationReceipt = await GetRetryableCreationReceipt();

            if (creationReceipt == null)
            {
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.NOT_YET_CREATED };
            }

            if (creationReceipt.Status.Value == 0)
            {
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.CREATION_FAILED };
            }

            var autoRedeem = await GetAutoRedeemAttempt();
            if (autoRedeem != null && autoRedeem.Status.Value == 1)
            {
                return new L1ToL2MessageWaitResult { L2TxReceipt = autoRedeem, Status = L1ToL2MessageStatus.REDEEMED };
            }

            if (await RetryableExists())
            {
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2 };
            }

            int increment = 1000;
            var fromBlock = await _l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new BlockParameter(creationReceipt.BlockNumber));

            int timeout = (int)fromBlock.Timestamp.Value + l2Network.RetryableLifetimeSeconds;

            var queriedRange = new List<(int from, int to)>();

            var maxBlock = await _l2Provider.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            while ((int)fromBlock.Number.Value < (int)maxBlock.Value)
            {
                int toBlockNumber = Math.Min((int)fromBlock.Number.Value + increment, (int)maxBlock.Value);

                var outerBlockRange = ((int)fromBlock.Number.Value, toBlockNumber);

                queriedRange.Add(outerBlockRange);

                var redeemEvents = await eventFetcher.GetEventsAsync<RedeemScheduledEventDTO>(
                                    contractFactory: "ArbRetryableTx",
                                    eventName: "RedeemScheduled",
                                    argumentFilters: new Dictionary<string, object> { { "ticketId", RetryableCreationId } },
                                    filter: new NewFilterInput
                                    {
                                        FromBlock = new BlockParameter(outerBlockRange.Item1.ToHexBigInteger()),
                                        ToBlock = new BlockParameter(outerBlockRange.Item2.ToHexBigInteger()),
                                        Address = new string[] { Constants.ARB_RETRYABLE_TX_ADDRESS }
                                    },
                                    isClassic: false);

                var successfulRedeem = new List<TransactionReceipt>();

                foreach (var e in redeemEvents)
                {
                    var receipt = await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(e.TransactionHash);
                    if (receipt != null && receipt.Status.Value == 1)
                    {
                        successfulRedeem.Add(receipt);
                    }
                }

                if (successfulRedeem.Count > 1)
                {
                    throw new ArbSdkError($"Unexpected number of successful redeems. Expected only one redeem for ticket {RetryableCreationId}, but found {successfulRedeem.Count}.");
                }

                if (successfulRedeem.Count == 1)
                {
                    return new L1ToL2MessageWaitResult { L2TxReceipt = successfulRedeem.First(), Status = L1ToL2MessageStatus.REDEEMED };
                }

                var toBlock = await _l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(toBlockNumber.ToHexBigInteger());
                if (toBlock.Timestamp.Value > timeout)
                {
                    while (queriedRange.Count > 0)
                    {
                        var blockRange = queriedRange.First();
                        var keepAliveEvents = await eventFetcher.GetEventsAsync<LifetimeExtendedEventDTO>(
                                                contractFactory: "ArbRetryableTx",
                                                eventName: "LifetimeExtended",
                                                argumentFilters: new Dictionary<string, object> { { "ticketId", RetryableCreationId } },
                                                filter: new NewFilterInput
                                                {
                                                    FromBlock = new BlockParameter(blockRange.from.ToHexBigInteger()),
                                                    ToBlock = new BlockParameter(blockRange.to.ToHexBigInteger()),
                                                    Address = new string[] { Constants.ARB_RETRYABLE_TX_ADDRESS }
                                                },
                                                isClassic: false);

                        if (keepAliveEvents.Count > 0)
                        {
                            timeout = keepAliveEvents
                                .Select(e => e.Event.NewTimeout)
                                .Select(t => (int)t!)
                                .OrderByDescending(t => t)
                                .FirstOrDefault();
                            break;
                        }

                        queriedRange.RemoveAt(0);
                    }

                    if (toBlock.Timestamp.Value > timeout)
                    {
                        break;
                    }

                    while (queriedRange.Count > 1)
                    {
                        queriedRange.RemoveAt(0);
                    }
                }

                int processedSeconds = (int)(toBlock.Timestamp.Value - fromBlock.Timestamp.Value);
                if (processedSeconds != 0)
                {
                    increment = (int)Math.Ceiling((decimal)(increment * 86400) / processedSeconds);
                }

                fromBlock = toBlock;
            }

            return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.EXPIRED };
        }

        /**
         * Has this message expired. Once expired the retryable ticket can no longer be redeemed.
         * @deprecated Will be removed in v3.0.0
         * @returns
         */
        public async Task<bool> IsExpired()
        {
            return await RetryableExists();
        }

        public async Task<bool> RetryableExists()
        {
            var currentTimestamp = (await _l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest())).Timestamp;

            try
            {
                var timeoutTimestamp = await GetTimeout();
                return currentTimestamp <= timeoutTimestamp;
            }
            catch (SmartContractRevertException ex)
            {
                if (ex.EncodedData == "0x80698456")
                {
                    Log.Information("Retryable does not exist.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while checking retryable existence.");
                throw;
            }
            return true;
        }

        public async Task<L1ToL2MessageStatus> Status()
        {
            return (await GetSuccessfulRedeem()).Status;
        }

        /**
         * Wait for the retryable ticket to be created, for it to be redeemed, and for the l2Tx to be executed.
         * Note: The terminal status of a transaction that only does an eth deposit is FUNDS_DEPOSITED_ON_L2 as
         * no L2 transaction needs to be executed, however the terminal state of any other transaction is REDEEMED
         * which represents that the retryable ticket has been redeemed and the L2 tx has been executed.
         * @param confirmations Amount of confirmations the retryable ticket and the auto redeem receipt should have
         * @param timeout Amount of time to wait for the retryable ticket to be created
         * Defaults to 15 minutes, as by this time all transactions are expected to be included on L2. Throws on timeout.
         * @returns The wait result contains a status, and optionally the l2TxReceipt.
         * If the status is "REDEEMED" then a l2TxReceipt is also available on the result.
         * If the status has any other value then l2TxReceipt is not populated.
         */
        public async Task<L1ToL2MessageWaitResult> WaitForStatus(int? confirmations = null, int? timeout = null)
        {
            var l2network = await GetL2Network((int)ChainId);

            var chosenTimeout = timeout.HasValue ? timeout : l2network.DepositTimeout;

            var _retryableCreationReceipt = await GetRetryableCreationReceipt(confirmations, chosenTimeout);

            if (_retryableCreationReceipt == null)
            {
                if (confirmations != null || chosenTimeout != null)
                {
                    throw new ArbSdkError($"Retryable creation script not found {RetryableCreationId}");
                }
            }

            return await GetSuccessfulRedeem();
        }

        /**
         * The minimium lifetime of a retryable tx
         * @returns
         */
        public async Task<BigInteger> GetLifetime(Web3 l2Provider)
        {
            var arbRetryableTxContract = await LoadContractUtils.LoadContract(
                                                contractName: "ArbRetryableTx",
                                                address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                                                provider: l2Provider,
                                                isClassic: false
                                                );

            var getLifetimeFunction = arbRetryableTxContract.GetFunction("getLifetime");

            return await getLifetimeFunction.CallAsync<BigInteger>();
        }

        /**
         * Timestamp at which this message expires
         * @returns
         */
        public async Task<BigInteger> GetTimeout()
        {
            var arbRetryableTxContract = await LoadContractUtils.LoadContract(
                contractName: "ArbRetryableTx",
                address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                provider: _l2Provider,
                isClassic: false
            );

            var getTimeoutFunction = arbRetryableTxContract.GetFunction("getTimeout");
            return await getTimeoutFunction.CallAsync<BigInteger>(RetryableCreationId.HexToByteArray());
        }

        /**
         * Address to which CallValue will be credited to on L2 if the retryable ticket times out or is cancelled.
         * The Beneficiary is also the address with the right to cancel a Retryable Ticket (if the ticket hasn’t been redeemed yet).
         * @returns
         */
        public async Task<string> GetBeneficiary()
        {
            var arbRetryableTxContract = await LoadContractUtils.LoadContract(
                contractName: "ArbRetryableTx",
                address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                provider: _l2Provider,
                isClassic: false
            );

            var getBeneficiaryFunction = arbRetryableTxContract.GetFunction("getBeneficiary");
            return await getBeneficiaryFunction.CallAsync<string>(RetryableCreationId.HexToByteArray());
        }
    }

    public class L1ToL2MessageReaderClassic
    {
        private TransactionReceipt? RetryableCreationReceipt;
        public BigInteger MessageNumber { get; }
        public string RetryableCreationId { get; }
        public string AutoRedeemId { get; }
        public string L2TxHash { get; }
        public Web3 L2Provider { get; }

        public L1ToL2MessageReaderClassic(Web3 l2Provider, int chainId, BigInteger messageNumber)
        {
            MessageNumber = messageNumber;
            L2Provider = l2Provider;
            RetryableCreationId = CalculateRetryableCreationId(chainId, messageNumber);
            AutoRedeemId = CalculateAutoRedeemId(RetryableCreationId);
            L2TxHash = CalculateL2TxHash(RetryableCreationId);
        }
        public static BigInteger BitFlip(BigInteger num)
        {
            return num | BigInteger.One << 255;
        }

        public static string CalculateRetryableCreationId(int chainId, BigInteger messageNumber)
        {
            return new Sha3Keccack().CalculateHash(
                Concat(
                    ZeroPad(BitConverter.GetBytes(chainId), 32),
                    ZeroPad(BitFlip(messageNumber).ToByteArray(), 32)
                )
            ).ToHex();
        }

        public static string CalculateAutoRedeemId(string retryableCreationId)
        {
            return new Sha3Keccack().CalculateHash(
                Concat(
                    ZeroPad(retryableCreationId.HexToByteArray(), 32),
                    ZeroPad(BigInteger.One.ToByteArray(), 32)
                )
            ).ToHex();
        }

        public static string CalculateL2TxHash(string retryableCreationId)
        {
            return new Sha3Keccack().CalculateHash(
                Concat(
                    ZeroPad(HexToBytes(retryableCreationId), 32),
                    ZeroPad(BigInteger.Zero.ToByteArray(), 32)
                )
            ).ToHex();
        }

        private string CalculateL2DerivedHash(string retryableCreationId)
        {
            return new Sha3Keccack().CalculateHash(
                Concat(
                    ZeroPad(HexToBytes(retryableCreationId), 32),
                    ZeroPad(BigInteger.One.ToByteArray(), 32)
                )
            ).ToHex();
        }

        public static byte[] HexToBytes(string value)
        {
            return value.HexToByteArray();
        }

        /**
         * Try to get the receipt for the retryable ticket creation.
         * This is the L2 transaction that creates the retryable ticket.
         * If confirmations or timeout is provided, this will wait for the ticket to be created
         * @returns Null if retryable has not been created
         */
        public async Task<TransactionReceipt> GetRetryableCreationReceipt(int? confirmations = null, int? timeout = null)
        {
            RetryableCreationReceipt ??= await Lib.GetTransactionReceiptAsync(
                                            L2Provider,
                                            RetryableCreationId,
                                            confirmations,
                                            timeout);

            return RetryableCreationReceipt;
        }

        public async Task<L1ToL2MessageStatus> Status()
        {
            var creationReceipt = await GetRetryableCreationReceipt();

            if (creationReceipt == null)
            {
                return L1ToL2MessageStatus.NOT_YET_CREATED;
            }

            if (creationReceipt.Status.Value == 0)
            {
                return L1ToL2MessageStatus.CREATION_FAILED;
            }

            var l2DerivedHash = CalculateL2DerivedHash(RetryableCreationId);
            var l2TxReceipt = await L2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2DerivedHash);

            if (l2TxReceipt != null && l2TxReceipt.Status.Value == 1)
            {
                return L1ToL2MessageStatus.REDEEMED;
            }

            return L1ToL2MessageStatus.EXPIRED;
        }
    }

    public class L1ToL2MessageWriter : L1ToL2MessageReader
    {
        private SignerOrProvider _l2Signer;
        public L1ToL2MessageWriter(
            SignerOrProvider l2Signer,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData) : base(l2Signer.Provider, chainId, sender, messageNumber, l1BaseFee, messageData)
        {
            if (l2Signer?.Provider == null)
            {
                throw new ArbSdkError("Signer not connected to provider.");
            }
            _l2Signer = l2Signer;
        }

        /**
         * Manually redeem the retryable ticket.
         * Throws if message status is not L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2
         */
        public async Task<RedeemTransaction> Redeem(Dictionary<string, object>? overrides = null)
        {
            var status = await Status();

            if (status == L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)
            {
                var funcParam = new RedeemFunction
                {
                    TicketId = RetryableCreationId.HexToByteArray()
                };

                if ((BigInteger)overrides["gasLimit"] != BigInteger.Zero) funcParam.Gas = (BigInteger)overrides["gasLimit"];

                var contractHandler = _l2Signer.Provider.Eth.GetContractHandler(Constants.ARB_RETRYABLE_TX_ADDRESS);
                var txReceipt = await contractHandler.SendRequestAndWaitForReceiptAsync(funcParam);

                return L2TransactionReceipt.ToRedeemTransaction(
                    L2TransactionReceipt.MonkeyPatchWait(txReceipt), _l2Provider);
            }
            else
            {
                throw new ArbSdkError($"Cannot redeem as retryable does not exist. Message status: {Enum.GetName(typeof(L1ToL2MessageStatus), status)} must be: {Enum.GetName(typeof(L1ToL2MessageStatus), L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)}.");
            }
        }

        /**
         * Cancel the retryable ticket.
         * Throws if message status is not L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2
         */
        public async Task<TransactionReceipt> Cancel(Dictionary<string, object>? overrides = null)
        {
            var status = await Status();

            if (status == L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)
            {
                var arbRetryableTxContract = await LoadContractUtils.LoadContract(
                    contractName: "ArbRetryableTx",
                    address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                    provider: _l2Signer.Provider,
                    isClassic: false
                );

                if (overrides == null)
                {
                    overrides = new Dictionary<string, object>();
                }

                if (!overrides.ContainsKey("from"))
                {
                    overrides["from"] = _l2Signer?.Account?.Address;
                }

                if (overrides.ContainsKey("gasLimit"))
                {
                    overrides["gas"] = overrides["gasLimit"];
                    if ((BigInteger)overrides["gas"] == 0)
                        overrides.Remove("gas");
                }

                var cancelFunction = arbRetryableTxContract.GetFunction("cancel");
                var txHash = await cancelFunction.SendTransactionAsync(RetryableCreationId, overrides);

                var txReceipt = await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

                return txReceipt;
            }
            else
            {
                throw new ArbSdkError($"Cannot cancel as retryable does not exist. Message status: {Enum.GetName(typeof(L1ToL2MessageStatus), status)} must be: {Enum.GetName(typeof(L1ToL2MessageStatus), L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)}.");
            }
        }

        /**
         * Increase the timeout of a retryable ticket.
         * Throws if message status is not L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2
         */
        public async Task<TransactionReceipt> KeepAlive(Dictionary<string, object>? overrides = null)
        {
            var status = await Status();

            if (status == L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)
            {
                var arbRetryableTxContract = await LoadContractUtils.LoadContract(
                    contractName: "ArbRetryableTx",
                    address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                    provider: _l2Signer.Provider,
                    isClassic: false
                );

                if (overrides == null)
                {
                    overrides = new Dictionary<string, object>();
                }

                if (!overrides.ContainsKey("from"))
                {
                    overrides["from"] = _l2Signer?.Account?.Address;
                }

                if (overrides.ContainsKey("gasLimit"))
                {
                    overrides["gas"] = overrides["gasLimit"];
                    if ((BigInteger)overrides["gas"] == 0)
                        overrides.Remove("gas");
                }

                var keepAliveFunction = arbRetryableTxContract.GetFunction("keepAlive");
                var keepAliveTx = await keepAliveFunction.SendTransactionAsync(RetryableCreationId, overrides);

                var txReceipt = await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(keepAliveTx);

                return txReceipt;
            }
            else
            {
                throw new ArbSdkError($"Cannot keep alive as retryable does not exist. Message status: {Enum.GetName(typeof(L1ToL2MessageStatus), status)} must be: {Enum.GetName(typeof(L1ToL2MessageStatus), L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)}.");
            }
        }

    }

    /**
     * A message for Eth deposits from L1 to L2
     */
    public class EthDepositMessage
    {

        public Web3 L2Provider { get; }
        public int L2ChainId { get; }
        public BigInteger MessageNumber { get; }
        public string FromAddress { get; }
        public string ToAddress { get; }
        public BigInteger Value { get; }
        public string L2DepositTxHash { get; }
        public TransactionReceipt? L2DepositTxReceipt { get; set; }


        public EthDepositMessage(Web3 l2Provider, int l2ChainId, BigInteger messageNumber, string fromAddress, string toAddress, BigInteger value)
        {
            L2Provider = l2Provider;
            L2ChainId = l2ChainId;
            MessageNumber = messageNumber;
            FromAddress = fromAddress;
            ToAddress = toAddress;
            Value = value;
            L2DepositTxHash = CalculateDepositTxId(l2ChainId, messageNumber, fromAddress, toAddress, value);
            L2DepositTxReceipt = null;
        }

        public static string CalculateDepositTxId(
            BigInteger l2ChainId,
            BigInteger messageNumber,
            string fromAddress,
            string toAddress,
            BigInteger value)
        {
            string fromAddr = AddressUtil.Current.ConvertToChecksumAddress(fromAddress);
            string toAddr = AddressUtil.Current.ConvertToChecksumAddress(toAddress);

            var fields = new List<byte[]>
            {
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(l2ChainId))),
                RLP.EncodeElement(ZeroPad(FormatNumber(messageNumber), 32)),
                RLP.EncodeElement(fromAddr.HexToByteArray()),
                RLP.EncodeElement(toAddr.HexToByteArray()),
                RLP.EncodeElement(TrimLeadingZeros(FormatNumber(value)))
            };

            var rlpEncodedFields = RLP.EncodeList(fields.ToArray());

            var rlpWithTypePrefix = new byte[] { 0x64 }.Concat(rlpEncodedFields).ToArray();
            var depositTxId = new Sha3Keccack().CalculateHash(rlpWithTypePrefix);

            return depositTxId.ToHex();
        }

        private static byte[] FormatNumber(BigInteger number)
        {
            return number.ToByteArray().Reverse().ToArray();
        }

        private static byte[] ZeroPad(byte[] input, int length)
        {
            if (input.Length >= length) return input;
            var padded = new byte[length];
            Array.Copy(input, 0, padded, length - input.Length, input.Length);
            return padded;
        }

        private static byte[] TrimLeadingZeros(byte[] input)
        {
            int i = 0;
            while (i < input.Length && input[i] == 0) i++;
            return input.Skip(i).ToArray();
        }

        /**
         * Parse the data field in
         * event InboxMessageDelivered(uint256 indexed messageNum, bytes data);
         * @param eventData
         * @returns destination and amount
         */
        public static (string to, BigInteger value) ParseEthDepositData(string eventData)
        {
            if (eventData.StartsWith("0x"))
            {
                eventData = eventData.Substring(2);
            }

            var addressEnd = 2 + 20 * 2;
            var toAddressHex = string.Concat("0x", eventData.AsSpan(0, addressEnd - 2));

            var toAddress = Web3.ToChecksumAddress(toAddressHex);

            var valueHex = eventData[addressEnd..];

            var value = BigInteger.Parse("0" + valueHex, System.Globalization.NumberStyles.HexNumber);

            return (toAddress, value);
        }

        /**
         * Create an EthDepositMessage from data emitted in event when calling ethDeposit on Inbox.sol
         * @param l2Provider
         * @param messageNumber The message number in the Inbox.InboxMessageDelivered event
         * @param senderAddr The sender address from Bridge.MessageDelivered event
         * @param inboxMessageEventData The data field from the Inbox.InboxMessageDelivered event
         * @returns
         */
        public static async Task<EthDepositMessage> FromEventComponents(
            Web3 _l2Provider,
            BigInteger messageNumber,
            string senderAddr,
            string inboxMessageEventData)
        {
            var chainId = (await _l2Provider.Eth.ChainId.SendRequestAsync()).Value;
            var (to, value) = ParseEthDepositData(inboxMessageEventData);

            return new EthDepositMessage(_l2Provider, ((int)chainId), messageNumber, senderAddr, to, value);
        }

        public async Task<EthDepositStatus> Status()
        {
            var receipt = await L2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(L2DepositTxHash);
            return receipt == null ? EthDepositStatus.PENDING : EthDepositStatus.DEPOSITED;
        }


        public async Task<TransactionReceipt> Wait(int? confirmations = null, int? timeout = null)
        {
            var l2Network = await GetL2Network(L2ChainId);

            var chosenTimeout = timeout ?? l2Network?.DepositTimeout;

            L2DepositTxReceipt ??= await Lib.GetTransactionReceiptAsync(
                    L2Provider,
                    L2DepositTxHash,
                    confirmations,
                    chosenTimeout
                );

            return L2DepositTxReceipt;
        }
    }
}
