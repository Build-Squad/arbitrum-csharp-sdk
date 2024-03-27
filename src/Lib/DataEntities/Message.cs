using System.Numerics;

namespace Arbitrum.DataEntities
{
    /* The components of a submit retryable message. 
     * Can be parsed from the events emitted from the Inbox.
     */
    public class RetryableMessageParams
    {
        /* Destination address for L2 message */
        public string DestAddress { get; set; }

        /* Call value in L2 message */
        public BigInteger L2CallValue { get; set; }

        /* Value sent at L1 */
        public BigInteger L1Value { get; set; }

        /* Max gas deducted from L2 balance to cover base submission fee */
        public BigInteger MaxSubmissionFee { get; set; }

        /* L2 address to credit (gaslimit x gasprice - execution cost) */
        public string ExcessFeeRefundAddress { get; set; }

        /* Address to credit l2Callvalue on L2 if retryable txn times out or gets cancelled */
        public string CallValueRefundAddress { get; set; }

        /* Max gas deducted from user's L2 balance to cover L2 execution */
        public BigInteger GasLimit { get; set; }

        /* Gas price for L2 execution */
        public BigInteger MaxFeePerGas { get; set; }

        /* Calldata for of the L2 message */
        public string Data { get; set; }
    }

    /* The inbox message kind as defined in:
     * https://github.com/OffchainLabs/nitro/blob/c7f3429e2456bf5ca296a49cec3bb437420bc2bb/contracts/src/libraries/MessageTypes.sol
     */
    public enum InboxMessageKind
    {
        L1MessageType_submitRetryableTx = 9,
        L1MessageType_ethDeposit = 12,
        L2MessageType_signedTx = 4,
    }

    /* L2ToL1 message status */
    public enum L2ToL1MessageStatus
    {
        UNCONFIRMED,
        CONFIRMED,
        EXECUTED,
    }
}
