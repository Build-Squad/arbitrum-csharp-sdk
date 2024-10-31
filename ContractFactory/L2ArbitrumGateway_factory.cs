using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory.L2ArbitrumGateway
{
    public partial class L2ArbitrumGatewayDeployment : L2ArbitrumGatewayDeploymentBase
    {
        public L2ArbitrumGatewayDeployment() : base(BYTECODE) { }
        public L2ArbitrumGatewayDeployment(string byteCode) : base(byteCode) { }
    }

    public class L2ArbitrumGatewayDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public L2ArbitrumGatewayDeploymentBase() : base(BYTECODE) { }
        public L2ArbitrumGatewayDeploymentBase(string byteCode) : base(byteCode) { }

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

    public partial class ExitNumFunction : ExitNumFunctionBase { }

    [Function("exitNum", "uint256")]
    public class ExitNumFunctionBase : FunctionMessage
    {

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
        [Parameter("bytes", "_data", 4)]
        public virtual byte[] Data { get; set; }
    }

    public partial class OutboundTransfer1Function : OutboundTransfer1FunctionBase { }

    [Function("outboundTransfer", "bytes")]
    public class OutboundTransfer1FunctionBase : FunctionMessage
    {
        [Parameter("address", "_l1Token", 1)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 3)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
        [Parameter("uint256", "", 5)]
        public virtual BigInteger ReturnValue5 { get; set; }
        [Parameter("bytes", "_data", 6)]
        public virtual byte[] Data { get; set; }
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

    public partial class DepositFinalizedEventDTO : DepositFinalizedEventDTOBase { }

    [Event("DepositFinalized")]
    public class DepositFinalizedEventDTOBase : IEventDTO
    {
        [Parameter("address", "l1Token", 1, true)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_from", 2, true)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 3, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_amount", 4, false)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class TxToL1EventDTO : TxToL1EventDTOBase { }

    [Event("TxToL1")]
    public class TxToL1EventDTOBase : IEventDTO
    {
        [Parameter("address", "_from", 1, true)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 2, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_id", 3, true)]
        public virtual BigInteger Id { get; set; }
        [Parameter("bytes", "_data", 4, false)]
        public virtual byte[] Data { get; set; }
    }

    public partial class WithdrawalInitiatedEventDTO : WithdrawalInitiatedEventDTOBase { }

    [Event("WithdrawalInitiated")]
    public class WithdrawalInitiatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "l1Token", 1, false)]
        public virtual string L1Token { get; set; }
        [Parameter("address", "_from", 2, true)]
        public virtual string From { get; set; }
        [Parameter("address", "_to", 3, true)]
        public virtual string To { get; set; }
        [Parameter("uint256", "_l2ToL1Id", 4, true)]
        public virtual BigInteger L2ToL1Id { get; set; }
        [Parameter("uint256", "_exitNum", 5, false)]
        public virtual BigInteger ExitNum { get; set; }
        [Parameter("uint256", "_amount", 6, false)]
        public virtual BigInteger Amount { get; set; }
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

    public partial class ExitNumOutputDTO : ExitNumOutputDTOBase { }

    [FunctionOutput]
    public class ExitNumOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetOutboundCalldataOutputDTO : GetOutboundCalldataOutputDTOBase { }

    [FunctionOutput]
    public class GetOutboundCalldataOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes", "outboundCalldata", 1)]
        public virtual byte[] OutboundCalldata { get; set; }
    }

    public partial class RouterOutputDTO : RouterOutputDTOBase { }

    [FunctionOutput]
    public class RouterOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
