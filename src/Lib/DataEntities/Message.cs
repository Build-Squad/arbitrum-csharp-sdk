using System.Numerics;

namespace Arbitrum.DataEntities
{
    /* The components of a submit retryable message. 
     * Can be parsed from the events emitted from the Inbox.
     */
    public interface RetryableMessageParams
    {
        /* Destination address for L2 message */
        string DestAddress { get; }

        /* Call value in L2 message */
        BigInteger L2CallValue { get; }

        /* Value sent at L1 */
        BigInteger L1Value { get; }

        /* Max gas deducted from L2 balance to cover base submission fee */
        BigInteger MaxSubmissionFee { get; }

        /* L2 address to credit (gaslimit x gasprice - execution cost) */
        string ExcessFeeRefundAddress { get; }

        /* Address to credit l2Callvalue on L2 if retryable txn times out or gets cancelled */
        string CallValueRefundAddress { get; }

        /* Max gas deducted from user's L2 balance to cover L2 execution */
        BigInteger GasLimit { get; }

        /* Gas price for L2 execution */
        BigInteger MaxFeePerGas { get; }

        /* Calldata for of the L2 message */
        string Data { get; }
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
