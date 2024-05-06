using System.Numerics;
using Arbitrum.DataEntities;
using NUnit.Framework;
using static Arbitrum.AssetBridger.Erc20Bridger;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.AssetBridger.Tests.Integration
{
    [TestFixture]
    public class CustomERC20Tests
    {
        private BigInteger depositAmount = 100;
        private BigInteger withdrawalAmount = 10;

        [Test]
        public async Task RegisterCustomToken()
        {
            var (l1Token, l2Token) = await RegisterCustomToken(setupState.L2Network, setupState.L1Signer, setupState.L2Signer, setupState.AdminErc20Bridger);
            setupState.L1CustomToken = l1Token;
        }

        [Test]
        public async Task Deposit()
        {
            var txHash = await setupState.L1CustomToken.Mint().SendTransactionAsync(setupState.L1Signer.Account.Address);

            await setupState.L1Signer.Provider.Eth.WaitTransactionReceiptAsync(txHash);

            await DepositToken(
                DEPOSIT_AMOUNT,
                setupState.L1CustomToken.Address,
                setupState.AdminErc20Bridger,
                setupState.L1Signer,
                setupState.L2Signer,
                L1ToL2MessageStatus.REDEEMED,
                GatewayType.CUSTOM
            );
        }

        [Test]
        public async Task WithdrawToken()
        {
            await WithdrawToken(
                setupState,
                setupState.AdminErc20Bridger,
                DEPOSIT_AMOUNT,
                GatewayType.CUSTOM,
                DEPOSIT_AMOUNT,
                setupState.L1CustomToken
            );
        }

        private async Task<(Contract, Contract)> RegisterCustomToken(L2Network l2Network, Signer l1Signer, Signer l2Signer, AdminErc20Bridger adminErc20Bridger)
        {
            var l1CustomToken = await DeployAbiContract<TestCustomTokenL1>(l1Signer.Provider, l1Signer.Account, l2Network.TokenBridge.L1CustomGateway, l2Network.TokenBridge.L1GatewayRouter);

            if (!IsContractDeployed(l1Signer.Provider, l1CustomToken.Address))
                throw new ArbSdkError("L1 custom token not deployed");

            var l2CustomToken = await DeployAbiContract<TestArbCustomToken>(l2Signer.Provider, l2Signer.Account, l2Network.TokenBridge.L2CustomGateway, l1CustomToken.Address);

            if (!IsContractDeployed(l2Signer.Provider, l2CustomToken.Address))
                throw new ArbSdkError("L2 custom token not deployed");

            var l1GatewayRouter = LoadContract<L1GatewayRouter>(l1Signer.Provider, l2Network.TokenBridge.L1GatewayRouter);
            var l2GatewayRouter = LoadContract<L2GatewayRouter>(l2Signer.Provider, l2Network.TokenBridge.L2GatewayRouter);
            var l1CustomGateway = LoadContract<L1CustomGateway>(l1Signer.Provider, l2Network.TokenBridge.L1CustomGateway);
            var l2CustomGateway = LoadContract<L1CustomGateway>(l2Signer.Provider, l2Network.TokenBridge.L2CustomGateway);

            var startL1GatewayAddress = l1GatewayRouter.L1TokenToGateway(l1CustomToken.Address);
            Assert.AreEqual(Constants.AddressZero, startL1GatewayAddress);

            var startL2GatewayAddress = l2GatewayRouter.L1TokenToGateway(l1CustomToken.Address);
            Assert.AreEqual(Constants.AddressZero, startL2GatewayAddress);

            var startL1Erc20Address = l1CustomGateway.L1ToL2Token(l1CustomToken.Address);
            Assert.AreEqual(Constants.AddressZero, startL1Erc20Address);

            var startL2Erc20Address = l2CustomGateway.L1ToL2Token(l1CustomToken.Address);
            Assert.AreEqual(Constants.AddressZero, startL2Erc20Address);

            var regTxReceipt = await adminErc20Bridger.RegisterCustomToken(
                l1CustomToken.Address,
                l2CustomToken.Address,
                l1Signer,
                l2Signer.Provider
            );

            var l1ToL2Messages = await regTxReceipt.GetL1ToL2Messages(l2Signer.Provider);
            Assert.AreEqual(2, l1ToL2Messages.Length);

            var setTokenTx = await l1ToL2Messages[0].WaitForStatus();
            Assert.AreEqual(L1ToL2MessageStatus.REDEEMED, setTokenTx.Status);

            var setGatewayTx = await l1ToL2Messages[1].WaitForStatus();
            Assert.AreEqual(L1ToL2MessageStatus.REDEEMED, setGatewayTx.Status);

            var endL1GatewayAddress = l1GatewayRouter.L1TokenToGateway(l1CustomToken.Address);
            Assert.AreEqual(l2Network.TokenBridge.L1CustomGateway, endL1GatewayAddress);

            var endL2GatewayAddress = l2GatewayRouter.L1TokenToGateway(l1CustomToken.Address);
            Assert.AreEqual(l2Network.TokenBridge.L2CustomGateway, endL2GatewayAddress);

            var endL1Erc20Address = l1CustomGateway.L1ToL2Token(l1CustomToken.Address);
            Assert.AreEqual(l2CustomToken.Address, endL1Erc20Address);

            var endL2Erc20Address = l2CustomGateway.L1ToL2Token(l1CustomToken.Address);
            Assert.AreEqual(l2CustomToken.Address, endL2Erc20Address);

            return (l1CustomToken, l2CustomToken);
        }
    }
}
