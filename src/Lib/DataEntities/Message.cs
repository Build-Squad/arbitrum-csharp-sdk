using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Arbitrum.DataEntities
{
    [FunctionOutput]
    public class RetryableMessageParams : FunctionOutputDTO
    {
        [Parameter("address", 1)]
        public string? DestAddress { get; set; } // Destination address for L2 message

        [Parameter("uint256", 2)]
        public BigInteger L2CallValue { get; set; } // Call value in L2 message

        [Parameter("uint256", 3)]
        public BigInteger L1Value { get; set; } // Value sent at L1

        [Parameter("uint256", 4)]
        public BigInteger MaxSubmissionFee { get; set; } // Max gas deducted from L2 balance to cover base submission fee

        [Parameter("address", 5)]
        public string ExcessFeeRefundAddress { get; set; } // L2 address to credit (gaslimit x gasprice - execution cost)

        [Parameter("address", 6)]
        public string CallValueRefundAddress { get; set; } // Address to credit l2Callvalue on L2 if retryable txn times out or gets cancelled

        [Parameter("uint256", 7)]
        public BigInteger GasLimit { get; set; } // Max gas deducted from user's L2 balance to cover L2 execution

        [Parameter("uint256", 8)]
        public BigInteger MaxFeePerGas { get; set; } // Gas price for L2 execution

        [Parameter("bytes", 9)]
        public string Data { get; set; } // Calldata for of the L2 message
    }

    [FunctionOutput]
    public class RetryableMessageParamsTest : FunctionOutputDTO
    {
        [Parameter("address", 1)]
        public string? destAddr { get; set; }

        [Parameter("uint256", 2)]
        public BigInteger l2CallValue { get; set; }
    }

    public enum InboxMessageKind
    {
        L1MessageType_submitRetryableTx = 9,
        L1MessageType_ethDeposit = 12,
        L2MessageType_signedTx = 4
    }

    public enum L2ToL1MessageStatus
    {
        UNCONFIRMED,
        CONFIRMED,
        EXECUTED
    }
}
