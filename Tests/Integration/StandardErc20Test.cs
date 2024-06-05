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

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class StandardERC20Tests
    {
        private static readonly BigInteger DEPOSIT_AMOUNT = 100;
        private static readonly BigInteger WITHDRAWAL_AMOUNT = 10;
        private TestState _setupState;

        [SetUp]
        public async Task SetupState()
        {
            _setupState = await TestSetupUtils.TestSetup();

            //await TestHelpers.FundL1(setupState.L1Signer);
            //await TestHelpers.FundL2(setupState.L2Signer);

            var testToken = await LoadContractUtils.DeployAbiContract(
                provider: _setupState.L1Signer.Provider,
                deployer: _setupState.L1Signer,
                contractName: "TestERC20",
                isClassic: true
                );

            var tx = new TransactionRequest() { From = _setupState?.L1Signer?.Account.Address, Gas = await testToken.GetFunction("mint").EstimateGasAsync()  };
            var receipt = await testToken.GetFunction("mint").SendTransactionAndWaitForReceiptAsync(tx);
            //var receipt = await setupState.L1Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            _setupState.L1Token = testToken;
        }

        [SetUp]
        public async Task SkipIfMainnet()
        {
            var setupState = _setupState;
            int chainId = setupState.L1Network.ChainID;
            await TestSetupUtils.SkipIfMainnet(chainId);
        }

        [Test]
        public async Task TestDepositErc20()
        {
            var setupState = _setupState;

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
        public async Task DepositWithNoFundsManualRedeem()
        {
            var setupState = _setupState;

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
        public async Task DepositWithLowFundsManualRedeem()
        {
            var setupState = _setupState;

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
        public async Task DepositWithOnlyLowGasLimitManualRedeemSuccess()
        {
            var setupState = _setupState;

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
            var redeemsScheduled = (await l2Receipt.GetRedeemScheduledEvents(setupState.L2Signer.Provider)).ToList();
            Assert.That(1, Is.EqualTo(redeemsScheduled.Count), "Unexpected redeem length");

            var retryReceipt = await Lib.GetTransactionReceiptAsync(txHash: redeemsScheduled[0].Event.RetryTxHash, web3: setupState.L2Signer.Provider);
            Assert.That(retryReceipt, Is.Null, "Retry should not exist");

            await RedeemAndTest(setupState, waitRes.Message, 1);
        }

        [Test]
        public async Task DepositWithLowFundsFailsFirstRedeemThenSucceeds()
        {
            var setupState = _setupState;

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
                    GasLimit = new PercentIncreaseWithMin { Base = 5 },
                    MaxFeePerGas = new PercentIncreaseType { Base = 5 }
                }
            );

            var waitRes = depositTokenParams.WaitRes;
            await RedeemAndTest(setupState, waitRes.Message, 0);

            await RedeemAndTest(setupState, waitRes.Message, 1);
        }

        [Test]
        public async Task TestWithdrawsErc20()
        {
            var setupState = _setupState;

            var l2TokenAddr = await setupState.Erc20Bridger.GetL2ERC20Address(
                setupState.L1Token.Address, setupState.L1Signer.Provider
            );

            var l2Token = await setupState.Erc20Bridger.GetL2TokenContract(setupState.L2Signer.Provider, l2TokenAddr);

            var startBalance = DEPOSIT_AMOUNT * 5;
            var l2BalanceStart = await l2Token.GetFunction("balanceOf").CallAsync<BigInteger>(setupState.L2Signer.Account.Address);

            Assert.That(startBalance, Is.EqualTo(l2BalanceStart), "Unexpected L2 balance");

            await TestHelpers.WithdrawToken(new WithdrawalParams
            {
                Erc20Bridger = setupState.Erc20Bridger,
                L1Signer = setupState.L1Signer,
                L2Signer = setupState.L1Signer,
                Amount = WITHDRAWAL_AMOUNT,
                GatewayType = GatewayType.STANDARD,
                StartBalance = startBalance,
                L1Token = await LoadContractUtils.DeployAbiContract(
                    provider: setupState.L1Signer.Provider,
                    deployer: setupState.L1Signer,
                    contractName: "ERC20",
                    isClassic: true
                )
            });
        }
        public async Task RedeemAndTest(TestState setupState, L1ToL2MessageReaderOrWriter message, int expectedStatus, BigInteger? gasLimit = null)
        {
            var manualRedeem = await message.Redeem(new Dictionary<string, object> { { "gasLimit", gasLimit } });
            var retryRec = await manualRedeem.WaitForRedeem();
            var redeemRec = await manualRedeem.Wait();
            var blockHash = redeemRec.BlockHash;

            Assert.That(retryRec.BlockHash, Is.EqualTo(blockHash), "redeemed in same block");
            Assert.That(retryRec.To, Is.EqualTo(setupState.L2Network.TokenBridge.L2ERC20Gateway), "redeemed in same block");
            Assert.That(retryRec.Status, Is.EqualTo(expectedStatus), "tx didn't fail");

            var messageStatus = await message.Status();
            var expectedMessageStatus = expectedStatus == 0 ? L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2 : L1ToL2MessageStatus.REDEEMED;
            Assert.That(messageStatus, Is.EqualTo(expectedMessageStatus), "message status");
        }
    }
}
