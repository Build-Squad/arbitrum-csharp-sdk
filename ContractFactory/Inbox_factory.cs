using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory
{
    public partial class InboxDeployment : InboxDeploymentBase
    {
        public InboxDeployment() : base(BYTECODE) { }
        public InboxDeployment(string byteCode) : base(byteCode) { }
    }

    public class InboxDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE;
        public InboxDeploymentBase() : base(BYTECODE) { }
        public InboxDeploymentBase(string byteCode) : base(byteCode) 
        {
            BYTECODE = byteCode;
        }

        [Parameter("uint256", "_maxDataSize", 1)]
        public virtual BigInteger MaxDataSize { get; set; }
    }

    public partial class AllowListEnabledFunction : AllowListEnabledFunctionBase { }

    [Function("allowListEnabled", "bool")]
    public class AllowListEnabledFunctionBase : FunctionMessage
    {

    }

    public partial class BridgeFunction : BridgeFunctionBase { }

    [Function("bridge", "address")]
    public class BridgeFunctionBase : FunctionMessage
    {

    }

    public partial class CalculateRetryableSubmissionFeeFunction : CalculateRetryableSubmissionFeeFunctionBase { }

    [Function("calculateRetryableSubmissionFee", "uint256")]
    public class CalculateRetryableSubmissionFeeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "dataLength", 1)]
        public virtual BigInteger DataLength { get; set; }

        [Parameter("uint256", "baseFee", 2)]
        public virtual BigInteger BaseFee { get; set; }
    }

    public partial class CreateRetryableTicketFunction : CreateRetryableTicketFunctionBase { }

    [Function("createRetryableTicket", "uint256")]
    public class CreateRetryableTicketFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "l2CallValue", 2)]
        public virtual BigInteger L2CallValue { get; set; }
        [Parameter("uint256", "maxSubmissionCost", 3)]
        public virtual BigInteger MaxSubmissionCost { get; set; }
        [Parameter("address", "excessFeeRefundAddress", 4)]
        public virtual string ExcessFeeRefundAddress { get; set; }
        [Parameter("address", "callValueRefundAddress", 5)]
        public virtual string CallValueRefundAddress { get; set; }
        [Parameter("uint256", "gasLimit", 6)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 7)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("bytes", "data", 8)]
        public virtual byte[] Data { get; set; }
    }

    public partial class CreateRetryableTicketNoRefundAliasRewriteFunction : CreateRetryableTicketNoRefundAliasRewriteFunctionBase { }

    [Function("createRetryableTicketNoRefundAliasRewrite", "uint256")]
    public class CreateRetryableTicketNoRefundAliasRewriteFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "l2CallValue", 2)]
        public virtual BigInteger L2CallValue { get; set; }
        [Parameter("uint256", "maxSubmissionCost", 3)]
        public virtual BigInteger MaxSubmissionCost { get; set; }
        [Parameter("address", "excessFeeRefundAddress", 4)]
        public virtual string ExcessFeeRefundAddress { get; set; }
        [Parameter("address", "callValueRefundAddress", 5)]
        public virtual string CallValueRefundAddress { get; set; }
        [Parameter("uint256", "gasLimit", 6)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 7)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("bytes", "data", 8)]
        public virtual byte[] Data { get; set; }
    }

    public partial class DepositEth1Function : DepositEth1FunctionBase { }

    [Function("depositEth", "uint256")]
    public class DepositEth1FunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class DepositEthFunction : DepositEthFunctionBase { }

    [Function("depositEth", "uint256")]
    public class DepositEthFunctionBase : FunctionMessage
    {

    }

    public partial class GetProxyAdminFunction : GetProxyAdminFunctionBase { }

    [Function("getProxyAdmin", "address")]
    public class GetProxyAdminFunctionBase : FunctionMessage
    {

    }

    public partial class InitializeFunction : InitializeFunctionBase { }

    [Function("initialize")]
    public class InitializeFunctionBase : FunctionMessage
    {
        [Parameter("address", "_bridge", 1)]
        public virtual string Bridge { get; set; }
        [Parameter("address", "_sequencerInbox", 2)]
        public virtual string SequencerInbox { get; set; }
    }

    public partial class IsAllowedFunction : IsAllowedFunctionBase { }

    [Function("isAllowed", "bool")]
    public class IsAllowedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class MaxDataSizeFunction : MaxDataSizeFunctionBase { }

    [Function("maxDataSize", "uint256")]
    public class MaxDataSizeFunctionBase : FunctionMessage
    {

    }

    public partial class PauseFunction : PauseFunctionBase { }

    [Function("pause")]
    public class PauseFunctionBase : FunctionMessage
    {

    }

    public partial class PausedFunction : PausedFunctionBase { }

    [Function("paused", "bool")]
    public class PausedFunctionBase : FunctionMessage
    {

    }

    public partial class PostUpgradeInitFunction : PostUpgradeInitFunctionBase { }

    [Function("postUpgradeInit")]
    public class PostUpgradeInitFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SendContractTransactionFunction : SendContractTransactionFunctionBase { }

    [Function("sendContractTransaction", "uint256")]
    public class SendContractTransactionFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasLimit", 1)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 2)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("address", "to", 3)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 4)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SendL1FundedContractTransactionFunction : SendL1FundedContractTransactionFunctionBase { }

    [Function("sendL1FundedContractTransaction", "uint256")]
    public class SendL1FundedContractTransactionFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasLimit", 1)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 2)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("address", "to", 3)]
        public virtual string To { get; set; }
        [Parameter("bytes", "data", 4)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SendL1FundedUnsignedTransactionFunction : SendL1FundedUnsignedTransactionFunctionBase { }

    [Function("sendL1FundedUnsignedTransaction", "uint256")]
    public class SendL1FundedUnsignedTransactionFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasLimit", 1)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 2)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("uint256", "nonce", 3)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("bytes", "data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SendL1FundedUnsignedTransactionToForkFunction : SendL1FundedUnsignedTransactionToForkFunctionBase { }

    [Function("sendL1FundedUnsignedTransactionToFork", "uint256")]
    public class SendL1FundedUnsignedTransactionToForkFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasLimit", 1)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 2)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("uint256", "nonce", 3)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("bytes", "data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SendL2MessageFunction : SendL2MessageFunctionBase { }

    [Function("sendL2Message", "uint256")]
    public class SendL2MessageFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "messageData", 1)]
        public virtual byte[] MessageData { get; set; }
    }

    public partial class SendL2MessageFromOriginFunction : SendL2MessageFromOriginFunctionBase { }

    [Function("sendL2MessageFromOrigin", "uint256")]
    public class SendL2MessageFromOriginFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "messageData", 1)]
        public virtual byte[] MessageData { get; set; }
    }

    public partial class SendUnsignedTransactionFunction : SendUnsignedTransactionFunctionBase { }

    [Function("sendUnsignedTransaction", "uint256")]
    public class SendUnsignedTransactionFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasLimit", 1)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 2)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("uint256", "nonce", 3)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 5)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 6)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SendUnsignedTransactionToForkFunction : SendUnsignedTransactionToForkFunctionBase { }

    [Function("sendUnsignedTransactionToFork", "uint256")]
    public class SendUnsignedTransactionToForkFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasLimit", 1)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 2)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("uint256", "nonce", 3)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 5)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 6)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SendWithdrawEthToForkFunction : SendWithdrawEthToForkFunctionBase { }

    [Function("sendWithdrawEthToFork", "uint256")]
    public class SendWithdrawEthToForkFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasLimit", 1)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 2)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("uint256", "nonce", 3)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("uint256", "value", 4)]
        public virtual BigInteger Value { get; set; }
        [Parameter("address", "withdrawTo", 5)]
        public virtual string WithdrawTo { get; set; }
    }

    public partial class SequencerInboxFunction : SequencerInboxFunctionBase { }

    [Function("sequencerInbox", "address")]
    public class SequencerInboxFunctionBase : FunctionMessage
    {

    }

    public partial class SetAllowListFunction : SetAllowListFunctionBase { }

    [Function("setAllowList")]
    public class SetAllowListFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "user", 1)]
        public virtual List<string> User { get; set; }
        [Parameter("bool[]", "val", 2)]
        public virtual List<bool> Val { get; set; }
    }

    public partial class SetAllowListEnabledFunction : SetAllowListEnabledFunctionBase { }

    [Function("setAllowListEnabled")]
    public class SetAllowListEnabledFunctionBase : FunctionMessage
    {
        [Parameter("bool", "_allowListEnabled", 1)]
        public virtual bool AllowListEnabled { get; set; }
    }

    public partial class UnpauseFunction : UnpauseFunctionBase { }

    [Function("unpause")]
    public class UnpauseFunctionBase : FunctionMessage
    {

    }

    public partial class UnsafeCreateRetryableTicketFunction : UnsafeCreateRetryableTicketFunctionBase { }

    [Function("unsafeCreateRetryableTicket", "uint256")]
    public class UnsafeCreateRetryableTicketFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "l2CallValue", 2)]
        public virtual BigInteger L2CallValue { get; set; }
        [Parameter("uint256", "maxSubmissionCost", 3)]
        public virtual BigInteger MaxSubmissionCost { get; set; }
        [Parameter("address", "excessFeeRefundAddress", 4)]
        public virtual string ExcessFeeRefundAddress { get; set; }
        [Parameter("address", "callValueRefundAddress", 5)]
        public virtual string CallValueRefundAddress { get; set; }
        [Parameter("uint256", "gasLimit", 6)]
        public virtual BigInteger GasLimit { get; set; }
        [Parameter("uint256", "maxFeePerGas", 7)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("bytes", "data", 8)]
        public virtual byte[] Data { get; set; }
    }

    public partial class AllowListAddressSetEventDTO : AllowListAddressSetEventDTOBase { }

    [Event("AllowListAddressSet")]
    public class AllowListAddressSetEventDTOBase : IEventDTO
    {
        [Parameter("address", "user", 1, true)]
        public virtual string User { get; set; }
        [Parameter("bool", "val", 2, false)]
        public virtual bool Val { get; set; }
    }

    public partial class AllowListEnabledUpdatedEventDTO : AllowListEnabledUpdatedEventDTOBase { }

    [Event("AllowListEnabledUpdated")]
    public class AllowListEnabledUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("bool", "isEnabled", 1, false)]
        public virtual bool IsEnabled { get; set; }
    }

    public partial class InboxMessageDeliveredEventDTO : InboxMessageDeliveredEventDTOBase { }

    [Event("InboxMessageDelivered")]
    public class InboxMessageDeliveredEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "messageNum", 1, true)]
        public virtual BigInteger MessageNum { get; set; }
        [Parameter("bytes", "data", 2, false)]
        public virtual byte[] Data { get; set; }
    }

    public partial class InboxMessageDeliveredFromOriginEventDTO : InboxMessageDeliveredFromOriginEventDTOBase { }

    [Event("InboxMessageDeliveredFromOrigin")]
    public class InboxMessageDeliveredFromOriginEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "messageNum", 1, true)]
        public virtual BigInteger MessageNum { get; set; }
    }

    public partial class PausedEventDTO : PausedEventDTOBase { }

    [Event("Paused")]
    public class PausedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, false)]
        public virtual string Account { get; set; }
    }

    public partial class UnpausedEventDTO : UnpausedEventDTOBase { }

    [Event("Unpaused")]
    public class UnpausedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, false)]
        public virtual string Account { get; set; }
    }

    public partial class AllowListEnabledOutputDTO : AllowListEnabledOutputDTOBase { }

    [FunctionOutput]
    public class AllowListEnabledOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class BridgeOutputDTO : BridgeOutputDTOBase { }

    [FunctionOutput]
    public class BridgeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class CalculateRetryableSubmissionFeeOutputDTO : CalculateRetryableSubmissionFeeOutputDTOBase { }

    [FunctionOutput]
    public class CalculateRetryableSubmissionFeeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetProxyAdminOutputDTO : GetProxyAdminOutputDTOBase { }

    [FunctionOutput]
    public class GetProxyAdminOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsAllowedOutputDTO : IsAllowedOutputDTOBase { }

    [FunctionOutput]
    public class IsAllowedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class MaxDataSizeOutputDTO : MaxDataSizeOutputDTOBase { }

    [FunctionOutput]
    public class MaxDataSizeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class PausedOutputDTO : PausedOutputDTOBase { }

    [FunctionOutput]
    public class PausedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class SequencerInboxOutputDTO : SequencerInboxOutputDTOBase { }

    [FunctionOutput]
    public class SequencerInboxOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
