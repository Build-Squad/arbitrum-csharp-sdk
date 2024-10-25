using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory
{
    public partial class NodeInterfaceDeployment : NodeInterfaceDeploymentBase
    {
        public NodeInterfaceDeployment() : base(BYTECODE) { }
        public NodeInterfaceDeployment(string byteCode) : base(byteCode) 
        {
            BYTECODE = byteCode;
        }
    }

    public class NodeInterfaceDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE;
        public NodeInterfaceDeploymentBase() : base(BYTECODE) { }
        public NodeInterfaceDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class BlockL1NumFunction : BlockL1NumFunctionBase { }

    [Function("blockL1Num", "uint64")]
    public class BlockL1NumFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "l2BlockNum", 1)]
        public virtual ulong L2BlockNum { get; set; }
    }

    public partial class ConstructOutboxProofFunction : ConstructOutboxProofFunctionBase { }

    [Function("constructOutboxProof", typeof(ConstructOutboxProofOutputDTO))]
    public class ConstructOutboxProofFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "size", 1)]
        public virtual ulong Size { get; set; }
        [Parameter("uint64", "leaf", 2)]
        public virtual ulong Leaf { get; set; }
    }

    public partial class EstimateRetryableTicketFunction : EstimateRetryableTicketFunctionBase { }

    [Function("estimateRetryableTicket")]
    public class EstimateRetryableTicketFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "deposit", 2)]
        public virtual BigInteger Deposit { get; set; }
        [Parameter("address", "to", 3)]
        public virtual string To { get; set; }
        [Parameter("uint256", "l2CallValue", 4)]
        public virtual BigInteger L2CallValue { get; set; }
        [Parameter("address", "excessFeeRefundAddress", 5)]
        public virtual string ExcessFeeRefundAddress { get; set; }
        [Parameter("address", "callValueRefundAddress", 6)]
        public virtual string CallValueRefundAddress { get; set; }
        [Parameter("bytes", "data", 7)]
        public virtual byte[] Data { get; set; }
    }

    public partial class FindBatchContainingBlockFunction : FindBatchContainingBlockFunctionBase { }

    [Function("findBatchContainingBlock", "uint64")]
    public class FindBatchContainingBlockFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "blockNum", 1)]
        public virtual ulong BlockNum { get; set; }
    }

    public partial class GasEstimateComponentsFunction : GasEstimateComponentsFunctionBase { }

    [Function("gasEstimateComponents", typeof(GasEstimateComponentsOutputDTO))]
    public class GasEstimateComponentsFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("bool", "contractCreation", 2)]
        public virtual bool ContractCreation { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class GasEstimateL1ComponentFunction : GasEstimateL1ComponentFunctionBase { }

    [Function("gasEstimateL1Component", typeof(GasEstimateL1ComponentOutputDTO))]
    public class GasEstimateL1ComponentFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("bool", "contractCreation", 2)]
        public virtual bool ContractCreation { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
    }

    public partial class GetL1ConfirmationsFunction : GetL1ConfirmationsFunctionBase { }

    [Function("getL1Confirmations", "uint64")]
    public class GetL1ConfirmationsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "blockHash", 1)]
        public virtual byte[] BlockHash { get; set; }
    }

    public partial class L2BlockRangeForL1Function : L2BlockRangeForL1FunctionBase { }

    [Function("l2BlockRangeForL1", typeof(L2BlockRangeForL1OutputDTO))]
    public class L2BlockRangeForL1FunctionBase : FunctionMessage
    {
        [Parameter("uint64", "blockNum", 1)]
        public virtual ulong BlockNum { get; set; }
    }

    public partial class LegacyLookupMessageBatchProofFunction : LegacyLookupMessageBatchProofFunctionBase { }

    [Function("legacyLookupMessageBatchProof", typeof(LegacyLookupMessageBatchProofOutputDTO))]
    public class LegacyLookupMessageBatchProofFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "batchNum", 1)]
        public virtual BigInteger BatchNum { get; set; }
        [Parameter("uint64", "index", 2)]
        public virtual ulong Index { get; set; }
    }

    public partial class NitroGenesisBlockFunction : NitroGenesisBlockFunctionBase { }

    [Function("nitroGenesisBlock", "uint256")]
    public class NitroGenesisBlockFunctionBase : FunctionMessage
    {

    }

    public partial class BlockL1NumOutputDTO : BlockL1NumOutputDTOBase { }

    [FunctionOutput]
    public class BlockL1NumOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "l1BlockNum", 1)]
        public virtual ulong L1BlockNum { get; set; }
    }

    public partial class ConstructOutboxProofOutputDTO : ConstructOutboxProofOutputDTOBase { }

    [FunctionOutput]
    public class ConstructOutboxProofOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "send", 1)]
        public virtual byte[] Send { get; set; }
        [Parameter("bytes32", "root", 2)]
        public virtual byte[] Root { get; set; }
        [Parameter("bytes32[]", "proof", 3)]
        public virtual List<byte[]> Proof { get; set; }
    }



    public partial class FindBatchContainingBlockOutputDTO : FindBatchContainingBlockOutputDTOBase { }

    [FunctionOutput]
    public class FindBatchContainingBlockOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "batch", 1)]
        public virtual ulong Batch { get; set; }
    }

    public partial class GasEstimateComponentsOutputDTO : GasEstimateComponentsOutputDTOBase { }

    [FunctionOutput]
    public class GasEstimateComponentsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "gasEstimate", 1)]
        public virtual ulong GasEstimate { get; set; }
        [Parameter("uint64", "gasEstimateForL1", 2)]
        public virtual ulong GasEstimateForL1 { get; set; }
        [Parameter("uint256", "baseFee", 3)]
        public virtual BigInteger BaseFee { get; set; }
        [Parameter("uint256", "l1BaseFeeEstimate", 4)]
        public virtual BigInteger L1BaseFeeEstimate { get; set; }
    }

    public partial class GasEstimateL1ComponentOutputDTO : GasEstimateL1ComponentOutputDTOBase { }

    [FunctionOutput]
    public class GasEstimateL1ComponentOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "gasEstimateForL1", 1)]
        public virtual ulong GasEstimateForL1 { get; set; }
        [Parameter("uint256", "baseFee", 2)]
        public virtual BigInteger BaseFee { get; set; }
        [Parameter("uint256", "l1BaseFeeEstimate", 3)]
        public virtual BigInteger L1BaseFeeEstimate { get; set; }
    }

    public partial class GetL1ConfirmationsOutputDTO : GetL1ConfirmationsOutputDTOBase { }

    [FunctionOutput]
    public class GetL1ConfirmationsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "confirmations", 1)]
        public virtual ulong Confirmations { get; set; }
    }

    public partial class L2BlockRangeForL1OutputDTO : L2BlockRangeForL1OutputDTOBase { }

    [FunctionOutput]
    public class L2BlockRangeForL1OutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "firstBlock", 1)]
        public virtual ulong FirstBlock { get; set; }
        [Parameter("uint64", "lastBlock", 2)]
        public virtual ulong LastBlock { get; set; }
    }

    public partial class LegacyLookupMessageBatchProofOutputDTO : LegacyLookupMessageBatchProofOutputDTOBase { }

    [FunctionOutput]
    public class LegacyLookupMessageBatchProofOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32[]", "proof", 1)]
        public virtual List<byte[]> Proof { get; set; }
        [Parameter("uint256", "path", 2)]
        public virtual BigInteger Path { get; set; }
        [Parameter("address", "l2Sender", 3)]
        public virtual string L2Sender { get; set; }
        [Parameter("address", "l1Dest", 4)]
        public virtual string L1Dest { get; set; }
        [Parameter("uint256", "l2Block", 5)]
        public virtual BigInteger L2Block { get; set; }
        [Parameter("uint256", "l1Block", 6)]
        public virtual BigInteger L1Block { get; set; }
        [Parameter("uint256", "timestamp", 7)]
        public virtual BigInteger Timestamp { get; set; }
        [Parameter("uint256", "amount", 8)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("bytes", "calldataForL1", 9)]
        public virtual byte[] CalldataForL1 { get; set; }
    }

    public partial class NitroGenesisBlockOutputDTO : NitroGenesisBlockOutputDTOBase { }

    [FunctionOutput]
    public class NitroGenesisBlockOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "number", 1)]
        public virtual BigInteger Number { get; set; }
    }
}
