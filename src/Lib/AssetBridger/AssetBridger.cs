using Arbitrum.DataEntities;
using Arbitrum.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arbitrum.AssetBridgerModule
{
    public abstract class AssetBridger<DepositParams, WithdrawParams>
    {
        public L1Network L1Network;
        public L2Network L2Network;
        public string? NativeToken;

        public AssetBridger(L2Network l2Network)
        {
            L2Network = l2Network;
            NativeToken = l2Network.NativeToken;
            if (L1Network == null)
            {
                throw new ArbSdkError($"Unknown l1 network chain id: {l2Network.PartnerChainID}");
            }
        }

        public async Task InitializeAsync()
        {
            L1Network = await NetworkUtils.GetL1NetworkAsync(L2Network.PartnerChainID);
            if (L1Network == null)
            {
                throw new ArbSdkError($"Unknown l1 network chain id: {L2Network.PartnerChainID}");
            }
        }

        protected async Task CheckL1Network(SignerOrProvider sop)
        {
            await SignerProviderUtils.CheckNetworkMatches(sop, L1Network.ChainID);
        }

        protected async Task CheckL2Network(SignerOrProvider sop)
        {
            await SignerProviderUtils.CheckNetworkMatches(sop, L2Network.ChainID);
        }

        protected bool NativeTokenIsEth => string.IsNullOrEmpty(NativeToken) || NativeToken == Constants.ADDRESS_ZERO;

        public abstract Task<L1ContractTransaction> Deposit(DepositParams parameters);

        public abstract Task<L2ContractTransaction> Withdraw(WithdrawParams parameters);
    }
}
