using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.Contracts;
using YourNamespace.Lib.SignerOrProvider;
using YourNamespace.Scripts;
using YourNamespace.Tests.Integration.Helpers;
using Arbitrum.DataEntities;

namespace YourNamespace.Tests.Integration
{
    [TestFixture]
    public class RetryableDataTests
    {
        private Web3 l1Provider;
        private Web3 l2Provider;
        private Account l1Signer;
        private Account l2Signer;
        private Address erc20Bridger;

        private const ulong DEPOSIT_AMOUNT = 100;

        [SetUp]
        public async Task Setup()
        {
            var setupState = await TestSetup.TestSetup();
            l1Provider = setupState.L1Signer.Provider;
            l2Provider = setupState.L2Signer.Provider;
            l1Signer = setupState.L1Signer.Account;
            l2Signer = setupState.L2Signer.Account;
            erc20Bridger = setupState.ERC20Bridger;

            await FundL1(l1Signer, Web3.Convert.ToWei(2, Unit.Eth));

            // Deploy contracts and other initializations if needed
        }

        [Test]
        public async Task TestDoesParseErrorInEstimateGas()
        {
            await RetryableDataParsing("estimateGas");
        }

        [Test]
        public async Task TestDoesParseFromCallStatic()
        {
            await RetryableDataParsing("callStatic");
        }

        [Test]
        public async Task TestERC20DepositComparison()
        {
            await ERC20DepositComparison();
        }

        private async Task RetryableDataParsing(string funcName)
        {
            var inboxContract = new Contract(null, "Inbox", l1Provider, null, null);
            var revertParams = CreateRevertParams();

            try
            {
                if (funcName == "estimateGas")
                {
                    await inboxContract.EstimateGas
                        .CreateRetryableTicket(revertParams.To, revertParams.L2CallValue, revertParams.MaxSubmissionCost,
                            revertParams.ExcessFeeRefundAddress, revertParams.CallValueRefundAddress, revertParams.GasLimit,
                            revertParams.MaxFeePerGas, revertParams.Data)
                        .SendTransactionAsync(l1Signer.Address, new HexBigInteger(revertParams.Value));
                }
                else if (funcName == "callStatic")
                {
                    await inboxContract.CallFunction
                        .CreateRetryableTicket(revertParams.To, revertParams.L2CallValue, revertParams.MaxSubmissionCost,
                            revertParams.ExcessFeeRefundAddress, revertParams.CallValueRefundAddress, revertParams.GasLimit,
                            revertParams.MaxFeePerGas, revertParams.Data)
                        .CallAsync<bool>(l1Signer.Address, new HexBigInteger(revertParams.Value));
                }

                Assert.Fail($"Expected {funcName} to fail");
            }
            catch (Exception ex)
            {
                var parsedData = RetryableDataTools.TryParseError(ex.Message);

                Assert.IsNotNull(parsedData, "Failed to parse error data");
                Assert.AreEqual(parsedData.CallValueRefundAddress, revertParams.CallValueRefundAddress);
                Assert.AreEqual(parsedData.Data, revertParams.Data);
                Assert.AreEqual(parsedData.Deposit, revertParams.Value);
                Assert.AreEqual(parsedData.ExcessFeeRefundAddress, revertParams.ExcessFeeRefundAddress);
                Assert.AreEqual(parsedData.From, l1Signer.Address);
                Assert.AreEqual(parsedData.GasLimit, revertParams.GasLimit);
                Assert.AreEqual(parsedData.L2CallValue, revertParams.L2CallValue);
                Assert.AreEqual(parsedData.MaxFeePerGas, revertParams.MaxFeePerGas);
                Assert.AreEqual(parsedData.MaxSubmissionCost, revertParams.MaxSubmissionCost);
                Assert.AreEqual(parsedData.To, revertParams.To);
            }
        }

        private async Task ERC20DepositComparison()
        {
            var testToken = new Contract(null, "TestERC20", l1Provider, null, null);
            var txHash = await testToken.Transactions.Mint.SendTransactionAsync(l1Signer.Address);

            await l1Provider.TransactionManager.TransactionReceiptService
                .WaitForTransactionReceiptAsync(txHash);

            var l1TokenAddress = testToken.Address;

            var retryableOverrides = new
            {
                MaxFeePerGas = new { Base = RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas, PercentIncrease = 0 },
                GasLimit = new { Base = RetryableDataTools.ErrorTriggeringParams.GasLimit, Min = 0, PercentIncrease = 0 }
            };

            var erc20Params = new
            {
                L1Signer = l1Signer.Address,
                L2SignerOrProvider = l2Provider,
                From = l1Signer.Address,
                ERC20L1Address = l1TokenAddress,
                Amount = DEPOSIT_AMOUNT,
                RetryableGasOverrides = retryableOverrides
            };

            var depositParams = await erc20Bridger.CallFunction
                .GetDepositRequest(erc20Params)
                .CallAsync<dynamic>();

            try
            {
                await erc20Bridger.Transactions.Deposit.SendTransactionAsync(erc20Params);
                Assert.Fail("Expected estimateGas to fail");
            }
            catch (Exception ex)
            {
                var parsedData = RetryableDataTools.TryParseError(ex.Message);

                Assert.IsNotNull(parsedData, "Failed to parse error");

                Assert.AreEqual(parsedData.CallValueRefundAddress, depositParams.RetryableData.CallValueRefundAddress);
                Assert.AreEqual(parsedData.Data, depositParams.RetryableData.Data);
                Assert.AreEqual(parsedData.Deposit, depositParams.TxRequest.Value);
                Assert.AreEqual(parsedData.ExcessFeeRefundAddress, depositParams.RetryableData.ExcessFeeRefundAddress);
                Assert.AreEqual(parsedData.From, depositParams.RetryableData.From);
                Assert.AreEqual(parsedData.GasLimit, depositParams.RetryableData.GasLimit);
                Assert.AreEqual(parsedData.L2CallValue, depositParams.RetryableData.L2CallValue);
                Assert.AreEqual(parsedData.MaxFeePerGas, depositParams.RetryableData.MaxFeePerGas);
                Assert.AreEqual(parsedData.MaxSubmissionCost, depositParams.RetryableData.MaxSubmissionCost);
                Assert.AreEqual(parsedData.To, depositParams.RetryableData.To);
            }
        }

        private dynamic CreateRevertParams()
        {
            var l2CallValue = 137;
            var maxSubmissionCost = 1618;

            return new
            {
                To = Account.Create().Address,
                ExcessFeeRefundAddress = Account.Create().Address,
                CallValueRefundAddress = Account.Create().Address,
                L2CallValue = l2CallValue,
                Data = $"0x{Guid.NewGuid().ToString().Replace("-", "")}",
                MaxSubmissionCost = maxSubmissionCost,
                Value = l2CallValue + maxSubmissionCost + RetryableDataTools.ErrorTriggeringParams.GasLimit
                    + RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas,
                GasLimit = RetryableDataTools.ErrorTriggeringParams.GasLimit,
                MaxFeePerGas = RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas
            };
        }

        private async Task FundL1(Account account, ulong amount)
        {
            // Implement logic to fund L1 account
        }
    }
}
