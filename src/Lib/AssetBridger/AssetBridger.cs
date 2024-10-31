using Arbitrum.DataEntities;
using Arbitrum.Message;

namespace Arbitrum.AssetBridgerModule
{
    public abstract class AssetBridger<DepositParams, WithdrawParams, TDepositReceipt>
    {
        public L1Network L1Network;
        public L2Network L2Network;
        public string? NativeToken;

        public AssetBridger(L2Network l2Network)
        {
            L2Network = l2Network;
            L1Network = NetworkUtils.l1Networks[l2Network.PartnerChainID];
            NativeToken = l2Network?.NativeToken;
            if (L1Network == null)
            {
                throw new ArbSdkError($"Unknown l1 network chain id: {l2Network?.PartnerChainID}");
            }
        }

        public async Task InitializeAsync()
        {
            L1Network = await NetworkUtils.GetL1Network(L2Network?.PartnerChainID);
            if (L1Network == null)
            {
                throw new ArbSdkError($"Unknown l1 network chain id: {L2Network?.PartnerChainID}");
            }
        }

        protected async Task CheckL1Network(dynamic sop)
        {
            await SignerProviderUtils.CheckNetworkMatches(sop, L2Network.PartnerChainID);
        }

        protected async Task CheckL2Network(dynamic sop)
        {
            await SignerProviderUtils.CheckNetworkMatches(sop, L1Network.PartnerChainIDs[0]);
        }

        protected bool NativeTokenIsEth => string.IsNullOrEmpty(NativeToken) || NativeToken == Constants.ADDRESS_ZERO;

        public abstract Task<TDepositReceipt> Deposit(dynamic parameters);

        public abstract Task<L2TransactionReceipt> Withdraw(dynamic parameters);

    }
}
