using NBitcoin;

namespace Arbitrum.src.Lib.DataEntities
{
    public class RollupAdminLogic
    {
        public class Config
        {
            public int ChainId { get; set; }
            public int ConfirmPeriodBlocks { get; set; }
            public uint256 ExtraChallengeTimeBlocks { get; set; }
            public uint256 BaseStake { get; set; }
            public string StakeToken { get; set; }
            public string LoserStakeEscrow { get; set; }
        }

        public class ContractDependencies
        {
            public string Bridge { get; set; }
            public string Inbox { get; set; }
            public string SequencerInbox { get; set; }
            public string Outbox { get; set; }
            public string Rollup { get; set; }
        }
    }
}
