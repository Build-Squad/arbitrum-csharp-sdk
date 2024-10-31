using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory
{
    public partial class ArbSysDeployment : ArbSysDeploymentBase
    {
        public ArbSysDeployment() : base(BYTECODE) { }
        public ArbSysDeployment(string byteCode) : base(byteCode) { }
    }

    public class ArbSysDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public ArbSysDeploymentBase() : base(BYTECODE) { }
        public ArbSysDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ArbBlockHashFunction : ArbBlockHashFunctionBase { }

    [Function("arbBlockHash", "bytes32")]
    public class ArbBlockHashFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "arbBlockNum", 1)]
        public virtual BigInteger ArbBlockNum { get; set; }
    }

    public partial class ArbBlockNumberFunction : ArbBlockNumberFunctionBase { }

    [Function("arbBlockNumber", "uint256")]
    public class ArbBlockNumberFunctionBase : FunctionMessage
    {

    }

    public partial class ArbChainIDFunction : ArbChainIDFunctionBase { }

    [Function("arbChainID", "uint256")]
    public class ArbChainIDFunctionBase : FunctionMessage
    {

    }

    public partial class ArbOSVersionFunction : ArbOSVersionFunctionBase { }

    [Function("arbOSVersion", "uint256")]
    public class ArbOSVersionFunctionBase : FunctionMessage
    {

    }

    public partial class GetStorageGasAvailableFunction : GetStorageGasAvailableFunctionBase { }

    [Function("getStorageGasAvailable", "uint256")]
    public class GetStorageGasAvailableFunctionBase : FunctionMessage
    {

    }

    public partial class IsTopLevelCallFunction : IsTopLevelCallFunctionBase { }

    [Function("isTopLevelCall", "bool")]
    public class IsTopLevelCallFunctionBase : FunctionMessage
    {

    }

    public partial class MapL1SenderContractAddressToL2AliasFunction : MapL1SenderContractAddressToL2AliasFunctionBase { }

    [Function("mapL1SenderContractAddressToL2Alias", "address")]
    public class MapL1SenderContractAddressToL2AliasFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("address", "unused", 2)]
        public virtual string Unused { get; set; }
    }

    public partial class MyCallersAddressWithoutAliasingFunction : MyCallersAddressWithoutAliasingFunctionBase { }

    [Function("myCallersAddressWithoutAliasing", "address")]
    public class MyCallersAddressWithoutAliasingFunctionBase : FunctionMessage
    {

    }

    public partial class SendMerkleTreeStateFunction : SendMerkleTreeStateFunctionBase { }

    [Function("sendMerkleTreeState", typeof(SendMerkleTreeStateOutputDTO))]
    public class SendMerkleTreeStateFunctionBase : FunctionMessage
    {

    }

    public partial class SendTxToL1Function : SendTxToL1FunctionBase { }

    [Function("sendTxToL1", "uint256")]
    public class SendTxToL1FunctionBase : FunctionMessage
    {
        [Parameter("address", "destination", 1)]
        public virtual string Destination { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }

    public partial class WasMyCallersAddressAliasedFunction : WasMyCallersAddressAliasedFunctionBase { }

    [Function("wasMyCallersAddressAliased", "bool")]
    public class WasMyCallersAddressAliasedFunctionBase : FunctionMessage
    {

    }

    public partial class WithdrawEthFunction : WithdrawEthFunctionBase { }

    [Function("withdrawEth", "uint256")]
    public class WithdrawEthFunctionBase : FunctionMessage
    {
        [Parameter("address", "destination", 1)]
        public virtual string Destination { get; set; }
    }

    public partial class L2ToL1TransactionEventDTO : L2ToL1TransactionEventDTOBase { }

    [Event("L2ToL1Transaction")]
    public class L2ToL1TransactionEventDTOBase : IEventDTO
    {
        [Parameter("address", "caller", 1, false)]
        public virtual string Caller { get; set; }
        [Parameter("address", "destination", 2, true)]
        public virtual string Destination { get; set; }
        [Parameter("uint256", "uniqueId", 3, true)]
        public virtual BigInteger UniqueId { get; set; }
        [Parameter("uint256", "batchNumber", 4, true)]
        public virtual BigInteger BatchNumber { get; set; }
        [Parameter("uint256", "indexInBatch", 5, false)]
        public virtual BigInteger IndexInBatch { get; set; }
        [Parameter("uint256", "arbBlockNum", 6, false)]
        public virtual BigInteger ArbBlockNum { get; set; }
        [Parameter("uint256", "ethBlockNum", 7, false)]
        public virtual BigInteger EthBlockNum { get; set; }
        [Parameter("uint256", "timestamp", 8, false)]
        public virtual BigInteger Timestamp { get; set; }
        [Parameter("uint256", "callvalue", 9, false)]
        public virtual BigInteger Callvalue { get; set; }
        [Parameter("bytes", "data", 10, false)]
        public virtual byte[] Data { get; set; }
    }

    public partial class L2ToL1TxEventDTO : L2ToL1TxEventDTOBase { }

    [Event("L2ToL1Tx")]
    public class L2ToL1TxEventDTOBase : IEventDTO
    {
        [Parameter("address", "caller", 1, false)]
        public virtual string Caller { get; set; }
        [Parameter("address", "destination", 2, true)]
        public virtual string Destination { get; set; }
        [Parameter("uint256", "hash", 3, true)]
        public virtual BigInteger Hash { get; set; }
        [Parameter("uint256", "position", 4, true)]
        public virtual BigInteger Position { get; set; }
        [Parameter("uint256", "arbBlockNum", 5, false)]
        public virtual BigInteger ArbBlockNum { get; set; }
        [Parameter("uint256", "ethBlockNum", 6, false)]
        public virtual BigInteger EthBlockNum { get; set; }
        [Parameter("uint256", "timestamp", 7, false)]
        public virtual BigInteger Timestamp { get; set; }
        [Parameter("uint256", "callvalue", 8, false)]
        public virtual BigInteger Callvalue { get; set; }
        [Parameter("bytes", "data", 9, false)]
        public virtual byte[] Data { get; set; }
    }

    public partial class SendMerkleUpdateEventDTO : SendMerkleUpdateEventDTOBase { }

    [Event("SendMerkleUpdate")]
    public class SendMerkleUpdateEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "reserved", 1, true)]
        public virtual BigInteger Reserved { get; set; }
        [Parameter("bytes32", "hash", 2, true)]
        public virtual byte[] Hash { get; set; }
        [Parameter("uint256", "position", 3, true)]
        public virtual BigInteger Position { get; set; }
    }

    public partial class ArbBlockHashOutputDTO : ArbBlockHashOutputDTOBase { }

    [FunctionOutput]
    public class ArbBlockHashOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class ArbBlockNumberOutputDTO : ArbBlockNumberOutputDTOBase { }

    [FunctionOutput]
    public class ArbBlockNumberOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ArbChainIDOutputDTO : ArbChainIDOutputDTOBase { }

    [FunctionOutput]
    public class ArbChainIDOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ArbOSVersionOutputDTO : ArbOSVersionOutputDTOBase { }

    [FunctionOutput]
    public class ArbOSVersionOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetStorageGasAvailableOutputDTO : GetStorageGasAvailableOutputDTOBase { }

    [FunctionOutput]
    public class GetStorageGasAvailableOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class IsTopLevelCallOutputDTO : IsTopLevelCallOutputDTOBase { }

    [FunctionOutput]
    public class IsTopLevelCallOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class MapL1SenderContractAddressToL2AliasOutputDTO : MapL1SenderContractAddressToL2AliasOutputDTOBase { }

    [FunctionOutput]
    public class MapL1SenderContractAddressToL2AliasOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class MyCallersAddressWithoutAliasingOutputDTO : MyCallersAddressWithoutAliasingOutputDTOBase { }

    [FunctionOutput]
    public class MyCallersAddressWithoutAliasingOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SendMerkleTreeStateOutputDTO : SendMerkleTreeStateOutputDTOBase { }

    [FunctionOutput]
    public class SendMerkleTreeStateOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "size", 1)]
        public virtual BigInteger Size { get; set; }
        [Parameter("bytes32", "root", 2)]
        public virtual byte[] Root { get; set; }
        [Parameter("bytes32[]", "partials", 3)]
        public virtual List<byte[]> Partials { get; set; }
    }

    public partial class WasMyCallersAddressAliasedOutputDTO : WasMyCallersAddressAliasedOutputDTOBase { }

    [FunctionOutput]
    public class WasMyCallersAddressAliasedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }
}