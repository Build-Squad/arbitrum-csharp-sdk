using System;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using NUnit.Framework;
using YourNamespace.Lib.DataEntities.Constants;
using YourNamespace.Lib.DataEntities.Errors;
using YourNamespace.Lib.DataEntities.Message;
using YourNamespace.Lib.DataEntities.Types;
using YourNamespace.Lib.Message;
using YourNamespace.Lib.Utils.Helper;
using YourNamespace.Lib.Utils.Lib;
using YourNamespace.Scripts;
using YourNamespace.Tests.Integration.TestHelpers;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace YourNamespace.Tests.Integration
{
    [TestFixture]
    public class TransactionTests
    {
        private static readonly decimal DEPOSIT_AMOUNT = 100;
        private static readonly decimal WITHDRAWAL_AMOUNT = 10;

        [Test]
        public async Task DepositErc20()
        {
            var setupState = await SetupState();

            await DepositToken(
                DEPOSIT_AMOUNT,
                setupState.l1Token.Address,
                setupState.erc20Bridger,
                setupState.l1Signer,
                setupState.l2Signer,
                L1ToL2MessageStatus.REDEEMED,
                GatewayType.STANDARD
            );
        }

        [Test]
        public async Task DepositWithNoFundsManualRedeem()
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
                    GasLimit = new GasLimit { Base = 0 },
                    MaxFeePerGas = new MaxFeePerGas { Base = 0 }
                }
            );

            var waitRes = depositTokenParams["waitRes"];
            await RedeemAndTest(setupState, waitRes.message, 1);
        }

        [Test]
        public async Task DepositWithLowFundsManualRedeem()
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
            await RedeemAndTest(setupState, waitRes.message, 1);
        }

        [Test]
        public async Task DepositWithOnlyLowGasLimitManualRedeemSuccess()
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
                    GasLimit = new GasLimit { Base = 21000 }
                }
            );

            var waitRes = depositTokenParams["waitRes"];
            var retryableCreation = await waitRes.message.GetRetryableCreationReceipt();
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

        private async Task<dynamic> DepositToken(decimal depositAmount, string l1TokenAddress, dynamic erc20Bridger,
            dynamic l1Signer, dynamic l2Signer, int expectedStatus, string expectedGatewayType, RetryableOverrides retryableOverrides = null)
        {
            // Implement deposit token logic
        }

        private async Task RedeemAndTest(dynamic setupState, dynamic message, int expectedStatus, int? gasLimit = null)
        {
            // Implement redeem and test logic
        }

        private async Task<dynamic> GetTransactionReceipt(string txHash, dynamic provider)
        {
            // Implement get transaction receipt logic
        }

        private async Task WithdrawToken(dynamic parameters)
        {
            // Implement withdraw token logic
        }
    }
}
