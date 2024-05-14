using System;
using System.Numerics;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Arbitrum.Tests.Integration;
using Arbitrum.Utils;
using Moq;
using Nethereum.Contracts.Standards.ENS.ETHRegistrarController.ContractDefinition;
using Nethereum.Web3;
using NUnit.Framework;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.AssetBridger.Tests.Integration
{
    [TestFixture]
    public class StandardERC20Tests
    {
        private static readonly BigInteger DEPOSIT_AMOUNT = 100;
        private static readonly BigInteger WITHDRAWAL_AMOUNT = 10;

        [OneTimeSetUp]
        public async Task<TestState> SetupState()
        {
            TestState setupState = await TestSetupUtils.TestSetup();

            await TestHelpers.FundL1(setupState.L1Signer);
            await TestHelpers.FundL2(setupState.L2Signer);

            var testToken = await LoadContractUtils.DeployAbiContract(
                provider: new Web3(setupState.L1Signer.TransactionManager.Client),
                deployer: setupState.L1Signer,
                contractName: "TestERC20",
                isClassic: true
                );

            var txHash = await testToken.GetFunction("mint").SendTransactionAsync(from: setupState?.L1Signer?.Address);
            await new Web3(setupState.L1Signer.TransactionManager.Client).Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            setupState.L1Token = testToken;

            return setupState;
        }

        [SetUp]
        public async Task SkipIfMainnet(TestState setupState)
        {
            int chainId = setupState.L1Network.ChainID;
            await TestSetupUtils.SkipIfMainnet(chainId);
        }

        [Test]
        public async Task TestDepositErc20(TestState setupState)
        {
            await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                expectedStatus: L1ToL2MessageStatus.REDEEMED,
                expectedGatewayType: GatewayType.STANDARD
            );
        }

        [Test]
        public async Task DepositWithNoFundsManualRedeem(TestState setupState)
        {
            var depositTokenParams = await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                expectedStatus: L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2,
                expectedGatewayType: GatewayType.STANDARD,
                retryableOverrides: new GasOverrides
                {
                    GasLimit = new PercentIncreaseWithMin() { Base = new BigInteger(0) },
                    MaxFeePerGas = new PercentIncreaseType() { Base = new BigInteger(0) }
                }
            );

            var waitRes = depositTokenParams.WaitRes;
            await RedeemAndTest(setupState, waitRes.Message, 1);
        }

        [Test]
        public async Task DepositWithLowFundsManualRedeem( TestState setupState)
        {
            var depositTokenParams = await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                expectedStatus: L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2,
                expectedGatewayType: GatewayType.STANDARD,
                retryableOverrides: new GasOverrides
                {
                    GasLimit = new PercentIncreaseWithMin() { Base = new BigInteger(5) },
                    MaxFeePerGas = new PercentIncreaseType() { Base = new BigInteger(5) }
                }
            );

            var waitRes = depositTokenParams.WaitRes;
            await RedeemAndTest(setupState, waitRes.Message, 1);
        }

        [Test]
        public async Task DepositWithOnlyLowGasLimitManualRedeemSuccess(TestState setupState)
        {
            var depositTokenParams = await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                expectedStatus: L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2,
                expectedGatewayType: GatewayType.STANDARD,
                retryableOverrides: new GasOverrides
                {
                    GasLimit = new PercentIncreaseWithMin() { Base = new BigInteger(21000) }
                }   
            );

            var waitRes = depositTokenParams.WaitRes;
            var retryableCreation = await waitRes.Message.GetRetryableCreationReceipt();
            if (retryableCreation == null)
            {
                throw new ArbSdkError("Missing retryable creation.");
            }

            var l2Receipt = new L2TransactionReceipt(retryableCreation);
            var redeemsScheduled = l2Receipt.GetRedeemScheduledEvents(setupState.l2Signer.Provider);
            Assert.AreEqual(1, redeemsScheduled.Count, "Unexpected redeem length");

            var retryReceipt = await GetTransactionReceipt(redeemsScheduled[0].RetryTxHash, setupState.l2Signer.Provider);
            Assert.IsNull(retryReceipt, "Retry should not exist");

            await RedeemAndTest(setupState, waitRes.message, 1);
        }

        [Test]
        public async Task DepositWithLowFundsFailsFirstRedeemThenSucceeds()
        {
            var setupState = await SetupState();

            var depositTokenParams = await DepositToken(
                DEPOSIT_AMOUNT,
                setupState.l1Token.Address,
                setupState.erc20Bridger,
                setupState.l1Signer,
                setupState.l2Signer,
                L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2,
                GatewayType.STANDARD,
                new RetryableOverrides
                {
                    GasLimit = new GasLimit { Base = 5 },
                    MaxFeePerGas = new MaxFeePerGas { Base = 5 }
                }
            );

            var waitRes = depositTokenParams["waitRes"];
            await RedeemAndTest(setupState, waitRes.message, 0);

            await RedeemAndTest(setupState, waitRes.message, 1);
        }

        [Test]
        public async Task WithdrawsErc20()
        {
            var setupState = await SetupState();

            var l2TokenAddr = await setupState.erc20Bridger.GetL2ERC20Address(
                setupState.l1Token.Address, setupState.l1Signer.Provider
            );

            var l2Token = setupState.erc20Bridger.GetL2TokenContract(setupState.l2Signer.Provider, l2TokenAddr);

            var startBalance = DEPOSIT_AMOUNT * 5;
            var l2BalanceStart = await l2Token.BalanceOf(setupState.l2Signer.Account.Address);

            Assert.AreEqual(startBalance, l2BalanceStart, "Unexpected L2 balance");

            await WithdrawToken(new
            {
                setupState,
                amount = WITHDRAWAL_AMOUNT,
                gatewayType = GatewayType.STANDARD,
                startBalance,
                l1Token = deployAbiContract(
                    provider: setupState.l1Signer.Provider,
                    deployer: setupState.l1Signer.Account,
                    contractName: "ERC20",
                    isClassic: true
                )
            });
        }

        private async Task<dynamic> SetupState()
        {
            var setup = await TestSetup.TestSetup();

            FundL1(setup.l1Signer);
            FundL2(setup.l2Signer);

            var testToken = deployAbiContract(
                provider: setup.l1Signer.Provider,
                deployer: setup.l1Signer.Account,
                contractName: "TestERC20",
                isClassic: true
            );
            var txHash = await testToken.Mint(setup.l1Signer.Account.Address);

            await setup.l1Signer.Provider.Eth.WaitTransactionReceipt.SendRequestAsync(txHash);

            setup.l1Token = testToken;
            return setup;
        }
    }
}
