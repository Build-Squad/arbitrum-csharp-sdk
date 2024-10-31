using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory.L1GatewayRouter
{
    public partial class L1GatewayRouterDeployment : L1GatewayRouterDeploymentBase
    {
        public L1GatewayRouterDeployment() : base(BYTECODE) { }
        public L1GatewayRouterDeployment(string byteCode) : base(byteCode) 
        {
            BYTECODE = byteCode;
        }
    }

    public class L1GatewayRouterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE;
        public L1GatewayRouterDeploymentBase() : base(BYTECODE) { }
        public L1GatewayRouterDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CalculateL2TokenAddressFunction : CalculateL2TokenAddressFunctionBase { }

    [Function("calculateL2TokenAddress", "address")]
    public class CalculateL2TokenAddressFunctionBase : FunctionMessage
    {
        [Parameter("address", "l1ERC20", 1)]
        public virtual string L1ERC20 { get; set; }
    }

    public partial class CounterpartGatewayFunction : CounterpartGatewayFunctionBase { }

    [Function("counterpartGateway", "address")]
    public class CounterpartGatewayFunctionBase : FunctionMessage
    {

    }

    public partial class DefaultGatewayFunction : DefaultGatewayFunctionBase { }

    [Function("defaultGateway", "address")]
    public class DefaultGatewayFunctionBase : FunctionMessage
    {

    }

    public partial class FinalizeInboundTransferFunction : FinalizeInboundTransferFunctionBase { }

    [Function("finalizeInboundTransfer")]
    public class FinalizeInboundTransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
        [Parameter("bytes", "", 5)]
        public virtual byte[] ReturnValue5 { get; set; }
    }

    public partial class GetGatewayFunction : GetGatewayFunctionBase { }

    [Function("getGateway", "address")]
    public class GetGatewayFunctionBase : FunctionMessage
    {
        [Parameter("address", "_token", 1)]
        public virtual string Token { get; set; }
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
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("address", "_defaultGateway", 2)]
        public virtual string DefaultGateway { get; set; }
        [Parameter("address", "", 3)]
        public virtual string ReturnValue3 { get; set; }
        [Parameter("address", "_counterpartGateway", 4)]
        public virtual string CounterpartGateway { get; set; }
        [Parameter("address", "_inbox", 5)]
        public virtual string Inbox { get; set; }
    }

    public partial class L1TokenToGatewayFunction : L1TokenToGatewayFunctionBase { }

    [Function("l1TokenToGateway", "address")]
    public class L1TokenToGatewayFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class OutboundTransferFunction2 : OutboundTransferFunctionBase2 { }

    [Function("outboundTransfer", "bytes")]
    public class OutboundTransferFunctionBase2 : FunctionMessage
    {
        [Parameter("address", "_l1Token", 1)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 3)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("bytes", "_data", 4)]
        public virtual byte[] Data { get; set; }
    }

    public partial class OutboundTransferFunction : OutboundTransferFunctionBase { }

    [Function("outboundTransfer", "bytes")]
    public class OutboundTransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "_token", 1)]
        public virtual string Token { get; set; }
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
        [Parameter("address", "_token", 1)]
        public virtual string Token { get; set; }
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

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class PostUpgradeInitFunction : PostUpgradeInitFunctionBase { }

    [Function("postUpgradeInit")]
    public class PostUpgradeInitFunctionBase : FunctionMessage
    {

    }

    public partial class RouterFunction : RouterFunctionBase { }

    [Function("router", "address")]
    public class RouterFunctionBase : FunctionMessage
    {

    }

    public partial class SetDefaultGatewayFunction : SetDefaultGatewayFunctionBase { }

    [Function("setDefaultGateway", "uint256")]
    public class SetDefaultGatewayFunctionBase : FunctionMessage
    {
        [Parameter("address", "newL1DefaultGateway", 1)]
        public virtual string NewL1DefaultGateway { get; set; }
        [Parameter("uint256", "_maxGas", 2)]
        public virtual BigInteger MaxGas { get; set; }
        [Parameter("uint256", "_gasPriceBid", 3)]
        public virtual BigInteger GasPriceBid { get; set; }
        [Parameter("uint256", "_maxSubmissionCost", 4)]
        public virtual BigInteger MaxSubmissionCost { get; set; }
    }

    public partial class SetGateway1Function : SetGateway1FunctionBase { }

    [Function("setGateway", "uint256")]
    public class SetGateway1FunctionBase : FunctionMessage
    {
        [Parameter("address", "_gateway", 1)]
        public virtual string Gateway { get; set; }
        [Parameter("uint256", "_maxGas", 2)]
        public virtual BigInteger MaxGas { get; set; }
        [Parameter("uint256", "_gasPriceBid", 3)]
        public virtual BigInteger GasPriceBid { get; set; }
        [Parameter("uint256", "_maxSubmissionCost", 4)]
        public virtual BigInteger MaxSubmissionCost { get; set; }
        [Parameter("address", "_creditBackAddress", 5)]
        public virtual string CreditBackAddress { get; set; }
    }

    public partial class SetGatewayFunction : SetGatewayFunctionBase { }

    [Function("setGateway", "uint256")]
    public class SetGatewayFunctionBase : FunctionMessage
    {
        [Parameter("address", "_gateway", 1)]
        public virtual string Gateway { get; set; }
        [Parameter("uint256", "_maxGas", 2)]
        public virtual BigInteger MaxGas { get; set; }
        [Parameter("uint256", "_gasPriceBid", 3)]
        public virtual BigInteger GasPriceBid { get; set; }
        [Parameter("uint256", "_maxSubmissionCost", 4)]
        public virtual BigInteger MaxSubmissionCost { get; set; }
    }

    public partial class SetGatewaysFunction : SetGatewaysFunctionBase { }

    [Function("setGateways", "uint256")]
    public class SetGatewaysFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "_token", 1)]
        public virtual List<string> Token { get; set; }
        [Parameter("address[]", "_gateway", 2)]
        public virtual List<string> Gateway { get; set; }
        [Parameter("uint256", "_maxGas", 3)]
        public virtual BigInteger MaxGas { get; set; }
        [Parameter("uint256", "_gasPriceBid", 4)]
        public virtual BigInteger GasPriceBid { get; set; }
        [Parameter("uint256", "_maxSubmissionCost", 5)]
        public virtual BigInteger MaxSubmissionCost { get; set; }
    }

    public partial class SetOwnerFunction : SetOwnerFunctionBase { }

    [Function("setOwner")]
    public class SetOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class UpdateWhitelistSourceFunction : UpdateWhitelistSourceFunctionBase { }

    [Function("updateWhitelistSource")]
    public class UpdateWhitelistSourceFunctionBase : FunctionMessage
    {
        [Parameter("address", "newSource", 1)]
        public virtual string NewSource { get; set; }
    }

    public partial class WhitelistFunction : WhitelistFunctionBase { }

    [Function("whitelist", "address")]
    public class WhitelistFunctionBase : FunctionMessage
    {

    }

    public partial class DefaultGatewayUpdatedEventDTO : DefaultGatewayUpdatedEventDTOBase { }

    [Event("DefaultGatewayUpdated")]
    public class DefaultGatewayUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "newDefaultGateway", 1, false)]
        public virtual string NewDefaultGateway { get; set; }
    }

    public partial class GatewaySetEventDTO : GatewaySetEventDTOBase { }

    [Event("GatewaySet")]
    public class GatewaySetEventDTOBase : IEventDTO
    {
        [Parameter("address", "l1Token", 1, true)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "gateway", 2, true)]
        public virtual string Gateway { get; set; }
    }

    public partial class TransferRoutedEventDTO : TransferRoutedEventDTOBase { }

    [Event("TransferRouted")]
    public class TransferRoutedEventDTOBase : IEventDTO
    {
        [Parameter("address", "token", 1, true)]
        public virtual string Token { get; set; }
        [Parameter("address", "_userFrom", 2, true)]
        public virtual string UserFrom { get; set; }
        [Parameter("address", "_userTo", 3, true)]
        public virtual string UserTo { get; set; }
        [Parameter("address", "gateway", 4, false)]
        public virtual string Gateway { get; set; }
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

    public partial class WhitelistSourceUpdatedEventDTO : WhitelistSourceUpdatedEventDTOBase { }

    [Event("WhitelistSourceUpdated")]
    public class WhitelistSourceUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "newSource", 1, false)]
        public virtual string NewSource { get; set; }
    }

    public partial class CalculateL2TokenAddressOutputDTO : CalculateL2TokenAddressOutputDTOBase { }

    [FunctionOutput]
    public class CalculateL2TokenAddressOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class CounterpartGatewayOutputDTO : CounterpartGatewayOutputDTOBase { }

    [FunctionOutput]
    public class CounterpartGatewayOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class DefaultGatewayOutputDTO : DefaultGatewayOutputDTOBase { }

    [FunctionOutput]
    public class DefaultGatewayOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class GetGatewayOutputDTO : GetGatewayOutputDTOBase { }

    [FunctionOutput]
    public class GetGatewayOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "gateway", 1)]
        public virtual string Gateway { get; set; }
    }

    public partial class GetOutboundCalldataOutputDTO : GetOutboundCalldataOutputDTOBase { }

    [FunctionOutput]
    public class GetOutboundCalldataOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class InboxOutputDTO : InboxOutputDTOBase { }

    [FunctionOutput]
    public class InboxOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class L1TokenToGatewayOutputDTO : L1TokenToGatewayOutputDTOBase { }

    [FunctionOutput]
    public class L1TokenToGatewayOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
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
