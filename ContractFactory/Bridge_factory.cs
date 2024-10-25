using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory.Bridge
{
    public partial class BridgeDeployment : BridgeDeploymentBase
    {
        public BridgeDeployment() : base(BYTECODE) { }
        public BridgeDeployment(string byteCode) : base(byteCode) 
        {
            BYTECODE = byteCode;
        }
    }

    public class BridgeDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public BridgeDeploymentBase() : base(BYTECODE) { }
        public BridgeDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AcceptFundsFromOldBridgeFunction : AcceptFundsFromOldBridgeFunctionBase { }

    [Function("acceptFundsFromOldBridge")]
    public class AcceptFundsFromOldBridgeFunctionBase : FunctionMessage
    {

    }

    public partial class ActiveOutboxFunction : ActiveOutboxFunctionBase { }

    [Function("activeOutbox", "address")]
    public class ActiveOutboxFunctionBase : FunctionMessage
    {

    }

    public partial class AllowedDelayedInboxListFunction : AllowedDelayedInboxListFunctionBase { }

    [Function("allowedDelayedInboxList", "address")]
    public class AllowedDelayedInboxListFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AllowedDelayedInboxesFunction : AllowedDelayedInboxesFunctionBase { }

    [Function("allowedDelayedInboxes", "bool")]
    public class AllowedDelayedInboxesFunctionBase : FunctionMessage
    {
        [Parameter("address", "inbox", 1)]
        public virtual string Inbox { get; set; }
    }

    public partial class AllowedOutboxListFunction : AllowedOutboxListFunctionBase { }

    [Function("allowedOutboxList", "address")]
    public class AllowedOutboxListFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AllowedOutboxesFunction : AllowedOutboxesFunctionBase { }

    [Function("allowedOutboxes", "bool")]
    public class AllowedOutboxesFunctionBase : FunctionMessage
    {
        [Parameter("address", "outbox", 1)]
        public virtual string Outbox { get; set; }
    }

    public partial class DelayedInboxAccsFunction : DelayedInboxAccsFunctionBase { }

    [Function("delayedInboxAccs", "bytes32")]
    public class DelayedInboxAccsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class DelayedMessageCountFunction : DelayedMessageCountFunctionBase { }

    [Function("delayedMessageCount", "uint256")]
    public class DelayedMessageCountFunctionBase : FunctionMessage
    {

    }

    public partial class EnqueueDelayedMessageFunction : EnqueueDelayedMessageFunctionBase { }

    [Function("enqueueDelayedMessage", "uint256")]
    public class EnqueueDelayedMessageFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "kind", 1)]
        public virtual byte Kind { get; set; }
        [Parameter("address", "sender", 2)]
        public virtual string Sender { get; set; }
        [Parameter("bytes32", "messageDataHash", 3)]
        public virtual byte[] MessageDataHash { get; set; }
    }

    public partial class EnqueueSequencerMessageFunction : EnqueueSequencerMessageFunctionBase { }

    [Function("enqueueSequencerMessage", typeof(EnqueueSequencerMessageOutputDTO))]
    public class EnqueueSequencerMessageFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "dataHash", 1)]
        public virtual byte[] DataHash { get; set; }
        [Parameter("uint256", "afterDelayedMessagesRead", 2)]
        public virtual BigInteger AfterDelayedMessagesRead { get; set; }
        [Parameter("uint256", "prevMessageCount", 3)]
        public virtual BigInteger PrevMessageCount { get; set; }
        [Parameter("uint256", "newMessageCount", 4)]
        public virtual BigInteger NewMessageCount { get; set; }
    }

    public partial class ExecuteCallFunction : ExecuteCallFunctionBase { }

    [Function("executeCall", typeof(ExecuteCallOutputDTO))]
    public class ExecuteCallFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class InitializeFunction : InitializeFunctionBase { }

    [Function("initialize")]
    public class InitializeFunctionBase : FunctionMessage
    {
        [Parameter("address", "rollup_", 1)]
        public virtual string Rollup { get; set; }
    }

    public partial class RollupFunction : RollupFunctionBase { }

    [Function("rollup", "address")]
    public class RollupFunctionBase : FunctionMessage
    {

    }

    public partial class SequencerInboxFunction : SequencerInboxFunctionBase { }

    [Function("sequencerInbox", "address")]
    public class SequencerInboxFunctionBase : FunctionMessage
    {

    }

    public partial class SequencerInboxAccsFunction : SequencerInboxAccsFunctionBase { }

    [Function("sequencerInboxAccs", "bytes32")]
    public class SequencerInboxAccsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class SequencerMessageCountFunction : SequencerMessageCountFunctionBase { }

    [Function("sequencerMessageCount", "uint256")]
    public class SequencerMessageCountFunctionBase : FunctionMessage
    {

    }

    public partial class SequencerReportedSubMessageCountFunction : SequencerReportedSubMessageCountFunctionBase { }

    [Function("sequencerReportedSubMessageCount", "uint256")]
    public class SequencerReportedSubMessageCountFunctionBase : FunctionMessage
    {

    }

    public partial class SetDelayedInboxFunction : SetDelayedInboxFunctionBase { }

    [Function("setDelayedInbox")]
    public class SetDelayedInboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "inbox", 1)]
        public virtual string Inbox { get; set; }
        [Parameter("bool", "enabled", 2)]
        public virtual bool Enabled { get; set; }
    }

    public partial class SetOutboxFunction : SetOutboxFunctionBase { }

    [Function("setOutbox")]
    public class SetOutboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "outbox", 1)]
        public virtual string Outbox { get; set; }
        [Parameter("bool", "enabled", 2)]
        public virtual bool Enabled { get; set; }
    }

    public partial class SetSequencerInboxFunction : SetSequencerInboxFunctionBase { }

    [Function("setSequencerInbox")]
    public class SetSequencerInboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "_sequencerInbox", 1)]
        public virtual string SequencerInbox { get; set; }
    }

    public partial class SetSequencerReportedSubMessageCountFunction : SetSequencerReportedSubMessageCountFunctionBase { }

    [Function("setSequencerReportedSubMessageCount")]
    public class SetSequencerReportedSubMessageCountFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "newMsgCount", 1)]
        public virtual BigInteger NewMsgCount { get; set; }
    }

    public partial class SubmitBatchSpendingReportFunction : SubmitBatchSpendingReportFunctionBase { }

    [Function("submitBatchSpendingReport", "uint256")]
    public class SubmitBatchSpendingReportFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("bytes32", "messageDataHash", 2)]
        public virtual byte[] MessageDataHash { get; set; }
    }

    public partial class UpdateRollupAddressFunction : UpdateRollupAddressFunctionBase { }

    [Function("updateRollupAddress")]
    public class UpdateRollupAddressFunctionBase : FunctionMessage
    {
        [Parameter("address", "_rollup", 1)]
        public virtual string Rollup { get; set; }
    }

    public partial class BridgeCallTriggeredEventDTO : BridgeCallTriggeredEventDTOBase { }

    [Event("BridgeCallTriggered")]
    public class BridgeCallTriggeredEventDTOBase : IEventDTO
    {
        [Parameter("address", "outbox", 1, true)]
        public virtual string Outbox { get; set; }
        [Parameter("address", "to", 2, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3, false)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 4, false)]
        public virtual byte[] Data { get; set; }
    }

    public partial class InboxToggleEventDTO : InboxToggleEventDTOBase { }

    [Event("InboxToggle")]
    public class InboxToggleEventDTOBase : IEventDTO
    {
        [Parameter("address", "inbox", 1, true)]
        public virtual string Inbox { get; set; }
        [Parameter("bool", "enabled", 2, false)]
        public virtual bool Enabled { get; set; }
    }

    public partial class MessageDeliveredEventDTO : MessageDeliveredEventDTOBase { }

    [Event("MessageDelivered")]
    public class MessageDeliveredEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "messageIndex", 1, true)]
        public virtual BigInteger MessageIndex { get; set; }
        [Parameter("bytes32", "beforeInboxAcc", 2, true)]
        public virtual byte[] BeforeInboxAcc { get; set; }
        [Parameter("address", "inbox", 3, false)]
        public virtual string Inbox { get; set; }
        [Parameter("uint8", "kind", 4, false)]
        public virtual byte Kind { get; set; }
        [Parameter("address", "sender", 5, false)]
        public virtual string Sender { get; set; }
        [Parameter("bytes32", "messageDataHash", 6, false)]
        public virtual byte[] MessageDataHash { get; set; }
        [Parameter("uint256", "baseFeeL1", 7, false)]
        public virtual BigInteger BaseFeeL1 { get; set; }
        [Parameter("uint64", "timestamp", 8, false)]
        public virtual ulong Timestamp { get; set; }
    }

    public partial class OutboxToggleEventDTO : OutboxToggleEventDTOBase { }

    [Event("OutboxToggle")]
    public class OutboxToggleEventDTOBase : IEventDTO
    {
        [Parameter("address", "outbox", 1, true)]
        public virtual string Outbox { get; set; }
        [Parameter("bool", "enabled", 2, false)]
        public virtual bool Enabled { get; set; }
    }

    public partial class RollupUpdatedEventDTO : RollupUpdatedEventDTOBase { }

    [Event("RollupUpdated")]
    public class RollupUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "rollup", 1, false)]
        public virtual string Rollup { get; set; }
    }

    public partial class SequencerInboxUpdatedEventDTO : SequencerInboxUpdatedEventDTOBase { }

    [Event("SequencerInboxUpdated")]
    public class SequencerInboxUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "newSequencerInbox", 1, false)]
        public virtual string NewSequencerInbox { get; set; }
    }



    public partial class ActiveOutboxOutputDTO : ActiveOutboxOutputDTOBase { }

    [FunctionOutput]
    public class ActiveOutboxOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AllowedDelayedInboxListOutputDTO : AllowedDelayedInboxListOutputDTOBase { }

    [FunctionOutput]
    public class AllowedDelayedInboxListOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AllowedDelayedInboxesOutputDTO : AllowedDelayedInboxesOutputDTOBase { }

    [FunctionOutput]
    public class AllowedDelayedInboxesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class AllowedOutboxListOutputDTO : AllowedOutboxListOutputDTOBase { }

    [FunctionOutput]
    public class AllowedOutboxListOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AllowedOutboxesOutputDTO : AllowedOutboxesOutputDTOBase { }

    [FunctionOutput]
    public class AllowedOutboxesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class DelayedInboxAccsOutputDTO : DelayedInboxAccsOutputDTOBase { }

    [FunctionOutput]
    public class DelayedInboxAccsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class DelayedMessageCountOutputDTO : DelayedMessageCountOutputDTOBase { }

    [FunctionOutput]
    public class DelayedMessageCountOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class EnqueueSequencerMessageOutputDTO : EnqueueSequencerMessageOutputDTOBase { }

    [FunctionOutput]
    public class EnqueueSequencerMessageOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "seqMessageIndex", 1)]
        public virtual BigInteger SeqMessageIndex { get; set; }
        [Parameter("bytes32", "beforeAcc", 2)]
        public virtual byte[] BeforeAcc { get; set; }
        [Parameter("bytes32", "delayedAcc", 3)]
        public virtual byte[] DelayedAcc { get; set; }
        [Parameter("bytes32", "acc", 4)]
        public virtual byte[] Acc { get; set; }
    }

    public partial class ExecuteCallOutputDTO : ExecuteCallOutputDTOBase { }

    [FunctionOutput]
    public class ExecuteCallOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "success", 1)]
        public virtual bool Success { get; set; }
        [Parameter("bytes", "returnData", 2)]
        public virtual byte[] ReturnData { get; set; }
    }



    public partial class RollupOutputDTO : RollupOutputDTOBase { }

    [FunctionOutput]
    public class RollupOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SequencerInboxOutputDTO : SequencerInboxOutputDTOBase { }

    [FunctionOutput]
    public class SequencerInboxOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SequencerInboxAccsOutputDTO : SequencerInboxAccsOutputDTOBase { }

    [FunctionOutput]
    public class SequencerInboxAccsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SequencerMessageCountOutputDTO : SequencerMessageCountOutputDTOBase { }

    [FunctionOutput]
    public class SequencerMessageCountOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class SequencerReportedSubMessageCountOutputDTO : SequencerReportedSubMessageCountOutputDTOBase { }

    [FunctionOutput]
    public class SequencerReportedSubMessageCountOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }
}