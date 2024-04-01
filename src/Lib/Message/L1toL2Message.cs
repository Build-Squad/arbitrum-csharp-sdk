using System;
using System.Numerics;
using System.Threading.Tasks;
using Serilog;

using Nethereum.RLP;
using Nethereum.Web3;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Signer;
using Arbitrum.DataEntities;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.JsonRpc.Client;

using Arbitrum;
using Arbitrum.Utils;
using static Arbitrum.Message.L1ToL2MessageUtils;
using static Arbitrum.DataEntities.NetworkUtils;
using Nethereum.RPC.Eth.Services;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Org.BouncyCastle.Utilities.Encoders;
using Arbitrum.Message;

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
        public class IRetryableExistsError : Exception
        {
            public int ErrorCode { get; set; }
            public string? ErrorName { get; set; }
        }

        // Helper Methods
        public static byte[] IntToBytes(BigInteger value)
        {
            return value.ToByteArray();
        }

        //from hexadecimal string to Byte array
        public static byte[] HexToBytes(string value)
        {
            return value.HexToByteArray();
        }

        public static int ByteArrayToInt(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length % 2 != 0)
            {
                return null;
            }

            return (data.Length / 2);
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

    /**
     * Conditional type for Signer or Provider. If T is of type Provider
     * then L1ToL2MessageReaderOrWriter<T> will be of type L1ToL2MessageReader.
     * If T is of type Signer then L1ToL2MessageReaderOrWriter<T> will be of
     * type L1ToL2MessageWriter.
     */
    //public class L1ToL2MessageReaderOrWriter<T> where T : SignerOrProvider
    //{
    //    // Define a private constructor to prevent instantiation of this class
    //    private L1ToL2MessageReaderOrWriter() { }

    //    // Define static methods to mimic conditional behavior
    //    public static L1ToL2MessageReader FromProvider(Web3 provider, BigInteger chainId, string sender, BigInteger messageNumber, BigInteger l1BaseFee, RetryableMessageParams messageData)
    //    {
    //        return new L1ToL2MessageReader(provider, chainId, sender, messageNumber, l1BaseFee, messageData);
    //    }

    //    public static L1ToL2MessageWriter FromSigner(Web3 signer, BigInteger chainId, string sender, BigInteger messageNumber, BigInteger l1BaseFee, RetryableMessageParams messageData)
    //    {
    //        return new L1ToL2MessageWriter(signer, chainId, sender, messageNumber, l1BaseFee, messageData);
    //    }
    //}

        //public static Type GetReaderOrWriterType()
        //{
        //    if (typeof(T) == typeof(Web3))
        //    {
        //        return typeof(L1ToL2MessageReader);
        //    }
        //    else
        //    {
        //        return typeof(L1ToL2MessageWriter);
        //    }
        //}
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
            var chainId = l2ChainId;
            var msgNum = messageNumber;
            var fromAddr = new AddressUtil().ConvertToChecksumAddress(fromAddress);
            var destAddr = destAddress == Constants.ADDRESS_ZERO ? "0x" : new AddressUtil().ConvertToChecksumAddress(destAddress);
            var callValueRefundAddr = new AddressUtil().ConvertToChecksumAddress(callValueRefundAddress);
            var excessFeeRefundAddr = new AddressUtil().ConvertToChecksumAddress(excessFeeRefundAddress);

            var fields = new byte[][]
            {
            FormatNumber(chainId),
            ZeroPad(FormatNumber(msgNum), 32),
            fromAddr.HexToByteArray().ToHex(true).HexToByteArray(),
            FormatNumber(l1BaseFee),
            FormatNumber(l1Value),
            FormatNumber(maxFeePerGas),
            FormatNumber(gasLimit),
            destAddr != "0x" ? destAddr.HexToByteArray().ToHex(true).HexToByteArray() : new byte[0],
            FormatNumber(l2CallValue),
            callValueRefundAddr.HexToByteArray().ToHex(true).HexToByteArray(),
            FormatNumber(maxSubmissionFee),
            excessFeeRefundAddr.HexToByteArray().ToHex(true).HexToByteArray(),
            data.HexToByteArray().ToHex(true).HexToByteArray()
            };

            var rlpEncoded = RLP.EncodeList(fields);

            var rlpEncWithType = "69".HexToByteArray().Concat(rlpEncoded).ToArray();

            var retryableTxId = new Sha3Keccack().CalculateHash(rlpEncWithType);
            return retryableTxId.ToHex();
        }

        public static L1ToL2Message FromEventComponents<T>(
            T l2SignerOrProvider,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData) where T : SignerOrProvider
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
        public EthDepositMessage Message { get; set; }
        public TransactionReceipt L2TxReceipt { get; set; }
    }
    public class L1ContractCallTransactionReceiptResults
    {
        public bool Complete { get; set; }
        public L1ToL2Message Message { get; set; }
        public L1ToL2MessageWaitResult Result { get; set; }
    }

    public class EthDepositMessageWaitResult : TransactionReceipt
    {
        public TransactionReceipt L2TxReceipt { get; set; }
    }

    public class L1ToL2MessageReader : L1ToL2Message
    {
        public SignerOrProvider _l2Provider;
        public TransactionReceipt? _retryableCreationReceipt;

        public TransactionReceipt? RetryableCreationReceipt
        {
            get { return _retryableCreationReceipt; }
            set { _retryableCreationReceipt = value; }
        }
        public L1ToL2MessageReader(
            SignerOrProvider l2Provider,
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
            if (_retryableCreationReceipt == null)
            {
                // Ensure you're accessing _l2Provider through an instance
                _retryableCreationReceipt = await Lib.GetTransactionReceiptAsync(_l2Provider, RetryableCreationId, confirmations, timeout);
            }

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
                    return await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(redeemEvents[0].RetryTxHash);   ///////
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
            var l2Network = await GetL2NetworkAsync(_l2Provider);
            var eventFetcher = new EventFetcher(_l2Provider);
            var creationReceipt = await GetRetryableCreationReceipt();

            if (creationReceipt == null)
            {
                // retryable was never created, or not created yet
                // therefore it cant have been redeemed or be expired
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.NOT_YET_CREATED };
            }

            if (creationReceipt.Status.Value == 0)
            {
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.CREATION_FAILED };
            }

            // check the auto redeem first to avoid doing costly log queries in the happy case
            var autoRedeem = await GetAutoRedeemAttempt();
            if (autoRedeem != null && autoRedeem.Status.Value == 1)
            {
                return new L1ToL2MessageWaitResult { L2TxReceipt = autoRedeem, Status = L1ToL2MessageStatus.REDEEMED };
            }

            if (await RetryableExists())
            {
                // the retryable was created and still exists
                // therefore it cant have been redeemed or be expired
                return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2 };
            }

            // from this point on we know that the retryable was created but does not exist,
            // so the retryable was either successfully redeemed, or it expired

            // the auto redeem didnt exist or wasnt successful, look for a later manual redeem
            // to do this we need to filter through the whole lifetime of the ticket looking
            // for relevant redeem scheduled events
            // **below there are several explicit castings to int**
            int increment = 1000;
            BlockWithTransactions fromBlock = await _l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new BlockParameter(creationReceipt.BlockNumber));

            int timeout = (int)fromBlock.Timestamp.Value + l2Network.RetryableLifetimeSeconds;

            List<(int from, int to)> queriedRange = new List<(int from, int to)>();

            var maxBlock = await _l2Provider.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            while ((int)fromBlock.Number.Value < (int)maxBlock.Value)
            {
                int toBlockNumber = Math.Min((int)fromBlock.Number.Value + increment, (int)maxBlock.Value);

                // using fromBlock.number would lead to 1 block overlap
                // not fixing it here to keep the code simple
                var outerBlockRange = ((int)fromBlock.Number.Value, toBlockNumber); 

                queriedRange.Add(outerBlockRange);     

                List<FetchedEvent> redeemEvents = await eventFetcher.GetEventsAsync(
                                                            contractFactory: "ArbRetryableTx",
                                                            eventName: "RedeemScheduled",
                                                            argumentFilters: new Dictionary<string, object> { { "ticketId", this.RetryableCreationId } },
                                                            filter: new Dictionary<string, object>
                                                                    {
                                                                        { "fromBlock", outerBlockRange.Item1 },    ////////
                                                                        { "toBlock", outerBlockRange.Item2 },
                                                                        { "address", Constants.ARB_RETRYABLE_TX_ADDRESS }
                                                                    },
                                                            isClassic: false
                                                            );  

                var successfulRedeem = new List<TransactionReceipt>();

                foreach (var e in redeemEvents)
                {
                    var receipt = await _l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync("retryTxHash");   //////
                    if (receipt != null && receipt.Status.Value == 1)
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

                var toBlock = await _l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(toBlockNumber.ToHexBigInteger());
                if (toBlock.Timestamp.Value > timeout)
                {
                    // Check for LifetimeExtended event
                    while (queriedRange.Count > 0)
                    {
                        var blockRange = queriedRange.First();
                        List<FetchedEvent> keepAliveEvents = await eventFetcher.GetEventsAsync(   
                                                                           contractFactory: "ArbRetryableTx",
                                                                           eventName: "LifetimeExtended",
                                                                           argumentFilters: new Dictionary<string, object> { { "ticketId", this.RetryableCreationId } },
                                                                           filter: new Dictionary<string, object>
                                                                            {
                                                                                { "fromBlock", blockRange.from },    ////////
                                                                                { "toBlock", blockRange.to },
                                                                                { "address", Constants.ARB_RETRYABLE_TX_ADDRESS }
                                                                            },
                                                                           isClassic: false);
                        if (keepAliveEvents.Count > 0)
                        {
                            var maxTimeout = keepAliveEvents.Select(e => e.Event["newTimeout"]).OrderByDescending(t => t).First();
                            break;
                        }
                        queriedRange.RemoveAt(0);
                    }

                    // the retryable no longer exists, but we've searched beyond the timeout
                    // so it must have expired
                    if (toBlock.Timestamp.Value > timeout)
                    {
                        break;
                    }

                    // It is possible to have another keepalive in the last range as it might include block after previous timeout
                    while (queriedRange.Count > 1)
                    {
                        queriedRange.RemoveAt(0);
                    }
                }

                int processedSeconds = (int)(toBlock.Timestamp.Value - fromBlock.Timestamp.Value);
                if (processedSeconds != 0)
                {
                    // find the increment that cover ~ 1 day
                    //explicitly casting the result of the division operation to either decimal to make the compiler choose the appropriate overload.
                    increment = (int)Math.Ceiling((decimal)(increment * 86400) / processedSeconds);
                }

                fromBlock = toBlock;
            }

            // we know from earlier that the retryable no longer exists, so if we havent found the redemption
            // we know that it must have expired
            return new L1ToL2MessageWaitResult { Status = L1ToL2MessageStatus.EXPIRED };
        }

        /**
         * Has this message expired. Once expired the retryable ticket can no longer be redeemed.
         * @deprecated Will be removed in v3.0.0
         * @returns
         */
        public async Task<bool> IsExpired()
        {
            return await this.RetryableExists();
        }

        public async Task<bool> RetryableExists()
        {
            var currentTimestamp = (await _l2Provider.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest())).Timestamp;

            try
            {
                BigInteger timeoutTimestamp = await GetTimeout();
                // timeoutTimestamp returns the timestamp at which the retryable ticket expires
                // it can also return revert if the ticket l2Tx does not exist

                return currentTimestamp <= timeoutTimestamp;
            }
            catch (SmartContractRevertException ex)
            {
                if (ex.EncodedData == "0x80698456")   ///////
                {
                    Log.Information("Retryable does not exist.");
                    return false;
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, "An error occurred while checking retryable existence.");
                throw;
            }
            return true;
        }

        public async Task<L1ToL2MessageStatus> Status()
        {
            return (await this.GetSuccessfulRedeem()).Status;
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
            var l2network = await GetL2NetworkAsync(this.ChainId);

            var chosenTimeout = timeout.HasValue ? timeout : l2network.DepositTimeout;

            // try to wait for the retryable ticket to be created
            var _retryableCreationReceipt = await this.GetRetryableCreationReceipt(
                confirmations,
                chosenTimeout
                );
            
            if(_retryableCreationReceipt != null)
            {
                if(confirmations!=null || chosenTimeout!=null)
                {
                    throw new ArbSdkError(
                        "Retryable creation script not found ${this."
                        );
                }
            }
            return await this.GetSuccessfulRedeem();
        }

        /**
         * The minimium lifetime of a retryable tx
         * @returns
         */
        public static async Task<BigInteger> GetLifetime(object l2Provider)
        {
            Contract arbRetryableTxContract = LoadContractUtils.LoadContract(
                                                contractName: "ArbRetryableTx",
                                                address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                                                provider: l2Provider,
                                                isClassic: false
                                                );

            var getLifetimeFunction = arbRetryableTxContract.GetFunction("getLifetime");   ////////

            return await getLifetimeFunction.CallAsync<BigInteger>();
        }

        /**
         * Timestamp at which this message expires
         * @returns
         */
        public async Task<BigInteger> GetTimeout()
        {
            Contract arbRetryableTxContract = LoadContractUtils.LoadContract(
                contractName: "ArbRetryableTx",
                address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                provider: _l2Provider,
                isClassic: false
            );

            var getTimeoutFunction = arbRetryableTxContract.GetFunction("getTimeout");
            return await getTimeoutFunction.CallAsync<BigInteger>(RetryableCreationId);
        }

        /**
         * Address to which CallValue will be credited to on L2 if the retryable ticket times out or is cancelled.
         * The Beneficiary is also the address with the right to cancel a Retryable Ticket (if the ticket hasn’t been redeemed yet).
         * @returns
         */
        public async Task<string> GetBeneficiary()
        {
            Contract arbRetryableTxContract = LoadContractUtils.LoadContract(
                contractName: "ArbRetryableTx",
                address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                provider: _l2Provider,
                isClassic: false
            );

            var getBeneficiaryFunction = arbRetryableTxContract.GetFunction("getBeneficiary");   //////
            return await getBeneficiaryFunction.CallAsync<string>(RetryableCreationId);
        }
    }

    public class L1ToL2MessageReaderClassic
    {
        private TransactionReceipt retryableCreationReceipt;
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
                    ZeroPad(HexToBytes(retryableCreationId), 32),
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
                    // BN 0 meaning L2 TX
                    ZeroPad(BigInteger.One.ToByteArray(), 32)
                )
            ).ToHex();
        }

        /**
         * Try to get the receipt for the retryable ticket creation.
         * This is the L2 transaction that creates the retryable ticket.
         * If confirmations or timeout is provided, this will wait for the ticket to be created
         * @returns Null if retryable has not been created
         */
        public async Task<TransactionReceipt> GetRetryableCreationReceipt(int? confirmations = null, int? timeout = null)
        {
            if (retryableCreationReceipt == null)
            {
                retryableCreationReceipt = await Lib.GetTransactionReceiptAsync(
                    L2Provider,
                    RetryableCreationId,
                    confirmations,
                    timeout
                );
            }

            return retryableCreationReceipt;
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
        public readonly SignerOrProvider _l2Signer;
        public L1ToL2MessageWriter(
            SignerOrProvider l2provider,
            BigInteger chainId,
            string sender,
            BigInteger messageNumber,
            BigInteger l1BaseFee,
            RetryableMessageParams messageData) : base(l2provider, chainId, sender, messageNumber, l1BaseFee, messageData)
        {
            if (l2provider?.Provider == null)
            {
                throw new ArbSdkError("Signer not connected to provider.");
            }
        }

        /**
         * Manually redeem the retryable ticket.
         * Throws if message status is not L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2
         */
        public async Task<RedeemTransaction> Redeem(Dictionary<string, object>? overrides = null)
        {
            var status = await Status();

            if(status == L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)
            {
                Contract arbRetryableTxContract = LoadContractUtils.LoadContract(
                                                        contractName: "ArbRetryableTx",
                                                        address: Constants.ARB_RETRYABLE_TX_ADDRESS,
                                                        provider: _l2Signer.Provider,
                                                        isClassic: false
                                                    );
                if(overrides == null)
                {
                    overrides = new Dictionary<string, object>();
                }
                if (!overrides.ContainsKey("from"))
                {
                    overrides["from"] = _l2Signer.Account.Address;
                }
                if(overrides.ContainsKey("gasLimit"))
                {
                    overrides["gas"] = overrides["gasLimit"];
                    if ((BigInteger)overrides["gas"] == 0)
                        overrides.Remove("gas");
                }

                var redeemFunction = arbRetryableTxContract.GetFunction("Redeem");
                var redeemHash = await redeemFunction.SendTransactionAsync(RetryableCreationId, overrides);

                var txReceipt = await _l2Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(redeemHash);

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
                var arbRetryableTxContract = LoadContractUtils.LoadContract(
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
                    overrides["from"] = _l2Signer.Account.Address;
                }

                if (overrides.ContainsKey("gasLimit"))
                {
                    overrides["gas"] = overrides["gasLimit"];
                    if ((BigInteger)overrides["gas"] == 0)
                        overrides.Remove("gas");
                }

                var cancelFunction = arbRetryableTxContract.GetFunction("Cancel");
                var txHash = await cancelFunction.SendTransactionAsync(RetryableCreationId, overrides);

                var txReceipt = await _l2Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

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
                var arbRetryableTxContract = LoadContractUtils.LoadContract(
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
                    overrides["from"] = _l2Signer.Account.Address;
                }

                if (overrides.ContainsKey("gasLimit"))
                {
                    overrides["gas"] = overrides["gasLimit"];
                    if ((BigInteger)overrides["gas"] == 0)
                        overrides.Remove("gas");
                }

                var keepAliveFunction = arbRetryableTxContract.GetFunction("KeepAlive");
                var keepAliveTx = await keepAliveFunction.SendTransactionAsync(RetryableCreationId, overrides);

                var txReceipt = await _l2Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(keepAliveTx);

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
            int l2ChainId,
            BigInteger messageNumber,
            string fromAddress,
            string toAddress,
            BigInteger value)
        {
            BigInteger chainId =  new BigInteger(l2ChainId);
            BigInteger msgNum = messageNumber;

            byte[][] fields = new byte[][]
            {
                FormatNumber(chainId),
                ZeroPad(FormatNumber(msgNum), 32),
                HexToBytes(LoadContractUtils.GetAddress(fromAddress)),
                HexToBytes(LoadContractUtils.GetAddress(toAddress)),
                FormatNumber(value)
            };

            // Encode fields using RLP
            byte[] rlpEncoded = RLP.EncodeList(fields);

            // Concatenate the RLP encoded data with prefix 0x64
            byte[] rlpEncWithType = ByteUtil.Merge(new byte[] { 0x64 }, rlpEncoded);

            // Calculate the hash
            byte[] hashBytes = new Sha3Keccack().CalculateHash(rlpEncWithType);

            // Convert the hash bytes to hexadecimal string
            return hashBytes.ToHex();
        }

        /**
         * Parse the data field in
         * event InboxMessageDelivered(uint256 indexed messageNum, bytes data);
         * @param eventData
         * @returns destination and amount
         */
        public static (string to, BigInteger value) ParseEthDepositData(string eventData)
        {
            var to = eventData.Substring(0, 42); // Extract destination address
            var valueHex = eventData.Substring(42); // Extract value in hex
            var value = BigInteger.Parse("0x" + valueHex); // Convert hex value to BigInteger

            return (to, value);
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
            var l2Network = await GetL2NetworkAsync(this.L2ChainId);

            var chosenTimeout = timeout ?? l2Network.DepositTimeout;

            if (L2DepositTxReceipt == null)
            {
                L2DepositTxReceipt = await Lib.GetTransactionReceiptAsync(
                    L2Provider,
                    L2DepositTxHash,
                    confirmations,
                    chosenTimeout
                );
            }

            return L2DepositTxReceipt;
        }
    }

}
