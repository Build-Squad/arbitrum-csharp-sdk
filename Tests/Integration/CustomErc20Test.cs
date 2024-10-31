using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.Scripts;
using Arbitrum.Utils;
using Nethereum.Contracts;
using NUnit.Framework;
using System.Numerics;
using static Arbitrum.AssetBridger.Erc20Bridger;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class CustomERC20Tests
    {
        private static readonly BigInteger DEPOSIT_AMOUNT = new(0.1m);
        private static readonly BigInteger WITHDRAWAL_AMOUNT = new(0.01m);

        private TestState _setupState;

        [SetUp]
        public async Task SetUp()
        {
            _setupState = await SetupState();
            int chainId = _setupState.L1Network.ChainID;
            await TestSetupUtils.SkipIfMainnet(chainId);

            var (l1Token, l2Token) = await RegisterCustomToken(
                _setupState.L2Network,
                _setupState.L1Signer,
                _setupState.L2Signer,
                _setupState.AdminErc20Bridger
            );

            _setupState.L1CustomToken = l1Token;
        }


        public async Task<TestState> SetupState()
        {
            var setupState = await TestSetupUtils.TestSetup();
            await Task.Delay(2000);
            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: setupState.L1Signer.Account.Address);
            await Task.Delay(2000);
            await TestHelpers.FundL2(setupState.L2Deployer.Provider, address: setupState.L2Signer.Account.Address);

            return setupState;
        }

        [Test]
        public async Task TestCustomTokenRegistration()
        {
            var (l1Token, l2Token) = await RegisterCustomToken(
                _setupState.L2Network,
                _setupState.L1Signer,
                _setupState.L2Signer,
                _setupState.AdminErc20Bridger
            );

            Assert.That(l1Token, Is.Not.Null);
            Assert.That(l2Token, Is.Not.Null);
        }

        [Test]
        public async Task TestDeposit()
        {
            await SetupState();
            Assert.That(_setupState.L1CustomToken, Is.Not.Null);

            var contractHandler = _setupState?.L1Signer?.Provider.Eth.GetContractHandler(_setupState?.L1CustomToken.Address);
            var mintFunctionTxnReceipt = await contractHandler.SendRequestAndWaitForReceiptAsync<MintFunction>();

            await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: _setupState.L1CustomToken.Address,
                erc20Bridger: _setupState.AdminErc20Bridger,
                l1Signer: _setupState.L1Signer,
                l2Signer: _setupState.L2Signer,
                l2Network: _setupState.L2Network,
                expectedStatus: L1ToL2MessageStatus.REDEEMED,
                expectedGatewayType: GatewayType.CUSTOM
            );
        }

        [Test]
        public async Task TestWithdrawToken()
        {
            await SetupState();
            Assert.That(_setupState.L1CustomToken, Is.Not.Null);

            var l1Token = await LoadContractUtils.LoadContract(
                                    contractName: "ERC20",
                                    provider: _setupState.L1Signer.Provider,
                                    address: _setupState.L1CustomToken.Address,
                                    isClassic: true
                                    );

            await TestHelpers.WithdrawToken(new WithdrawalParams
            {
                Amount = WITHDRAWAL_AMOUNT,
                GatewayType = GatewayType.CUSTOM,
                L1Token = l1Token,
                StartBalance = DEPOSIT_AMOUNT,
                L1Signer = _setupState.L1Signer,
                L2Signer = _setupState.L2Signer,
                Erc20Bridger = _setupState.AdminErc20Bridger,
                L1FundProvider = _setupState.L1Deployer.Provider,
                L2FundProvider = _setupState.L2Deployer.Provider
            }, _setupState.L2Network);
        }

        public async Task<Tuple<Contract, Contract>> RegisterCustomToken(L2Network l2Network, SignerOrProvider l1Signer, SignerOrProvider l2Signer, AdminErc20Bridger adminErc20Bridger)
        {
            var l1CustomToken = await LoadContractUtils.DeployAbiContract(
                provider: l1Signer.Provider,
                contractName: "TestCustomTokenL1",
                isClassic: true,
                deployer: l1Signer,
                constructorArgs: new object[] { l2Network.TokenBridge.L1CustomGateway, l2Network.TokenBridge.L1GatewayRouter }
            );

            await Task.Delay(1000);

            if (!(await LoadContractUtils.IsContractDeployed(l1Signer.Provider, l1CustomToken.Address)))
            {
                throw new ArbSdkError("L1 custom token not deployed");
            }

            var l2CustomToken = await LoadContractUtils.DeployAbiContract(
                provider: l2Signer.Provider,
                contractName: "TestArbCustomToken",
                isClassic: true,
                deployer: l2Signer,
                constructorArgs: new object[] { l2Network.TokenBridge.L2CustomGateway, l1CustomToken.Address }
            );

            if (!(await LoadContractUtils.IsContractDeployed(l2Signer.Provider, l2CustomToken.Address)))
            {
                throw new ArbSdkError("L2 custom token not deployed");
            }

            var l1GatewayRouter = await LoadContractUtils.LoadContract(provider: l1Signer.Provider, contractName: "L1GatewayRouter", address: l2Network.TokenBridge.L1GatewayRouter, isClassic: true);
            var l2GatewayRouter = await LoadContractUtils.LoadContract(provider: l2Signer.Provider, contractName: "L2GatewayRouter", address: l2Network.TokenBridge.L2GatewayRouter, isClassic: true);
            var l1CustomGateway = await LoadContractUtils.LoadContract(provider: l1Signer.Provider, contractName: "L1CustomGateway", address: l2Network.TokenBridge.L1CustomGateway, isClassic: true);
            var l2CustomGateway = await LoadContractUtils.LoadContract(provider: l2Signer.Provider, contractName: "L1CustomGateway", address: l2Network.TokenBridge.L2CustomGateway, isClassic: true);

            var startL1GatewayAddress = await l1GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL1GatewayAddress, Is.EqualTo(Constants.ADDRESS_ZERO));

            var startL2GatewayAddress = await l2GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL2GatewayAddress, Is.EqualTo(Constants.ADDRESS_ZERO));

            var startL1ERC20Address = await l1CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL1ERC20Address, Is.EqualTo(Constants.ADDRESS_ZERO));

            var startL2ERC20Address = await l2CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL2ERC20Address, Is.EqualTo(Constants.ADDRESS_ZERO));

            var regTxReceipt = await adminErc20Bridger.RegisterCustomToken(
                l1CustomToken.Address,
                l2CustomToken.Address,
                l1Signer,
                l2Signer.Provider
            );

            var l1ToL2Messages = (await regTxReceipt.GetL1ToL2Messages(l2Signer.Provider)).ToList();

            Assert.That(l1ToL2Messages.Count, Is.EqualTo(2));

            var setTokenTx = await l1ToL2Messages[0].WaitForStatus();
            Assert.That(setTokenTx.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));

            var setGatewayTx = await l1ToL2Messages[1].WaitForStatus();
            Assert.That(setGatewayTx.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));

            var endL1GatewayAddress = await l1GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL1GatewayAddress, Is.EqualTo(l2Network.TokenBridge.L1CustomGateway));

            var endL2GatewayAddress = await l2GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL2GatewayAddress, Is.EqualTo(l2Network.TokenBridge.L2CustomGateway));

            var endL1Erc20Address = await l1CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL1Erc20Address.ToLower(), Is.EqualTo(l2CustomToken.Address));

            var endL2Erc20Address = await l2CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL2Erc20Address.ToLower(), Is.EqualTo(l2CustomToken.Address));

            return new Tuple<Contract, Contract>(l1CustomToken, l2CustomToken);
        }
    }
}
