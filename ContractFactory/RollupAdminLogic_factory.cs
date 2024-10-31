using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Arbitrum.ContractFactory.RollupAdminLogic
{
    public partial class RollupAdminLogicDeployment : RollupAdminLogicDeploymentBase
    {
        public RollupAdminLogicDeployment() : base(BYTECODE) { }
        public RollupAdminLogicDeployment(string byteCode) : base(byteCode) 
        { 
            BYTECODE = byteCode;
        }
    }

    public class RollupAdminLogicDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public RollupAdminLogicDeploymentBase() : base(BYTECODE) { }
        public RollupAdminLogicDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class StakerMapFunction : StakerMapFunctionBase { }

    [Function("_stakerMap", typeof(StakerMapOutputDTO))]
    public class StakerMapFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AmountStakedFunction : AmountStakedFunctionBase { }

    [Function("amountStaked", "uint256")]
    public class AmountStakedFunctionBase : FunctionMessage
    {
        [Parameter("address", "staker", 1)]
        public virtual string Staker { get; set; }
    }

    public partial class AnyTrustFastConfirmerFunction : AnyTrustFastConfirmerFunctionBase { }

    [Function("anyTrustFastConfirmer", "address")]
    public class AnyTrustFastConfirmerFunctionBase : FunctionMessage
    {

    }

    public partial class BaseStakeFunction : BaseStakeFunctionBase { }

    [Function("baseStake", "uint256")]
    public class BaseStakeFunctionBase : FunctionMessage
    {

    }

    public partial class BridgeFunction : BridgeFunctionBase { }

    [Function("bridge", "address")]
    public class BridgeFunctionBase : FunctionMessage
    {

    }

    public partial class ChainIdFunction : ChainIdFunctionBase { }

    [Function("chainId", "uint256")]
    public class ChainIdFunctionBase : FunctionMessage
    {

    }

    public partial class ChallengeManagerFunction : ChallengeManagerFunctionBase { }

    [Function("challengeManager", "address")]
    public class ChallengeManagerFunctionBase : FunctionMessage
    {

    }

    public partial class ConfirmPeriodBlocksFunction : ConfirmPeriodBlocksFunctionBase { }

    [Function("confirmPeriodBlocks", "uint64")]
    public class ConfirmPeriodBlocksFunctionBase : FunctionMessage
    {

    }

    public partial class CreateNitroMigrationGenesisFunction : CreateNitroMigrationGenesisFunctionBase { }

    [Function("createNitroMigrationGenesis")]
    public class CreateNitroMigrationGenesisFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "assertion", 1)]
        public virtual Assertion Assertion { get; set; }
    }

    public partial class CurrentChallengeFunction : CurrentChallengeFunctionBase { }

    [Function("currentChallenge", "uint64")]
    public class CurrentChallengeFunctionBase : FunctionMessage
    {
        [Parameter("address", "staker", 1)]
        public virtual string Staker { get; set; }
    }

    public partial class ExtraChallengeTimeBlocksFunction : ExtraChallengeTimeBlocksFunctionBase { }

    [Function("extraChallengeTimeBlocks", "uint64")]
    public class ExtraChallengeTimeBlocksFunctionBase : FunctionMessage
    {

    }

    public partial class FirstUnresolvedNodeFunction : FirstUnresolvedNodeFunctionBase { }

    [Function("firstUnresolvedNode", "uint64")]
    public class FirstUnresolvedNodeFunctionBase : FunctionMessage
    {

    }

    public partial class ForceConfirmNodeFunction : ForceConfirmNodeFunctionBase { }

    [Function("forceConfirmNode")]
    public class ForceConfirmNodeFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "nodeNum", 1)]
        public virtual ulong NodeNum { get; set; }
        [Parameter("bytes32", "blockHash", 2)]
        public virtual byte[] BlockHash { get; set; }
        [Parameter("bytes32", "sendRoot", 3)]
        public virtual byte[] SendRoot { get; set; }
    }

    public partial class ForceCreateNodeFunction : ForceCreateNodeFunctionBase { }

    [Function("forceCreateNode")]
    public class ForceCreateNodeFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "prevNode", 1)]
        public virtual ulong PrevNode { get; set; }
        [Parameter("uint256", "prevNodeInboxMaxCount", 2)]
        public virtual BigInteger PrevNodeInboxMaxCount { get; set; }
        [Parameter("tuple", "assertion", 3)]
        public virtual Assertion Assertion { get; set; }
        [Parameter("bytes32", "expectedNodeHash", 4)]
        public virtual byte[] ExpectedNodeHash { get; set; }
    }

    public partial class ForceRefundStakerFunction : ForceRefundStakerFunctionBase { }

    [Function("forceRefundStaker")]
    public class ForceRefundStakerFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "staker", 1)]
        public virtual List<string> Staker { get; set; }
    }

    public partial class ForceResolveChallengeFunction : ForceResolveChallengeFunctionBase { }

    [Function("forceResolveChallenge")]
    public class ForceResolveChallengeFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "stakerA", 1)]
        public virtual List<string> StakerA { get; set; }
        [Parameter("address[]", "stakerB", 2)]
        public virtual List<string> StakerB { get; set; }
    }

    public partial class GetNodeFunction : GetNodeFunctionBase { }

    [Function("getNode", typeof(GetNodeOutputDTO))]
    public class GetNodeFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "nodeNum", 1)]
        public virtual ulong NodeNum { get; set; }
    }

    public partial class GetNodeCreationBlockForLogLookupFunction : GetNodeCreationBlockForLogLookupFunctionBase { }

    [Function("getNodeCreationBlockForLogLookup", "uint256")]
    public class GetNodeCreationBlockForLogLookupFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "nodeNum", 1)]
        public virtual ulong NodeNum { get; set; }
    }

    public partial class GetStakerFunction : GetStakerFunctionBase { }

    [Function("getStaker", typeof(GetStakerOutputDTO))]
    public class GetStakerFunctionBase : FunctionMessage
    {
        [Parameter("address", "staker", 1)]
        public virtual string Staker { get; set; }
    }

    public partial class GetStakerAddressFunction : GetStakerAddressFunctionBase { }

    [Function("getStakerAddress", "address")]
    public class GetStakerAddressFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "stakerNum", 1)]
        public virtual ulong StakerNum { get; set; }
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
        [Parameter("tuple", "config", 1)]
        public virtual Config Config { get; set; }
        [Parameter("tuple", "connectedContracts", 2)]
        public virtual ContractDependencies ConnectedContracts { get; set; }
    }

    public partial class IsStakedFunction : IsStakedFunctionBase { }

    [Function("isStaked", "bool")]
    public class IsStakedFunctionBase : FunctionMessage
    {
        [Parameter("address", "staker", 1)]
        public virtual string Staker { get; set; }
    }

    public partial class IsStakedOnLatestConfirmedFunction : IsStakedOnLatestConfirmedFunctionBase { }

    [Function("isStakedOnLatestConfirmed", "bool")]
    public class IsStakedOnLatestConfirmedFunctionBase : FunctionMessage
    {
        [Parameter("address", "staker", 1)]
        public virtual string Staker { get; set; }
    }

    public partial class IsValidatorFunction : IsValidatorFunctionBase { }

    [Function("isValidator", "bool")]
    public class IsValidatorFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsZombieFunction : IsZombieFunctionBase { }

    [Function("isZombie", "bool")]
    public class IsZombieFunctionBase : FunctionMessage
    {
        [Parameter("address", "staker", 1)]
        public virtual string Staker { get; set; }
    }

    public partial class LastStakeBlockFunction : LastStakeBlockFunctionBase { }

    [Function("lastStakeBlock", "uint64")]
    public class LastStakeBlockFunctionBase : FunctionMessage
    {

    }

    public partial class LatestConfirmedFunction : LatestConfirmedFunctionBase { }

    [Function("latestConfirmed", "uint64")]
    public class LatestConfirmedFunctionBase : FunctionMessage
    {

    }

    public partial class LatestNodeCreatedFunction : LatestNodeCreatedFunctionBase { }

    [Function("latestNodeCreated", "uint64")]
    public class LatestNodeCreatedFunctionBase : FunctionMessage
    {

    }

    public partial class LatestStakedNodeFunction : LatestStakedNodeFunctionBase { }

    [Function("latestStakedNode", "uint64")]
    public class LatestStakedNodeFunctionBase : FunctionMessage
    {
        [Parameter("address", "staker", 1)]
        public virtual string Staker { get; set; }
    }

    public partial class LoserStakeEscrowFunction : LoserStakeEscrowFunctionBase { }

    [Function("loserStakeEscrow", "address")]
    public class LoserStakeEscrowFunctionBase : FunctionMessage
    {

    }

    public partial class MinimumAssertionPeriodFunction : MinimumAssertionPeriodFunctionBase { }

    [Function("minimumAssertionPeriod", "uint256")]
    public class MinimumAssertionPeriodFunctionBase : FunctionMessage
    {

    }

    public partial class NodeHasStakerFunction : NodeHasStakerFunctionBase { }

    [Function("nodeHasStaker", "bool")]
    public class NodeHasStakerFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "nodeNum", 1)]
        public virtual ulong NodeNum { get; set; }
        [Parameter("address", "staker", 2)]
        public virtual string Staker { get; set; }
    }

    public partial class OutboxFunction : OutboxFunctionBase { }

    [Function("outbox", "address")]
    public class OutboxFunctionBase : FunctionMessage
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

    public partial class ProxiableUUIDFunction : ProxiableUUIDFunctionBase { }

    [Function("proxiableUUID", "bytes32")]
    public class ProxiableUUIDFunctionBase : FunctionMessage
    {

    }

    public partial class RemoveOldOutboxFunction : RemoveOldOutboxFunctionBase { }

    [Function("removeOldOutbox")]
    public class RemoveOldOutboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "_outbox", 1)]
        public virtual string Outbox { get; set; }
    }

    public partial class ResumeFunction : ResumeFunctionBase { }

    [Function("resume")]
    public class ResumeFunctionBase : FunctionMessage
    {

    }

    public partial class RollupDeploymentBlockFunction : RollupDeploymentBlockFunctionBase { }

    [Function("rollupDeploymentBlock", "uint256")]
    public class RollupDeploymentBlockFunctionBase : FunctionMessage
    {

    }

    public partial class RollupEventInboxFunction : RollupEventInboxFunctionBase { }

    [Function("rollupEventInbox", "address")]
    public class RollupEventInboxFunctionBase : FunctionMessage
    {

    }

    public partial class SequencerInboxFunction : SequencerInboxFunctionBase { }

    [Function("sequencerInbox", "address")]
    public class SequencerInboxFunctionBase : FunctionMessage
    {

    }

    public partial class SetAnyTrustFastConfirmerFunction : SetAnyTrustFastConfirmerFunctionBase { }

    [Function("setAnyTrustFastConfirmer")]
    public class SetAnyTrustFastConfirmerFunctionBase : FunctionMessage
    {
        [Parameter("address", "_anyTrustFastConfirmer", 1)]
        public virtual string AnyTrustFastConfirmer { get; set; }
    }

    public partial class SetBaseStakeFunction : SetBaseStakeFunctionBase { }

    [Function("setBaseStake")]
    public class SetBaseStakeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "newBaseStake", 1)]
        public virtual BigInteger NewBaseStake { get; set; }
    }

    public partial class SetConfirmPeriodBlocksFunction : SetConfirmPeriodBlocksFunctionBase { }

    [Function("setConfirmPeriodBlocks")]
    public class SetConfirmPeriodBlocksFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "newConfirmPeriod", 1)]
        public virtual ulong NewConfirmPeriod { get; set; }
    }

    public partial class SetDelayedInboxFunction : SetDelayedInboxFunctionBase { }

    [Function("setDelayedInbox")]
    public class SetDelayedInboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "_inbox", 1)]
        public virtual string Inbox { get; set; }
        [Parameter("bool", "_enabled", 2)]
        public virtual bool Enabled { get; set; }
    }

    public partial class SetExtraChallengeTimeBlocksFunction : SetExtraChallengeTimeBlocksFunctionBase { }

    [Function("setExtraChallengeTimeBlocks")]
    public class SetExtraChallengeTimeBlocksFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "newExtraTimeBlocks", 1)]
        public virtual ulong NewExtraTimeBlocks { get; set; }
    }

    public partial class SetInboxFunction : SetInboxFunctionBase { }

    [Function("setInbox")]
    public class SetInboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "newInbox", 1)]
        public virtual string NewInbox { get; set; }
    }

    public partial class SetLoserStakeEscrowFunction : SetLoserStakeEscrowFunctionBase { }

    [Function("setLoserStakeEscrow")]
    public class SetLoserStakeEscrowFunctionBase : FunctionMessage
    {
        [Parameter("address", "newLoserStakerEscrow", 1)]
        public virtual string NewLoserStakerEscrow { get; set; }
    }

    public partial class SetMinimumAssertionPeriodFunction : SetMinimumAssertionPeriodFunctionBase { }

    [Function("setMinimumAssertionPeriod")]
    public class SetMinimumAssertionPeriodFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "newPeriod", 1)]
        public virtual BigInteger NewPeriod { get; set; }
    }

    public partial class SetOutboxFunction : SetOutboxFunctionBase { }

    [Function("setOutbox")]
    public class SetOutboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "_outbox", 1)]
        public virtual string Outbox { get; set; }
    }

    public partial class SetOwnerFunction : SetOwnerFunctionBase { }

    [Function("setOwner")]
    public class SetOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class SetSequencerInboxFunction : SetSequencerInboxFunctionBase { }

    [Function("setSequencerInbox")]
    public class SetSequencerInboxFunctionBase : FunctionMessage
    {
        [Parameter("address", "_sequencerInbox", 1)]
        public virtual string SequencerInbox { get; set; }
    }

    public partial class SetStakeTokenFunction : SetStakeTokenFunctionBase { }

    [Function("setStakeToken")]
    public class SetStakeTokenFunctionBase : FunctionMessage
    {
        [Parameter("address", "newStakeToken", 1)]
        public virtual string NewStakeToken { get; set; }
    }

    public partial class SetValidatorFunction : SetValidatorFunctionBase { }

    [Function("setValidator")]
    public class SetValidatorFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "_validator", 1)]
        public virtual List<string> Validator { get; set; }
        [Parameter("bool[]", "_val", 2)]
        public virtual List<bool> Val { get; set; }
    }

    public partial class SetValidatorWhitelistDisabledFunction : SetValidatorWhitelistDisabledFunctionBase { }

    [Function("setValidatorWhitelistDisabled")]
    public class SetValidatorWhitelistDisabledFunctionBase : FunctionMessage
    {
        [Parameter("bool", "_validatorWhitelistDisabled", 1)]
        public virtual bool ValidatorWhitelistDisabled { get; set; }
    }

    public partial class SetWasmModuleRootFunction : SetWasmModuleRootFunctionBase { }

    [Function("setWasmModuleRoot")]
    public class SetWasmModuleRootFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "newWasmModuleRoot", 1)]
        public virtual byte[] NewWasmModuleRoot { get; set; }
    }

    public partial class StakeTokenFunction : StakeTokenFunctionBase { }

    [Function("stakeToken", "address")]
    public class StakeTokenFunctionBase : FunctionMessage
    {

    }

    public partial class StakerCountFunction : StakerCountFunctionBase { }

    [Function("stakerCount", "uint64")]
    public class StakerCountFunctionBase : FunctionMessage
    {

    }

    public partial class TotalWithdrawableFundsFunction : TotalWithdrawableFundsFunctionBase { }

    [Function("totalWithdrawableFunds", "uint256")]
    public class TotalWithdrawableFundsFunctionBase : FunctionMessage
    {

    }

    public partial class UpgradeBeaconFunction : UpgradeBeaconFunctionBase { }

    [Function("upgradeBeacon")]
    public class UpgradeBeaconFunctionBase : FunctionMessage
    {
        [Parameter("address", "beacon", 1)]
        public virtual string Beacon { get; set; }
        [Parameter("address", "newImplementation", 2)]
        public virtual string NewImplementation { get; set; }
    }

    public partial class UpgradeSecondaryToFunction : UpgradeSecondaryToFunctionBase { }

    [Function("upgradeSecondaryTo")]
    public class UpgradeSecondaryToFunctionBase : FunctionMessage
    {
        [Parameter("address", "newImplementation", 1)]
        public virtual string NewImplementation { get; set; }
    }

    public partial class UpgradeSecondaryToAndCallFunction : UpgradeSecondaryToAndCallFunctionBase { }

    [Function("upgradeSecondaryToAndCall")]
    public class UpgradeSecondaryToAndCallFunctionBase : FunctionMessage
    {
        [Parameter("address", "newImplementation", 1)]
        public virtual string NewImplementation { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }

    public partial class UpgradeToFunction : UpgradeToFunctionBase { }

    [Function("upgradeTo")]
    public class UpgradeToFunctionBase : FunctionMessage
    {
        [Parameter("address", "newImplementation", 1)]
        public virtual string NewImplementation { get; set; }
    }

    public partial class UpgradeToAndCallFunction : UpgradeToAndCallFunctionBase { }

    [Function("upgradeToAndCall")]
    public class UpgradeToAndCallFunctionBase : FunctionMessage
    {
        [Parameter("address", "newImplementation", 1)]
        public virtual string NewImplementation { get; set; }
        [Parameter("bytes", "data", 2)]
        public virtual byte[] Data { get; set; }
    }

    public partial class ValidatorUtilsFunction : ValidatorUtilsFunctionBase { }

    [Function("validatorUtils", "address")]
    public class ValidatorUtilsFunctionBase : FunctionMessage
    {

    }

    public partial class ValidatorWalletCreatorFunction : ValidatorWalletCreatorFunctionBase { }

    [Function("validatorWalletCreator", "address")]
    public class ValidatorWalletCreatorFunctionBase : FunctionMessage
    {

    }

    public partial class ValidatorWhitelistDisabledFunction : ValidatorWhitelistDisabledFunctionBase { }

    [Function("validatorWhitelistDisabled", "bool")]
    public class ValidatorWhitelistDisabledFunctionBase : FunctionMessage
    {

    }

    public partial class WasmModuleRootFunction : WasmModuleRootFunctionBase { }

    [Function("wasmModuleRoot", "bytes32")]
    public class WasmModuleRootFunctionBase : FunctionMessage
    {

    }

    public partial class WithdrawableFundsFunction : WithdrawableFundsFunctionBase { }

    [Function("withdrawableFunds", "uint256")]
    public class WithdrawableFundsFunctionBase : FunctionMessage
    {
        [Parameter("address", "user", 1)]
        public virtual string User { get; set; }
    }

    public partial class ZombieAddressFunction : ZombieAddressFunctionBase { }

    [Function("zombieAddress", "address")]
    public class ZombieAddressFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "zombieNum", 1)]
        public virtual BigInteger ZombieNum { get; set; }
    }

    public partial class ZombieCountFunction : ZombieCountFunctionBase { }

    [Function("zombieCount", "uint256")]
    public class ZombieCountFunctionBase : FunctionMessage
    {

    }

    public partial class ZombieLatestStakedNodeFunction : ZombieLatestStakedNodeFunctionBase { }

    [Function("zombieLatestStakedNode", "uint64")]
    public class ZombieLatestStakedNodeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "zombieNum", 1)]
        public virtual BigInteger ZombieNum { get; set; }
    }

    public partial class AdminChangedEventDTO : AdminChangedEventDTOBase { }

    [Event("AdminChanged")]
    public class AdminChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "previousAdmin", 1, false)]
        public virtual string PreviousAdmin { get; set; }
        [Parameter("address", "newAdmin", 2, false)]
        public virtual string NewAdmin { get; set; }
    }

    public partial class BeaconUpgradedEventDTO : BeaconUpgradedEventDTOBase { }

    [Event("BeaconUpgraded")]
    public class BeaconUpgradedEventDTOBase : IEventDTO
    {
        [Parameter("address", "beacon", 1, true)]
        public virtual string Beacon { get; set; }
    }

    public partial class NodeConfirmedEventDTO : NodeConfirmedEventDTOBase { }

    [Event("NodeConfirmed")]
    public class NodeConfirmedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "nodeNum", 1, true)]
        public virtual ulong NodeNum { get; set; }
        [Parameter("bytes32", "blockHash", 2, false)]
        public virtual byte[] BlockHash { get; set; }
        [Parameter("bytes32", "sendRoot", 3, false)]
        public virtual byte[] SendRoot { get; set; }
    }

    public partial class NodeCreatedEventDTO : NodeCreatedEventDTOBase { }

    [Event("NodeCreated")]
    public class NodeCreatedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "nodeNum", 1, true)]
        public virtual ulong NodeNum { get; set; }
        [Parameter("bytes32", "parentNodeHash", 2, true)]
        public virtual byte[] ParentNodeHash { get; set; }
        [Parameter("bytes32", "nodeHash", 3, true)]
        public virtual byte[] NodeHash { get; set; }
        [Parameter("bytes32", "executionHash", 4, false)]
        public virtual byte[] ExecutionHash { get; set; }
        [Parameter("tuple", "assertion", 5, false)]
        public virtual Assertion Assertion { get; set; }
        [Parameter("bytes32", "afterInboxBatchAcc", 6, false)]
        public virtual byte[] AfterInboxBatchAcc { get; set; }
        [Parameter("bytes32", "wasmModuleRoot", 7, false)]
        public virtual byte[] WasmModuleRoot { get; set; }
        [Parameter("uint256", "inboxMaxCount", 8, false)]
        public virtual BigInteger InboxMaxCount { get; set; }
    }

    public partial class NodeRejectedEventDTO : NodeRejectedEventDTOBase { }

    [Event("NodeRejected")]
    public class NodeRejectedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "nodeNum", 1, true)]
        public virtual ulong NodeNum { get; set; }
    }

    public partial class OwnerFunctionCalledEventDTO : OwnerFunctionCalledEventDTOBase { }

    [Event("OwnerFunctionCalled")]
    public class OwnerFunctionCalledEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "id", 1, true)]
        public virtual BigInteger Id { get; set; }
    }

    public partial class PausedEventDTO : PausedEventDTOBase { }

    [Event("Paused")]
    public class PausedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, false)]
        public virtual string Account { get; set; }
    }

    public partial class RollupChallengeStartedEventDTO : RollupChallengeStartedEventDTOBase { }

    [Event("RollupChallengeStarted")]
    public class RollupChallengeStartedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "challengeIndex", 1, true)]
        public virtual ulong ChallengeIndex { get; set; }
        [Parameter("address", "asserter", 2, false)]
        public virtual string Asserter { get; set; }
        [Parameter("address", "challenger", 3, false)]
        public virtual string Challenger { get; set; }
        [Parameter("uint64", "challengedNode", 4, false)]
        public virtual ulong ChallengedNode { get; set; }
    }

    public partial class RollupInitializedEventDTO : RollupInitializedEventDTOBase { }

    [Event("RollupInitialized")]
    public class RollupInitializedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "machineHash", 1, false)]
        public virtual byte[] MachineHash { get; set; }
        [Parameter("uint256", "chainId", 2, false)]
        public virtual BigInteger ChainId { get; set; }
    }

    public partial class UnpausedEventDTO : UnpausedEventDTOBase { }

    [Event("Unpaused")]
    public class UnpausedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, false)]
        public virtual string Account { get; set; }
    }

    public partial class UpgradedEventDTO : UpgradedEventDTOBase { }

    [Event("Upgraded")]
    public class UpgradedEventDTOBase : IEventDTO
    {
        [Parameter("address", "implementation", 1, true)]
        public virtual string Implementation { get; set; }
    }

    public partial class UpgradedSecondaryEventDTO : UpgradedSecondaryEventDTOBase { }

    [Event("UpgradedSecondary")]
    public class UpgradedSecondaryEventDTOBase : IEventDTO
    {
        [Parameter("address", "implementation", 1, true)]
        public virtual string Implementation { get; set; }
    }

    public partial class UserStakeUpdatedEventDTO : UserStakeUpdatedEventDTOBase { }

    [Event("UserStakeUpdated")]
    public class UserStakeUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "user", 1, true)]
        public virtual string User { get; set; }
        [Parameter("uint256", "initialBalance", 2, false)]
        public virtual BigInteger InitialBalance { get; set; }
        [Parameter("uint256", "finalBalance", 3, false)]
        public virtual BigInteger FinalBalance { get; set; }
    }

    public partial class UserWithdrawableFundsUpdatedEventDTO : UserWithdrawableFundsUpdatedEventDTOBase { }

    [Event("UserWithdrawableFundsUpdated")]
    public class UserWithdrawableFundsUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "user", 1, true)]
        public virtual string User { get; set; }
        [Parameter("uint256", "initialBalance", 2, false)]
        public virtual BigInteger InitialBalance { get; set; }
        [Parameter("uint256", "finalBalance", 3, false)]
        public virtual BigInteger FinalBalance { get; set; }
    }

    public partial class StakerMapOutputDTO : StakerMapOutputDTOBase { }

    [FunctionOutput]
    public class StakerMapOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "amountStaked", 1)]
        public virtual BigInteger AmountStaked { get; set; }
        [Parameter("uint64", "index", 2)]
        public virtual ulong Index { get; set; }
        [Parameter("uint64", "latestStakedNode", 3)]
        public virtual ulong LatestStakedNode { get; set; }
        [Parameter("uint64", "currentChallenge", 4)]
        public virtual ulong CurrentChallenge { get; set; }
        [Parameter("bool", "isStaked", 5)]
        public virtual bool IsStaked { get; set; }
    }

    public partial class AmountStakedOutputDTO : AmountStakedOutputDTOBase { }

    [FunctionOutput]
    public class AmountStakedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AnyTrustFastConfirmerOutputDTO : AnyTrustFastConfirmerOutputDTOBase { }

    [FunctionOutput]
    public class AnyTrustFastConfirmerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class BaseStakeOutputDTO : BaseStakeOutputDTOBase { }

    [FunctionOutput]
    public class BaseStakeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class BridgeOutputDTO : BridgeOutputDTOBase { }

    [FunctionOutput]
    public class BridgeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ChainIdOutputDTO : ChainIdOutputDTOBase { }

    [FunctionOutput]
    public class ChainIdOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ChallengeManagerOutputDTO : ChallengeManagerOutputDTOBase { }

    [FunctionOutput]
    public class ChallengeManagerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ConfirmPeriodBlocksOutputDTO : ConfirmPeriodBlocksOutputDTOBase { }

    [FunctionOutput]
    public class ConfirmPeriodBlocksOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }



    public partial class CurrentChallengeOutputDTO : CurrentChallengeOutputDTOBase { }

    [FunctionOutput]
    public class CurrentChallengeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class ExtraChallengeTimeBlocksOutputDTO : ExtraChallengeTimeBlocksOutputDTOBase { }

    [FunctionOutput]
    public class ExtraChallengeTimeBlocksOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class FirstUnresolvedNodeOutputDTO : FirstUnresolvedNodeOutputDTOBase { }

    [FunctionOutput]
    public class FirstUnresolvedNodeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }









    public partial class GetNodeOutputDTO : GetNodeOutputDTOBase { }

    [FunctionOutput]
    public class GetNodeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("tuple", "", 1)]
        public virtual Node ReturnValue1 { get; set; }
    }

    public partial class GetNodeCreationBlockForLogLookupOutputDTO : GetNodeCreationBlockForLogLookupOutputDTOBase { }

    [FunctionOutput]
    public class GetNodeCreationBlockForLogLookupOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetStakerOutputDTO : GetStakerOutputDTOBase { }

    [FunctionOutput]
    public class GetStakerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("tuple", "", 1)]
        public virtual Staker ReturnValue1 { get; set; }
    }

    public partial class GetStakerAddressOutputDTO : GetStakerAddressOutputDTOBase { }

    [FunctionOutput]
    public class GetStakerAddressOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class InboxOutputDTO : InboxOutputDTOBase { }

    [FunctionOutput]
    public class InboxOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class IsStakedOutputDTO : IsStakedOutputDTOBase { }

    [FunctionOutput]
    public class IsStakedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsStakedOnLatestConfirmedOutputDTO : IsStakedOnLatestConfirmedOutputDTOBase { }

    [FunctionOutput]
    public class IsStakedOnLatestConfirmedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsValidatorOutputDTO : IsValidatorOutputDTOBase { }

    [FunctionOutput]
    public class IsValidatorOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsZombieOutputDTO : IsZombieOutputDTOBase { }

    [FunctionOutput]
    public class IsZombieOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class LastStakeBlockOutputDTO : LastStakeBlockOutputDTOBase { }

    [FunctionOutput]
    public class LastStakeBlockOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class LatestConfirmedOutputDTO : LatestConfirmedOutputDTOBase { }

    [FunctionOutput]
    public class LatestConfirmedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class LatestNodeCreatedOutputDTO : LatestNodeCreatedOutputDTOBase { }

    [FunctionOutput]
    public class LatestNodeCreatedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class LatestStakedNodeOutputDTO : LatestStakedNodeOutputDTOBase { }

    [FunctionOutput]
    public class LatestStakedNodeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class LoserStakeEscrowOutputDTO : LoserStakeEscrowOutputDTOBase { }

    [FunctionOutput]
    public class LoserStakeEscrowOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class MinimumAssertionPeriodOutputDTO : MinimumAssertionPeriodOutputDTOBase { }

    [FunctionOutput]
    public class MinimumAssertionPeriodOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class NodeHasStakerOutputDTO : NodeHasStakerOutputDTOBase { }

    [FunctionOutput]
    public class NodeHasStakerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class OutboxOutputDTO : OutboxOutputDTOBase { }

    [FunctionOutput]
    public class OutboxOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class PausedOutputDTO : PausedOutputDTOBase { }

    [FunctionOutput]
    public class PausedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ProxiableUUIDOutputDTO : ProxiableUUIDOutputDTOBase { }

    [FunctionOutput]
    public class ProxiableUUIDOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }





    public partial class RollupDeploymentBlockOutputDTO : RollupDeploymentBlockOutputDTOBase { }

    [FunctionOutput]
    public class RollupDeploymentBlockOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class RollupEventInboxOutputDTO : RollupEventInboxOutputDTOBase { }

    [FunctionOutput]
    public class RollupEventInboxOutputDTOBase : IFunctionOutputDTO
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































    public partial class StakeTokenOutputDTO : StakeTokenOutputDTOBase { }

    [FunctionOutput]
    public class StakeTokenOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class StakerCountOutputDTO : StakerCountOutputDTOBase { }

    [FunctionOutput]
    public class StakerCountOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class TotalWithdrawableFundsOutputDTO : TotalWithdrawableFundsOutputDTOBase { }

    [FunctionOutput]
    public class TotalWithdrawableFundsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }











    public partial class ValidatorUtilsOutputDTO : ValidatorUtilsOutputDTOBase { }

    [FunctionOutput]
    public class ValidatorUtilsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ValidatorWalletCreatorOutputDTO : ValidatorWalletCreatorOutputDTOBase { }

    [FunctionOutput]
    public class ValidatorWalletCreatorOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ValidatorWhitelistDisabledOutputDTO : ValidatorWhitelistDisabledOutputDTOBase { }

    [FunctionOutput]
    public class ValidatorWhitelistDisabledOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class WasmModuleRootOutputDTO : WasmModuleRootOutputDTOBase { }

    [FunctionOutput]
    public class WasmModuleRootOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class WithdrawableFundsOutputDTO : WithdrawableFundsOutputDTOBase { }

    [FunctionOutput]
    public class WithdrawableFundsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ZombieAddressOutputDTO : ZombieAddressOutputDTOBase { }

    [FunctionOutput]
    public class ZombieAddressOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ZombieCountOutputDTO : ZombieCountOutputDTOBase { }

    [FunctionOutput]
    public class ZombieCountOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ZombieLatestStakedNodeOutputDTO : ZombieLatestStakedNodeOutputDTOBase { }

    [FunctionOutput]
    public class ZombieLatestStakedNodeOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class GlobalState : GlobalStateBase { }

    public class GlobalStateBase
    {
        [Parameter("bytes32[2]", "bytes32Vals", 1)]
        public virtual List<byte[]> Bytes32Vals { get; set; }
        [Parameter("uint64[2]", "u64Vals", 2)]
        public virtual List<ulong> U64Vals { get; set; }
    }

    public partial class ExecutionState : ExecutionStateBase { }

    public class ExecutionStateBase
    {
        [Parameter("tuple", "globalState", 1)]
        public virtual GlobalState GlobalState { get; set; }
        [Parameter("uint8", "machineStatus", 2)]
        public virtual byte MachineStatus { get; set; }
    }

    public partial class Assertion : AssertionBase { }

    public class AssertionBase
    {
        [Parameter("tuple", "beforeState", 1)]
        public virtual ExecutionState BeforeState { get; set; }
        [Parameter("tuple", "afterState", 2)]
        public virtual ExecutionState AfterState { get; set; }
        [Parameter("uint64", "numBlocks", 3)]
        public virtual ulong NumBlocks { get; set; }
    }

    public partial class Node : NodeBase { }

    public class NodeBase
    {
        [Parameter("bytes32", "stateHash", 1)]
        public virtual byte[] StateHash { get; set; }
        [Parameter("bytes32", "challengeHash", 2)]
        public virtual byte[] ChallengeHash { get; set; }
        [Parameter("bytes32", "confirmData", 3)]
        public virtual byte[] ConfirmData { get; set; }
        [Parameter("uint64", "prevNum", 4)]
        public virtual ulong PrevNum { get; set; }
        [Parameter("uint64", "deadlineBlock", 5)]
        public virtual ulong DeadlineBlock { get; set; }
        [Parameter("uint64", "noChildConfirmedBeforeBlock", 6)]
        public virtual ulong NoChildConfirmedBeforeBlock { get; set; }
        [Parameter("uint64", "stakerCount", 7)]
        public virtual ulong StakerCount { get; set; }
        [Parameter("uint64", "childStakerCount", 8)]
        public virtual ulong ChildStakerCount { get; set; }
        [Parameter("uint64", "firstChildBlock", 9)]
        public virtual ulong FirstChildBlock { get; set; }
        [Parameter("uint64", "latestChildNumber", 10)]
        public virtual ulong LatestChildNumber { get; set; }
        [Parameter("uint64", "createdAtBlock", 11)]
        public virtual ulong CreatedAtBlock { get; set; }
        [Parameter("bytes32", "nodeHash", 12)]
        public virtual byte[] NodeHash { get; set; }
    }

    public partial class Staker : StakerBase { }

    public class StakerBase
    {
        [Parameter("uint256", "amountStaked", 1)]
        public virtual BigInteger AmountStaked { get; set; }
        [Parameter("uint64", "index", 2)]
        public virtual ulong Index { get; set; }
        [Parameter("uint64", "latestStakedNode", 3)]
        public virtual ulong LatestStakedNode { get; set; }
        [Parameter("uint64", "currentChallenge", 4)]
        public virtual ulong CurrentChallenge { get; set; }
        [Parameter("bool", "isStaked", 5)]
        public virtual bool IsStaked { get; set; }
    }

    public partial class MaxTimeVariation : MaxTimeVariationBase { }

    public class MaxTimeVariationBase
    {
        [Parameter("uint256", "delayBlocks", 1)]
        public virtual BigInteger DelayBlocks { get; set; }
        [Parameter("uint256", "futureBlocks", 2)]
        public virtual BigInteger FutureBlocks { get; set; }
        [Parameter("uint256", "delaySeconds", 3)]
        public virtual BigInteger DelaySeconds { get; set; }
        [Parameter("uint256", "futureSeconds", 4)]
        public virtual BigInteger FutureSeconds { get; set; }
    }

    public partial class Config : ConfigBase { }

    public class ConfigBase
    {
        [Parameter("uint64", "confirmPeriodBlocks", 1)]
        public virtual ulong ConfirmPeriodBlocks { get; set; }
        [Parameter("uint64", "extraChallengeTimeBlocks", 2)]
        public virtual ulong ExtraChallengeTimeBlocks { get; set; }
        [Parameter("address", "stakeToken", 3)]
        public virtual string StakeToken { get; set; }
        [Parameter("uint256", "baseStake", 4)]
        public virtual BigInteger BaseStake { get; set; }
        [Parameter("bytes32", "wasmModuleRoot", 5)]
        public virtual byte[] WasmModuleRoot { get; set; }
        [Parameter("address", "owner", 6)]
        public virtual string Owner { get; set; }
        [Parameter("address", "loserStakeEscrow", 7)]
        public virtual string LoserStakeEscrow { get; set; }
        [Parameter("uint256", "chainId", 8)]
        public virtual BigInteger ChainId { get; set; }
        [Parameter("string", "chainConfig", 9)]
        public virtual string ChainConfig { get; set; }
        [Parameter("uint64", "genesisBlockNum", 10)]
        public virtual ulong GenesisBlockNum { get; set; }
        [Parameter("tuple", "sequencerInboxMaxTimeVariation", 11)]
        public virtual MaxTimeVariation SequencerInboxMaxTimeVariation { get; set; }
    }

    public partial class ContractDependencies : ContractDependenciesBase { }

    public class ContractDependenciesBase
    {
        [Parameter("address", "bridge", 1)]
        public virtual string Bridge { get; set; }
        [Parameter("address", "sequencerInbox", 2)]
        public virtual string SequencerInbox { get; set; }
        [Parameter("address", "inbox", 3)]
        public virtual string Inbox { get; set; }
        [Parameter("address", "outbox", 4)]
        public virtual string Outbox { get; set; }
        [Parameter("address", "rollupEventInbox", 5)]
        public virtual string RollupEventInbox { get; set; }
        [Parameter("address", "challengeManager", 6)]
        public virtual string ChallengeManager { get; set; }
        [Parameter("address", "rollupAdminLogic", 7)]
        public virtual string RollupAdminLogic { get; set; }
        [Parameter("address", "rollupUserLogic", 8)]
        public virtual string RollupUserLogic { get; set; }
        [Parameter("address", "validatorUtils", 9)]
        public virtual string ValidatorUtils { get; set; }
        [Parameter("address", "validatorWalletCreator", 10)]
        public virtual string ValidatorWalletCreator { get; set; }
    }
}
