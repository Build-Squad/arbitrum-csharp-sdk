using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RLP;
using Nethereum.Web3;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Signer;
using Arbitrum.DataEntities;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.JsonRpc.Client;

using Arbitrum.Utils;
using static Arbitrum.Message.L1ToL2MessageUtils;
using static Arbitrum.DataEntities.NetworkUtils;

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

        // Helper Methods
        public static byte[] IntToBytes(BigInteger value)
        {
            return value.ToByteArray();
        }

        public static byte[] HexToBytes(string value)
        {
            return value.HexToByteArray();
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

        public static byte[] FormatNumber(BigInteger value)
        {
            // Convert the BigInteger to a byte array
            byte[] bytes = value.ToByteArray();

            // Convert the byte array to a hexadecimal string
            string hexStr = bytes.ToHex(prefix: true);

            // Remove leading zeros if any
            hexStr = hexStr.TrimStart('0');

            // Ensure the length of the hexadecimal string is even
            if (hexStr.Length % 2 != 0)
            {
                hexStr = "0" + hexStr;
            }

            // Convert the hexadecimal string back to a byte array
            return hexStr.HexToByteArray();
        }

        public static byte[] Concat(params object[] args)
        {
            // Initialize an enumerable to store byte arrays
            IEnumerable<byte[]> objects;

            // Check if a single argument is provided and if it's a list or array of byte arrays
            if (args.Length == 1 && (args[0] is IList<byte> || args[0] is IList<byte[]>))
            {
                // If yes, convert it to an enumerable of byte arrays
                objects = ((IEnumerable<byte[]>)args[0]).Select(item => item.ToArray());
            }
            else
            {
                // If multiple arguments are provided or the single argument is not a list or array of byte arrays,
                // process each argument and convert it to a byte array
                objects = args.Select(arg =>
                {
                    // Convert the argument to a byte array based on its type
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

            // Calculate total length of the concatenated byte arrays
            var length = objects.Sum(item => item.Length);

            // Initialize the resulting byte array with the calculated length
            var result = new byte[length];

            // Copy each byte array to the resulting byte array
            var offset = 0;
            foreach (var obj in objects)
            {
                Array.Copy(obj, 0, result, offset, obj.Length);
                offset += obj.Length;
            }

            // Return the concatenated byte array
            return result;
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
        public RetryableMessageParams MessageData { get; }
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
                MessageData.DestAddress,
                MessageData.L2CallValue,
                MessageData.L1Value,
                MessageData.MaxSubmissionFee,
                MessageData.ExcessFeeRefundAddress,
                MessageData.CallValueRefundAddress,
                MessageData.GasLimit,
                MessageData.MaxFeePerGas,
                MessageData.Data);
        }

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
            var chainId = l2ChainId;
            var msgNum = messageNumber;
            var fromAddr = new AddressUtil().ConvertToChecksumAddress(fromAddress);
            var destAddr = destAddress == Constants.ADDRESS_ZERO ? "0x" : new AddressUtil().ConvertToChecksumAddress(destAddress);
            var callValueRefundAddr = new AddressUtil().ConvertToChecksumAddress(callValueRefundAddress);
            var excessFeeRefundAddr = new AddressUtil().ConvertToChecksumAddress(excessFeeRefundAddress);

            var fields = new byte[][]
            {
            L1ToL2MessageUtils.FormatNumber(chainId),
            L1ToL2MessageUtils.ZeroPad(L1ToL2MessageUtils.FormatNumber(msgNum), 32),
            fromAddr.HexToByteArray().ToHex(true).HexToByteArray(),
            L1ToL2MessageUtils.FormatNumber(l1BaseFee),
            L1ToL2MessageUtils.FormatNumber(l1Value),
            L1ToL2MessageUtils.FormatNumber(maxFeePerGas),
            L1ToL2MessageUtils.FormatNumber(gasLimit),
            destAddr != "0x" ? destAddr.HexToByteArray().ToHex(true).HexToByteArray() : new byte[0],
            L1ToL2MessageUtils.FormatNumber(l2CallValue),
            callValueRefundAddr.HexToByteArray().ToHex(true).HexToByteArray(),
            L1ToL2MessageUtils.FormatNumber(maxSubmissionFee),
            excessFeeRefundAddr.HexToByteArray().ToHex(true).HexToByteArray(),
            data.HexToByteArray().ToHex(true).HexToByteArray()
            };

            var rlpEncoded = RLP.EncodeList(fields);

            var rlpEncWithType = "69".HexToByteArray().Concat(rlpEncoded).ToArray();

            var retryableTxId = new Sha3Keccack().CalculateHash(rlpEncWithType);
            return retryableTxId.ToHex();
        }

        public static L1ToL2Message FromEventComponents(
            Web3 l2SignerOrProvider,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData)
        {
            if (SignerProviderUtils.IsSigner(l2SignerOrProvider))
            {
                return new L1ToL2MessageWriter(
                    l2SignerOrProvider,
                    chainId,
                    sender,
                    messageNumber,
                    l1BaseFee,
                    messageData);
            }
            else
            {
                return new L1ToL2MessageReader(
                    l2SignerOrProvider,
                    chainId,
                    sender,
                    messageNumber,
                    l1BaseFee,
                    messageData);
            }
        }
    }

    public class L1ToL2MessageWaitResult
    {
        public L1ToL2MessageStatus Status { get; set; }
        public TransactionReceipt? L2TxReceipt { get; set; }
    }

    public class L1ToL2MessageReader : L1ToL2Message
    {
        private Web3 _l2Provider;
        private TransactionReceipt _retryableCreationReceipt;

        public L1ToL2MessageReader(
            Web3 l2Provider,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData) : base(chainId, sender, messageNumber, l1BaseFee, messageData)
        {
            _l2Provider = l2Provider;
            _retryableCreationReceipt = null;
        }

        public async Task<TransactionReceipt?> GetRetryableCreationReceipt(int? confirmations = null, int? timeout = null)
        {
            if (_retryableCreationReceipt == null)
            {
                // Ensure you're accessing _l2Provider through an instance
                _retryableCreationReceipt = await Lib.GetTransactionReceiptAsync(_l2Provider, RetryableCreationId, confirmations, timeout);
            }

            return _retryableCreationReceipt;
        }

        public async Task<TransactionReceipt?> GetAutoRedeemAttempt()
        {
            var creationReceipt = await GetRetryableCreationReceipt();

            if (creationReceipt != null)
            {
                var l2Receipt = new L2TransactionReceipt(creationReceipt);
                var redeemEvents = l2Receipt.GetRedeemScheduledEvents();

                if (redeemEvents.Count == 1)
                {
                    return await _l2Provider.GetTransactionReceipt(redeemEvents[0].RetryTxHash);
                }
                else if (redeemEvents.Count > 1)
                {
                    throw new ArbSdkError($"Unexpected number of redeem events for retryable creation tx. {creationReceipt} {redeemEvents}");
                }
            }

            return null;
        }


        public async Task<L1ToL2MessageWaitResult> GetSuccessfulRedeem()
        {
            var l2Network = await GetL2NetworkAsync(this.l2Provider);
            var eventFetcher = new EventFetcher(this.l2Provider);
            var creationReceipt = await GetRetryableCreationReceipt();

            if (creationReceipt == null)
            {
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.NOT_YET_CREATED };
            }

            if (creationReceipt.Status == 0)
            {
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.CREATION_FAILED };
            }

            var autoRedeem = await GetAutoRedeemAttempt();
            if (autoRedeem != null && autoRedeem.Status == 1)
            {
                return new L1ToL2MessageWaitResult { L2TxReceipt = autoRedeem, Status = L1ToL2MessageStatus.REDEEMED };
            }

            if (await RetryableExists())
            {
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2 };
            }

            var maxBlock = await this.l2Provider.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var increment = 1000;
            var fromBlock = await this.l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(creationReceipt.BlockNumber.Value);

            var timeout = fromBlock.Timestamp + l2Network.RetryableLifetimeSeconds;
            var queriedRange = new List<(ulong from, ulong to)>();

            while (fromBlock.Number.Value < maxBlock.Value)
            {
                var toBlockNumber = Math.Min(fromBlock.Number.Value + increment, maxBlock.Value);
                var outerBlockRange = (from: fromBlock.Number.Value, to: toBlockNumber);
                queriedRange.Add(outerBlockRange);

                var redeemEvents = await eventFetcher.GetEvents(ArbRetryableTx__factory.Instance,
                                                                 contract => contract.RedeemScheduled(this.RetryableCreationId),
                                                                 new BlockParameter(outerBlockRange.from),
                                                                 new BlockParameter(outerBlockRange.to),
                                                                 ARB_RETRYABLE_TX_ADDRESS);

                var successfulRedeem = new List<TransactionReceipt>();

                foreach (var e in redeemEvents)
                {
                    var receipt = await this.l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(e.Event.RetryTxHash);
                    if (receipt != null && receipt.Status == 1)
                    {
                        successfulRedeem.Add(receipt);
                    }
                }

                if (successfulRedeem.Count > 1)
                {
                    throw new ArbSdkError($"Unexpected number of successful redeems. Expected only one redeem for ticket {this.RetryableCreationId}, but found {successfulRedeem.Count}.");
                }

                if (successfulRedeem.Count == 1)
                {
                    return new L1ToL2MessageWaitResult { L2TxReceipt = successfulRedeem.First(), Status = L1ToL2MessageStatus.REDEEMED };
                }

                var toBlock = await this.l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(toBlockNumber);
                if (toBlock.Timestamp > timeout)
                {
                    while (queriedRange.Count > 0)
                    {
                        var blockRange = queriedRange.First();
                        var keepaliveEvents = await eventFetcher.GetEvents(ArbRetryableTx__factory.Instance,
                                                                           contract => contract.LifetimeExtended(this.RetryableCreationId),
                                                                           new BlockParameter(blockRange.from),
                                                                           new BlockParameter(blockRange.to),
                                                                           ARB_RETRYABLE_TX_ADDRESS);

                        if (keepaliveEvents.Any())
                        {
                            timeout = keepaliveEvents.Max(e => e.Event.NewTimeout);
                            break;
                        }
                        queriedRange.RemoveAt(0);
                    }

                    if (toBlock.Timestamp > timeout)
                    {
                        break;
                    }

                    while (queriedRange.Count > 1)
                    {
                        queriedRange.RemoveAt(0);
                    }
                }

                var processedSeconds = (int)(toBlock.Timestamp - fromBlock.Timestamp);
                if (processedSeconds != 0)
                {
                    increment = (int)Math.Ceiling((increment * 86400) / processedSeconds);
                }

                fromBlock = toBlock;
            }

            return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.EXPIRED };
        }

        public async Task<bool> IsExpired()
        {
            // Implement your logic here to check if the message is expired
            return false;
        }

        public async Task<object> Status()
        {
            // Implement your logic here to get the status of the message
            return null;
        }

        public async Task<object> WaitForStatus(int? confirmations = null, TimeSpan? timeout = null)
        {
            // Implement your logic here to wait for the status
            return null;
        }

        public static async Task<object> GetLifetime(object l2Provider)
        {
            // Implement your logic here to get the lifetime
            return null;
        }

        public async Task<object> GetTimeout()
        {
            // Implement your logic here to get the timeout
            return null;
        }

        public async Task<object> GetBeneficiary()
        {
            // Implement your logic here to get the beneficiary
            return null;
        }
    }

    public class L1ToL2MessageWriter : L1ToL2MessageReader
    {
        public readonly SignerOrProvider l2Signer;

        public L1ToL2MessageWriter(
            Web3 l2Signer,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData) : base(l2Signer, chainId, sender, messageNumber, l1BaseFee, messageData)
        {
            if (l2Signer.Provider == null)
            {
                throw new ArbSdkError("Signer not connected to provider.");
            }
            this.l2Signer = l2Signer;
        }

        public async Task<object> Redeem(object overrides = null)
        {
            // Implement your logic here to redeem
            return null;
        }

        public async Task<object> Cancel(object overrides = null)
        {
            // Implement your logic here to cancel
            return null;
        }

        public async Task<object> KeepAlive(object overrides = null)
        {
            // Implement your logic here to keep alive
            return null;
        }
    }

    public class L1ToL2MessageReaderOrWriter<T> where T : SignerOrProvider
    {
        public static Type GetReaderOrWriterType()
        {
            if (typeof(T) == typeof(Web3))
            {
                return typeof(L1ToL2MessageReader);
            }
            else
            {
                return typeof(L1ToL2MessageWriter);
            }
        }
    }

    public class EthDepositMessage
    {
        private readonly RpcClient _l2Provider;
        public readonly string L2DepositTxHash;
        private TransactionReceipt _l2DepositTxReceipt;

        public static string CalculateDepositTxId(
            int l2ChainId,
            BigInteger messageNumber,
            string fromAddress,
            string toAddress,
            BigInteger value)
        {
            var chainIdBytes = l2ChainId.ToBytesForRLPEncoding();
            var messageNumberBytes = messageNumber.ToBytesForRLPEncoding();
            var fromAddressBytes = fromAddress.HexToByteArray();
            var toAddressBytes = toAddress.HexToByteArray();
            var valueBytes = value.ToBytesForRLPEncoding();

            var encodedFields = new byte[][] { chainIdBytes, messageNumberBytes, fromAddressBytes, toAddressBytes, valueBytes };

            var rlpEncoded = RLP.EncodeList(encodedFields);
            var txType = new byte[] { 0x64 }; // arbitrum eth deposit transactions have type 0x64
            var rlpEnc = RLP.EncodeList(txType, rlpEncoded);

            return new Sha3Keccack().CalculateHash(rlpEnc).ToHex();
        }

        public static (string to, BigInteger value) ParseEthDepositData(string eventData)
        {
            var to = eventData.Substring(0, 42); // Extract destination address
            var valueHex = eventData.Substring(42); // Extract value in hex
            var value = BigInteger.Parse("0x" + valueHex); // Convert hex value to BigInteger

            return (to, value);
        }

        public static async Task<EthDepositMessage> FromEventComponents(
            SignerOrProvider l2Provider,
            BigInteger messageNumber,
            string senderAddr,
            string inboxMessageEventData)
        {
            var chainId = (await l2Provider.Eth.ChainId.SendRequestAsync()).Value;
            var (to, value) = ParseEthDepositData(inboxMessageEventData);

            return new EthDepositMessage(_l2Provider, chainId, messageNumber, senderAddr, to, value);
        }

        public EthDepositMessage(
            RpcClient l2Provider,
            int l2ChainId,
            BigInteger messageNumber,
            string from,
            string to,
            BigInteger value)
        {
            L2DepositTxHash = CalculateDepositTxId(l2ChainId, messageNumber, from, to, value);
        }

        public async Task<L1ToL2MessageUtils.EthDepositStatus> Status()
        {
            var receipt = await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(L2DepositTxHash);
            return receipt == null ? L1ToL2MessageUtils.EthDepositStatus.PENDING : L1ToL2MessageUtils.EthDepositStatus.DEPOSITED;
        }

        public async Task<TransactionReceipt> Wait(int? confirmations = null, int? timeout = null)
        {
            if (_l2DepositTxReceipt == null)
            {
                _l2DepositTxReceipt = await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(L2DepositTxHash);
            }
            return _l2DepositTxReceipt;
        }
    }

}
