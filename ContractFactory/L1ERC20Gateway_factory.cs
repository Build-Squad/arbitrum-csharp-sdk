using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory.L1ERC20Gateway
{
    public partial class L1ERC20GatewayDeployment : L1ERC20GatewayDeploymentBase
    {
        public L1ERC20GatewayDeployment() : base(BYTECODE) { }
        public L1ERC20GatewayDeployment(string byteCode) : base(byteCode) { }
    }

    public class L1ERC20GatewayDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public L1ERC20GatewayDeploymentBase() : base(BYTECODE) { }
        public L1ERC20GatewayDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CalculateL2TokenAddressFunction : CalculateL2TokenAddressFunctionBase { }

    [Function("calculateL2TokenAddress", "address")]
    public class CalculateL2TokenAddressFunctionBase : FunctionMessage
    {
        [Parameter("address", "l1ERC20", 1)]
        public virtual string L1ERC20 { get; set; }
    }

    public partial class CloneableProxyHashFunction : CloneableProxyHashFunctionBase { }

    [Function("cloneableProxyHash", "bytes32")]
    public class CloneableProxyHashFunctionBase : FunctionMessage
    {

    }

    public partial class CounterpartGatewayFunction : CounterpartGatewayFunctionBase { }

    [Function("counterpartGateway", "address")]
    public class CounterpartGatewayFunctionBase : FunctionMessage
    {

    }

    public partial class EncodeWithdrawalFunction : EncodeWithdrawalFunctionBase { }

    [Function("encodeWithdrawal", "bytes32")]
    public class EncodeWithdrawalFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_exitNum", 1)]
        public virtual BigInteger ExitNum { get; set; }
        [Parameter("address", "_initialDestination", 2)]
        public virtual string InitialDestination { get; set; }
    }

    public partial class FinalizeInboundTransferFunction : FinalizeInboundTransferFunctionBase { }

    [Function("finalizeInboundTransfer")]
    public class FinalizeInboundTransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "_token", 1)]
        public virtual string Token { get; set; }
        [Parameter("address", "_from", 2)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 3)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 4)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("bytes", "_data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class GetExternalCallFunction : GetExternalCallFunctionBase { }

    [Function("getExternalCall", typeof(GetExternalCallOutputDTO))]
    public class GetExternalCallFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_exitNum", 1)]
        public virtual BigInteger ExitNum { get; set; }
        [Parameter("address", "_initialDestination", 2)]
        public virtual string InitialDestination { get; set; }
        [Parameter("bytes", "_initialData", 3)]
        public virtual byte[] InitialData { get; set; }
    }

    public partial class GetOutboundCalldataFunction : GetOutboundCalldataFunctionBase { }

    [Function("getOutboundCalldata", "bytes")]
    public class GetOutboundCalldataFunctionBase : FunctionMessage
    {
        [Parameter("address", "_token", 1)]
        public virtual string Token { get; set; }
        [Parameter("address", "_from", 2)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 3)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 4)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("bytes", "_data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class InboxFunction : InboxFunctionBase { }

    [Function("inbox", "address")]
    public class InboxFunctionBase : FunctionMessage
    {

    }

    public partial class InitializeFunction : InitializeFunctionBase { }

    [Function("initialize")]
    public class InitializeFunctionBase : FunctionMessage
    {
        [Parameter("address", "_l2Counterpart", 1)]
        public virtual string L2Counterpart { get; set; }
        [Parameter("address", "_router", 2)]
        public virtual string Router { get; set; }
        [Parameter("address", "_inbox", 3)]
        public virtual string Inbox { get; set; }
        [Parameter("bytes32", "_cloneableProxyHash", 4)]
        public virtual byte[] CloneableProxyHash { get; set; }
        [Parameter("address", "_l2BeaconProxyFactory", 5)]
        public virtual string L2BeaconProxyFactory { get; set; }
    }

    public partial class L2BeaconProxyFactoryFunction : L2BeaconProxyFactoryFunctionBase { }

    [Function("l2BeaconProxyFactory", "address")]
    public class L2BeaconProxyFactoryFunctionBase : FunctionMessage
    {

    }

    public partial class OutboundTransferFunction : OutboundTransferFunctionBase { }

    [Function("outboundTransfer", "bytes")]
    public class OutboundTransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "_l1Token", 1)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 3)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "_maxGas", 4)]
        public virtual BigInteger MaxGas { get; set; }
        [Parameter("uint256", "_gasPriceBid", 5)]
        public virtual BigInteger GasPriceBid { get; set; }
        [Parameter("bytes", "_data", 6)]
        public virtual byte[] Data { get; set; }
    }

    public partial class OutboundTransferCustomRefundFunction : OutboundTransferCustomRefundFunctionBase { }

    [Function("outboundTransferCustomRefund", "bytes")]
    public class OutboundTransferCustomRefundFunctionBase : FunctionMessage
    {
        [Parameter("address", "_l1Token", 1)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_refundTo", 2)]
        public virtual string RefundTo { get; set; }
        [Parameter("address", "_to", 3)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 4)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "_maxGas", 5)]
        public virtual BigInteger MaxGas { get; set; }
        [Parameter("uint256", "_gasPriceBid", 6)]
        public virtual BigInteger GasPriceBid { get; set; }
        [Parameter("bytes", "_data", 7)]
        public virtual byte[] Data { get; set; }
    }

    public partial class PostUpgradeInitFunction : PostUpgradeInitFunctionBase { }

    [Function("postUpgradeInit")]
    public class PostUpgradeInitFunctionBase : FunctionMessage
    {

    }

    public partial class RedirectedExitsFunction : RedirectedExitsFunctionBase { }

    [Function("redirectedExits", typeof(RedirectedExitsOutputDTO))]
    public class RedirectedExitsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class RouterFunction : RouterFunctionBase { }

    [Function("router", "address")]
    public class RouterFunctionBase : FunctionMessage
    {

    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class TransferExitAndCallFunction : TransferExitAndCallFunctionBase { }

    [Function("transferExitAndCall")]
    public class TransferExitAndCallFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_exitNum", 1)]
        public virtual BigInteger ExitNum { get; set; }
        [Parameter("address", "_initialDestination", 2)]
        public virtual string InitialDestination { get; set; }
        [Parameter("address", "_newDestination", 3)]
        public virtual string NewDestination { get; set; }
        [Parameter("bytes", "_newData", 4)]
        public virtual byte[] NewData { get; set; }
        [Parameter("bytes", "_data", 5)]
        public virtual byte[] Data { get; set; }
    }

    public partial class WhitelistFunction : WhitelistFunctionBase { }

    [Function("whitelist", "address")]
    public class WhitelistFunctionBase : FunctionMessage
    {

    }

    public partial class DepositInitiatedEventDTO : DepositInitiatedEventDTOBase { }

    [Event("DepositInitiated")]
    public class DepositInitiatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "l1Token", 1, false)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_from", 2, true)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 3, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_sequenceNumber", 4, true)]
        public virtual BigInteger SequenceNumber { get; set; }
        [Parameter("uint256", "_amount", 5, false)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TxToL2EventDTO : TxToL2EventDTOBase { }

    [Event("TxToL2")]
    public class TxToL2EventDTOBase : IEventDTO
    {
        [Parameter("address", "_from", 1, true)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 2, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_seqNum", 3, true)]
        public virtual BigInteger SeqNum { get; set; }
        [Parameter("bytes", "_data", 4, false)]
        public virtual byte[] Data { get; set; }
    }

    public partial class WithdrawRedirectedEventDTO : WithdrawRedirectedEventDTOBase { }

    [Event("WithdrawRedirected")]
    public class WithdrawRedirectedEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "exitNum", 3, true)]
        public virtual BigInteger ExitNum { get; set; }
        [Parameter("bytes", "newData", 4, false)]
        public virtual byte[] NewData { get; set; }
        [Parameter("bytes", "data", 5, false)]
        public virtual byte[] Data { get; set; }
        [Parameter("bool", "madeExternalCall", 6, false)]
        public virtual bool MadeExternalCall { get; set; }
    }

    public partial class WithdrawalFinalizedEventDTO : WithdrawalFinalizedEventDTOBase { }

    [Event("WithdrawalFinalized")]
    public class WithdrawalFinalizedEventDTOBase : IEventDTO
    {
        [Parameter("address", "l1Token", 1, false)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_from", 2, true)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 3, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_exitNum", 4, true)]
        public virtual BigInteger ExitNum { get; set; }
        [Parameter("uint256", "_amount", 5, false)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class CalculateL2TokenAddressOutputDTO : CalculateL2TokenAddressOutputDTOBase { }

    [FunctionOutput]
    public class CalculateL2TokenAddressOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class CloneableProxyHashOutputDTO : CloneableProxyHashOutputDTOBase { }

    [FunctionOutput]
    public class CloneableProxyHashOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class CounterpartGatewayOutputDTO : CounterpartGatewayOutputDTOBase { }

    [FunctionOutput]
    public class CounterpartGatewayOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class EncodeWithdrawalOutputDTO : EncodeWithdrawalOutputDTOBase { }

    [FunctionOutput]
    public class EncodeWithdrawalOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }



    public partial class GetExternalCallOutputDTO : GetExternalCallOutputDTOBase { }

    [FunctionOutput]
    public class GetExternalCallOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }

    public partial class GetOutboundCalldataOutputDTO : GetOutboundCalldataOutputDTOBase { }

    [FunctionOutput]
    public class GetOutboundCalldataOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes", "outboundCalldata", 1)]
        public virtual byte[] OutboundCalldata { get; set; }
    }

    public partial class InboxOutputDTO : InboxOutputDTOBase { }

    [FunctionOutput]
    public class InboxOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class L2BeaconProxyFactoryOutputDTO : L2BeaconProxyFactoryOutputDTOBase { }

    [FunctionOutput]
    public class L2BeaconProxyFactoryOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }







    public partial class RedirectedExitsOutputDTO : RedirectedExitsOutputDTOBase { }

    [FunctionOutput]
    public class RedirectedExitsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "isExit", 1)]
        public virtual bool IsExit { get; set; }
        [Parameter("address", "_newTo", 2)]
        public virtual string NewTo { get; set; }
        [Parameter("bytes", "_newData", 3)]
        public virtual byte[] NewData { get; set; }
    }

    public partial class RouterOutputDTO : RouterOutputDTOBase { }

    [FunctionOutput]
    public class RouterOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class WhitelistOutputDTO : WhitelistOutputDTOBase { }

    [FunctionOutput]
    public class WhitelistOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
