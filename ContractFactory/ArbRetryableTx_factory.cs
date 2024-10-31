using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory
{
    public partial class ArbRetryableTxDeployment : ArbRetryableTxDeploymentBase
    {
        public ArbRetryableTxDeployment() : base() { }
        public ArbRetryableTxDeployment(string byteCode) : base(byteCode) { }
    }

    public class ArbRetryableTxDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ArbRetryableTxDeploymentBase() : base(BYTECODE) { }
        public ArbRetryableTxDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CancelFunction : CancelFunctionBase { }

    [Function("cancel")]
    public class CancelFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "ticketId", 1)]
        public virtual byte[] TicketId { get; set; }
    }

    public partial class GetBeneficiaryFunction : GetBeneficiaryFunctionBase { }

    [Function("getBeneficiary", "address")]
    public class GetBeneficiaryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "ticketId", 1)]
        public virtual byte[] TicketId { get; set; }
    }

    public partial class GetCurrentRedeemerFunction : GetCurrentRedeemerFunctionBase { }

    [Function("getCurrentRedeemer", "address")]
    public class GetCurrentRedeemerFunctionBase : FunctionMessage
    {

    }

    public partial class GetLifetimeFunction : GetLifetimeFunctionBase { }

    [Function("getLifetime", "uint256")]
    public class GetLifetimeFunctionBase : FunctionMessage
    {

    }

    public partial class GetTimeoutFunction : GetTimeoutFunctionBase { }

    [Function("getTimeout", "uint256")]
    public class GetTimeoutFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "ticketId", 1)]
        public virtual byte[] TicketId { get; set; }
    }

    public partial class KeepaliveFunction : KeepaliveFunctionBase { }

    [Function("keepalive", "uint256")]
    public class KeepaliveFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "ticketId", 1)]
        public virtual byte[] TicketId { get; set; }
    }

    public partial class RedeemFunction : RedeemFunctionBase { }

    [Function("redeem", "bytes32")]
    public class RedeemFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "ticketId", 1)]
        public virtual byte[] TicketId { get; set; }
    }

    public partial class SubmitRetryableFunction : SubmitRetryableFunctionBase { }

    [Function("submitRetryable")]
    public class SubmitRetryableFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "requestId", 1)]
        public virtual byte[] RequestId { get; set; }
        [Parameter("uint256", "l1BaseFee", 2)]
        public virtual BigInteger L1BaseFee { get; set; }
        [Parameter("uint256", "deposit", 3)]
        public virtual BigInteger Deposit { get; set; }
        [Parameter("uint256", "callvalue", 4)]
        public virtual BigInteger Callvalue { get; set; }
        [Parameter("uint256", "gasFeeCap", 5)]
        public virtual BigInteger GasFeeCap { get; set; }
        [Parameter("uint64", "gasLimit", 6)]
        public virtual ulong GasLimit { get; set; }
        [Parameter("uint256", "maxSubmissionFee", 7)]
        public virtual BigInteger MaxSubmissionFee { get; set; }
        [Parameter("address", "feeRefundAddress", 8)]
        public virtual string FeeRefundAddress { get; set; }
        [Parameter("address", "beneficiary", 9)]
        public virtual string Beneficiary { get; set; }
        [Parameter("address", "retryTo", 10)]
        public virtual string RetryTo { get; set; }
        [Parameter("bytes", "retryData", 11)]
        public virtual byte[] RetryData { get; set; }
    }

    public partial class CanceledEventDTO : CanceledEventDTOBase { }

    [Event("Canceled")]
    public class CanceledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "ticketId", 1, true)]
        public virtual byte[] TicketId { get; set; }
    }

    public partial class LifetimeExtendedEventDTO : LifetimeExtendedEventDTOBase { }

    [Event("LifetimeExtended")]
    public class LifetimeExtendedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "ticketId", 1, true)]
        public virtual byte[] TicketId { get; set; }
        [Parameter("uint256", "newTimeout", 2, false)]
        public virtual BigInteger NewTimeout { get; set; }
    }

    public partial class RedeemScheduledEventDTO : RedeemScheduledEventDTOBase { }

    [Event("RedeemScheduled")]
    public class RedeemScheduledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "ticketId", 1, true)]
        public virtual byte[] TicketId { get; set; }
        [Parameter("bytes32", "retryTxHash", 2, true)]
        public virtual byte[] RetryTxHash { get; set; }
        [Parameter("uint64", "sequenceNum", 3, true)]
        public virtual ulong SequenceNum { get; set; }
        [Parameter("uint64", "donatedGas", 4, false)]
        public virtual ulong DonatedGas { get; set; }
        [Parameter("address", "gasDonor", 5, false)]
        public virtual string GasDonor { get; set; }
        [Parameter("uint256", "maxRefund", 6, false)]
        public virtual BigInteger MaxRefund { get; set; }
        [Parameter("uint256", "submissionFeeRefund", 7, false)]
        public virtual BigInteger SubmissionFeeRefund { get; set; }
    }

    public partial class RedeemedEventDTO : RedeemedEventDTOBase { }

    [Event("Redeemed")]
    public class RedeemedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "userTxHash", 1, true)]
        public virtual byte[] UserTxHash { get; set; }
    }

    public partial class TicketCreatedEventDTO : TicketCreatedEventDTOBase { }

    [Event("TicketCreated")]
    public class TicketCreatedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "ticketId", 1, true)]
        public virtual byte[] TicketId { get; set; }
    }



    public partial class GetBeneficiaryOutputDTO : GetBeneficiaryOutputDTOBase { }

    [FunctionOutput]
    public class GetBeneficiaryOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetCurrentRedeemerOutputDTO : GetCurrentRedeemerOutputDTOBase { }

    [FunctionOutput]
    public class GetCurrentRedeemerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetLifetimeOutputDTO : GetLifetimeOutputDTOBase { }

    [FunctionOutput]
    public class GetLifetimeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetTimeoutOutputDTO : GetTimeoutOutputDTOBase { }

    [FunctionOutput]
    public class GetTimeoutOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }
}