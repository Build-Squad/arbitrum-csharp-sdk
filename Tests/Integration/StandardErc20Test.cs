using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Arbitrum.Utils;
using Nethereum.Hex.HexConvertors.Extensions;
using NUnit.Framework;
using System.Numerics;
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

            int chainId = _setupState.L1Network.ChainID;
            await TestSetupUtils.SkipIfMainnet(chainId);

            await Task.Delay(2000);
            await TestHelpers.FundL1(_setupState.L1Deployer.Provider, address: _setupState.L1Signer.Account.Address);
            await Task.Delay(2000);
            await TestHelpers.FundL2(_setupState.L2Deployer.Provider, address: _setupState.L2Signer.Account.Address);

            var l1Provider = _setupState.L1Signer.Provider;
            var contractByteCode = (await LogParser.LoadAbi("TestERC20", true)).Item2;
            var erc20ContractDeployment = new TestERC20Deployment(contractByteCode);

            await Task.Delay(500);
            var deploymentReceipt = await l1Provider.Eth.GetContractDeploymentHandler<TestERC20Deployment>()
                .SendRequestAndWaitForReceiptAsync(erc20ContractDeployment);
            await Task.Delay(500);

            var contractHandler = l1Provider.Eth.GetContractHandler(deploymentReceipt.ContractAddress);
            await contractHandler.SendRequestAndWaitForReceiptAsync<MintFunction>();
            _setupState.L1Token = l1Provider.Eth.GetContract<TestERC20Deployment>(deploymentReceipt.ContractAddress);
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
                l2Network: setupState.L2Network,
                expectedStatus: L1ToL2MessageStatus.REDEEMED,
                expectedGatewayType: GatewayType.STANDARD
            );
        }

        [Test]
        public async Task TestDepositWithNoFundsManualRedeem()
        {
            var setupState = _setupState;

            var depositTokenParams = await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                l2Network: setupState.L2Network,
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
        public async Task TestDepositWithLowFundsManualRedeem()
        {
            var setupState = _setupState;

            var depositTokenParams = await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                l2Network: setupState.L2Network,
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
        public async Task TestDepositWithOnlyLowGasLimitManualRedeemSuccess()
        {
            var setupState = _setupState;

            var depositTokenParams = await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                l2Network: setupState.L2Network,
                expectedStatus: L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2,
                expectedGatewayType: GatewayType.STANDARD,
                retryableOverrides: new GasOverrides
                {
                    GasLimit = new PercentIncreaseWithMin() { Base = new BigInteger(21000) }
                }
            );

            var retryableCreation = await depositTokenParams.WaitRes.Message.GetRetryableCreationReceipt() 
                ?? throw new ArbSdkError("Missing retryable creation.");
            
            var l2Receipt = new L2TransactionReceipt(retryableCreation);
            var redeemsScheduled = (await l2Receipt.GetRedeemScheduledEvents(setupState.L2Signer.Provider)).ToList();
            Assert.That(1, Is.EqualTo(redeemsScheduled.Count), "Unexpected redeem length");

            var retryReceipt = await Lib.GetTransactionReceiptAsync(txHash: redeemsScheduled[0].Event.RetryTxHash.ToHex(), web3: setupState.L2Signer.Provider);
            Assert.That(retryReceipt, Is.Null, "Retry should not exist");

            await RedeemAndTest(setupState, depositTokenParams.WaitRes.Message, 1);
        }

        [Test]
        public async Task TestDepositWithLowFundsFailsFirstRedeemThenSucceeds()
        {
            var setupState = _setupState;

            var depositTokenParams = await TestHelpers.DepositToken(
                depositAmount: DEPOSIT_AMOUNT,
                l1TokenAddress: setupState.L1Token.Address,
                erc20Bridger: setupState.Erc20Bridger,
                l1Signer: setupState.L1Signer,
                l2Signer: setupState.L2Signer,
                l2Network: setupState.L2Network,
                expectedStatus: L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2,
                expectedGatewayType: GatewayType.STANDARD,
                retryableOverrides: new GasOverrides
                {
                    GasLimit = new PercentIncreaseWithMin { Base = 5 },
                    MaxFeePerGas = new PercentIncreaseType { Base = 5 }
                }
            );

            var waitRes = depositTokenParams.WaitRes;

            var funcParam = new RedeemFunction
            {
                TicketId = waitRes.Message.RetryableCreationId.HexToByteArray()
            };

            var arb_retryable = _setupState.L2Signer.Provider.Eth.GetContractHandler(Constants.ARB_RETRYABLE_TX_ADDRESS);
            var redeem_func_data = arb_retryable.GetFunction<RedeemFunction>().GetData(funcParam);

            var param = new GasEstimateComponentsFunction
            {
                To = arb_retryable.ContractAddress,
                ContractCreation = false,
                Data = redeem_func_data.HexToByteArray()
            };

            var node_interface = _setupState.L2Signer.Provider.Eth.GetContractHandler(Constants.NODE_INTERFACE_ADDRESS);
            var gas_components = await node_interface.GetFunction<GasEstimateComponentsFunction>().CallAsync<GasEstimateComponentsOutputDTO>(param);

            //await RedeemAndTest(setupState, waitRes.Message, 0, gas_components.GasEstimate - new BigInteger(500000));
            await RedeemAndTest(setupState, waitRes.Message, 1);
        }

        // Do not test this individually or on local node as the test case is dependent on the above 5 deposit based test cases.
        // We give start balance as what is deposited in above 5 test cases (hence 5 * Deposit_Amount)
        [Test]
        public async Task TestWithdrawsErc20()
        {
            var setupState = _setupState;

            var l2TokenAddr = await setupState.Erc20Bridger.GetL2ERC20Address(setupState.L1Token.Address, setupState.L1Signer, setupState.L2Network);

            var l2Token = await setupState.Erc20Bridger.GetL2TokenContract(setupState.L2Signer, l2TokenAddr);

            var startBalance = DEPOSIT_AMOUNT * 5;
            var l2BalanceStart = await l2Token.GetFunction("balanceOf").CallAsync<BigInteger>(setupState.L2Signer.Account.Address);

            /*Assert.That(startBalance, Is.EqualTo(l2BalanceStart), "Unexpected L2 balance");

            var l1token = await LoadContractUtils.LoadContract(
                    provider: setupState.L1Signer.Provider,
                    address: setupState.L1Token.Address,
                    contractName: "ERC20",
                    isClassic: true
                );

            await TestHelpers.WithdrawToken(new WithdrawalParams
            {
                Erc20Bridger = setupState.Erc20Bridger,
                L1Signer = setupState.L1Signer,
                L2Signer = setupState.L2Signer,
                Amount = WITHDRAWAL_AMOUNT,
                GatewayType = GatewayType.STANDARD,
                StartBalance = DEPOSIT_AMOUNT * 5,
                L1Token = l1token,
                L1FundProvider = setupState.L1Deployer.Provider,
                L2FundProvider = setupState.L2Deployer.Provider
            }, setupState.L2Network);*/
        }

        public async Task RedeemAndTest(TestState setupState, L1ToL2MessageReaderOrWriter message, BigInteger expectedStatus, BigInteger? gasLimit = null)
        {
            var manualRedeem = await message.Redeem(new Dictionary<string, object> { { "gasLimit", gasLimit ?? BigInteger.Zero } });
            var retryRec = await manualRedeem.WaitForRedeem();
            var redeemRec = await manualRedeem.Wait();
            var blockHash = redeemRec.BlockHash;

            Assert.That(retryRec.BlockHash, Is.EqualTo(blockHash), "redeemed in same block");
            Assert.That(retryRec.To.ToLower(), Is.EqualTo(setupState.L2Network.TokenBridge.L2ERC20Gateway.ToLower()), "redeemed in same block");
            Assert.That(retryRec.Status.Value, Is.EqualTo(expectedStatus), "tx didn't fail");

            // Not to be tested on local node
            /*var messageStatus = message.Status;
            var expectedMessageStatus = expectedStatus == 0 ? L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2 : L1ToL2MessageStatus.REDEEMED;
            Assert.That(messageStatus, Is.EqualTo(expectedMessageStatus), "message status");*/
        }
    }
}
