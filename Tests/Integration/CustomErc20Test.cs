using System.Numerics;
using Arbitrum.DataEntities;
using Arbitrum.Scripts;
using Arbitrum.Tests.Integration;
using Arbitrum.Utils;
using NBitcoin;
using Nethereum.Contracts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NUnit.Framework;
using static Arbitrum.AssetBridger.Erc20Bridger;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.AssetBridger.Tests.Integration
{
    [TestFixture]
    public class CustomERC20Tests
    {
        private BigInteger DEPOSIT_AMOUNT = 100;
        private BigInteger WITHDRAWL_AMOUNT = 10;

        [OneTimeSetUp]
        public async Task<TestState> SetupState()
        {
            TestState setupState = await TestSetupUtils.TestSetup();
            await TestHelpers.FundL1(setupState.L1Signer);
            await TestHelpers.FundL2(setupState.L2Signer);
            return setupState;
        }

        [SetUp]
        public async Task SkipIfMainnet(TestState setupState)
        {
            int chainId = setupState.L1Network.ChainID;
            await TestSetupUtils.SkipIfMainnet(chainId);
        }

        [Test]
        public async Task TestRegisterCustomToken(TestState setupState)
        {
            var l1Token = (await RegisterCustomToken(
                setupState.L2Network,
                setupState.L1Signer,
                setupState.L2Signer,
                setupState.AdminErc20Bridger
            )).Item1;

            var l2Token = (await RegisterCustomToken(
                setupState.L2Network,
                setupState.L1Signer,
                setupState.L2Signer,
                setupState.AdminErc20Bridger
            )).Item2;
            setupState.L1CustomToken = l1Token;
        }

        [Test]
        public async Task TestDeposit(TestState setupState)
        {
            var txHash = await setupState.L1CustomToken.GetFunction("mint").SendTransactionAsync(from: setupState?.L1Signer?.Address);

            await new Web3(setupState.L1Signer.TransactionManager.Client).Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1CustomToken.Address,
                erc20Bridger: setupState.AdminErc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                expectedStatus: L1ToL2MessageStatus.REDEEMED,
                expectedGatewayType: GatewayType.CUSTOM
            );
        }

        [Test]
        public async Task TestWithdrawTokenAsync(TestState setupState)
        {
            await TestHelpers.WithdrawToken(new WithdrawalParams
            {
                Amount = DEPOSIT_AMOUNT,
                GatewayType = GatewayType.CUSTOM,
                L1Token = await LoadContractUtils.LoadContract(
                                    contractName: "ERC20",
                                    provider: new Web3(setupState.L1Signer),
                                    address: setupState.L1CustomToken.Address,
                                    isClassic: true
                                    ),
                StartBalance = DEPOSIT_AMOUNT,
                L1Signer = setupState.L1Signer,
                L2Signer = setupState.L2Signer,
                Erc20Bridger = setupState.Erc20Bridger
            });
        }

        [Test]
        public async Task<(Contract, Contract)> RegisterCustomToken(L2Network l2Network, Account l1Signer, Account l2Signer, AdminErc20Bridger adminErc20Bridger)
        {
            var l1Provider = new Web3(l1Signer.TransactionManager.Client);
            var l2Provider = new Web3(l2Signer.TransactionManager.Client);

            // Deploy L1 custom token
            var l1CustomToken = await LoadContractUtils.DeployAbiContract(
                provider: l1Provider,
                contractName: "TestCustomTokenL1",
                isClassic: true,
                deployer: l1Signer,
                constructorArgs: new object[] { l2Network.TokenBridge.L1CustomGateway, l2Network.TokenBridge.L1GatewayRouter }
            );

            // Check if L1 custom token is deployed
            if (!(await LoadContractUtils.IsContractDeployed(l1Provider, l1CustomToken.Address)))
            {
                throw new ArbSdkError("L1 custom token not deployed");
            }

            // Deploy L2 custom token
            var l2CustomToken = await LoadContractUtils.DeployAbiContract(
                provider: l2Provider,
                contractName: "TestArbCustomToken",
                isClassic: true,
                deployer: l2Signer,
                constructorArgs: new object[] { l2Network.TokenBridge.L2CustomGateway, l1CustomToken.Address }
            );

            // Check if L2 custom token is deployed
            if (!(await LoadContractUtils.IsContractDeployed(l2Provider, l2CustomToken.Address)))
            {
                throw new ArbSdkError("L2 custom token not deployed");
            }

            // Load contracts
            var l1GatewayRouter = await LoadContractUtils.LoadContract(provider: l2Provider, contractName: "L1GatewayRouter", address: l2Network.TokenBridge.L1GatewayRouter, isClassic: true);
            var l2GatewayRouter = await LoadContractUtils.LoadContract(provider: l2Provider, contractName: "L2GatewayRouter", address: l2Network.TokenBridge.L2GatewayRouter, isClassic: true);
            var l1CustomGateway = await LoadContractUtils.LoadContract(provider: l1Provider, contractName: "L1CustomGateway", address: l2Network.TokenBridge.L1CustomGateway, isClassic: true);
            var l2CustomGateway = await LoadContractUtils.LoadContract(provider: l2Provider, contractName: "L1CustomGateway", address: l2Network.TokenBridge.L2CustomGateway, isClassic: true);

            // Get start L1 gateway address
            var startL1GatewayAddress = await l1GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL1GatewayAddress, Is.EqualTo(Constants.ADDRESS_ZERO));

            // Get start L2 gateway address
            var startL2GatewayAddress = await l2GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL2GatewayAddress, Is.EqualTo(Constants.ADDRESS_ZERO));

            // Get start L1 ERC20 address
            var startL1ERC20Address = await l1CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL1ERC20Address, Is.EqualTo(Constants.ADDRESS_ZERO));

            // Get start L2 ERC20 address
            var startL2ERC20Address = await l2CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(startL2ERC20Address, Is.EqualTo(Constants.ADDRESS_ZERO));

            // Register custom token
            var regTxReceipt = await adminErc20Bridger.RegisterCustomToken(
                l1CustomToken.Address,
                l2CustomToken.Address,
                l1Signer,
                l2Provider
            );

            // Get L1 to L2 messages
            var l1ToL2Messages = (await regTxReceipt.GetL1ToL2Messages(new SignerOrProvider(l2Provider))).ToList();

            // Assert the number of messages
            Assert.That(l1ToL2Messages.Count, Is.EqualTo(2));

            // Wait for status of set token transaction
            var setTokenTx = await l1ToL2Messages[0].WaitForStatus(l2Provider);
            Assert.That(setTokenTx.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));

            // Wait for status of set gateway transaction
            var setGatewayTx = await l1ToL2Messages[1].WaitForStatus(l2Provider);
            Assert.That(setGatewayTx.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));

            // Get end L1 gateway address
            var endL1GatewayAddress = await l1GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL1GatewayAddress, Is.EqualTo(l2Network.TokenBridge.L1CustomGateway));

            // Get end L2 gateway address
            var endL2GatewayAddress = await l2GatewayRouter.GetFunction("l1TokenToGateway").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL2GatewayAddress, Is.EqualTo(l2Network.TokenBridge.L2CustomGateway));

            // Get end L1 ERC20 address
            var endL1Erc20Address = await l1CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL1Erc20Address, Is.EqualTo(l2CustomToken.Address));

            // Get end L2 ERC20 address
            var endL2Erc20Address = await l2CustomGateway.GetFunction("l1ToL2Token").CallAsync<string>(l1CustomToken.Address);
            Assert.That(endL2Erc20Address, Is.EqualTo(l2CustomToken.Address));

            // Return custom tokens
            return (l1CustomToken, l2CustomToken);
        }
    }
}
