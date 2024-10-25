using Arbitrum.AssetBridger;
using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Arbitrum.src.Lib.DataEntities;
using Arbitrum.Utils;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NUnit.Framework;
using System.Numerics;
using static Arbitrum.AssetBridger.Erc20Bridger;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class RetryableDataParsingTests
    {
        private readonly BigInteger DEPOSIT_AMOUNT = Web3.Convert.ToWei(100, UnitConversion.EthUnit.Wei);


        [Test]
        public async Task DoesParseErrorInGasEstimate()
        {
            await RetryableDataParsing("estimateGas");
        }

        [Test]
        public async Task DoesParseFromCallStatic()
        {
            await RetryableDataParsing("callStatic");
        }

        private static RevertParams CreateRevertParams(SignerOrProvider l1Signer)
        {
            var l2CallValue = new BigInteger(137);
            var maxSubmissionCost = new BigInteger(1618);
            var gasLimit = RetryableDataTools.ErrorTriggeringParams.GasLimit;
            var maxFeePerGas = RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas;

            return new RevertParams
            {
                From = l1Signer.Account.Address,
                To = new Account(EthECKey.GenerateKey().GetPrivateKey()).Address,
                ExcessFeeRefundAddress = new Account(EthECKey.GenerateKey().GetPrivateKey()).Address,
                CallValueRefundAddress = new Account(EthECKey.GenerateKey().GetPrivateKey()).Address,
                L2CallValue = l2CallValue,
                Data = HelperMethods.GenerateRandomHex(32).HexToByteArray(),
                MaxSubmissionCost = maxSubmissionCost,
                Value = l2CallValue + maxSubmissionCost + gasLimit + maxFeePerGas,
                GasLimit = gasLimit.Value,
                MaxFeePerGas = maxFeePerGas.Value
            };
        }

        private async Task RetryableDataParsing(string funcName)
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;
            var erc20Bridger = setupState.Erc20Bridger;

            var revertParams = CreateRevertParams(l1Signer);

            var contractHandler = l1Signer.Provider.Eth.GetContractHandler(setupState.L2Network.EthBridge.Inbox);

            try
            {
                var createRetryableTicketParams = new CreateRetryableTicketFunction
                {
                    To = revertParams.To,
                    L2CallValue = revertParams.L2CallValue,
                    MaxSubmissionCost = revertParams.MaxSubmissionCost,
                    ExcessFeeRefundAddress = revertParams.ExcessFeeRefundAddress,
                    CallValueRefundAddress = revertParams.CallValueRefundAddress,
                    GasLimit = revertParams.GasLimit,
                    MaxFeePerGas = revertParams.MaxFeePerGas,
                    Data = revertParams.Data,
                    FromAddress = l1Signer.Account.Address,
                    AmountToSend = revertParams.Value.Value
                };

                Task.Delay(TimeSpan.FromSeconds(1));
                await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

                if (funcName == "estimateGas")
                {
                    await contractHandler.EstimateGasAsync(createRetryableTicketParams).WaitAsync(CancellationToken.None);
                }
                else if (funcName == "callStatic")
                {
                    await contractHandler.QueryAsync<CreateRetryableTicketFunction, BigInteger>(createRetryableTicketParams).WaitAsync(CancellationToken.None);
                }

                Assert.Fail($"Expected {funcName} to fail");
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                var parsedData = RetryableDataTools.TryParseError(ex.ExceptionEncodedData);

                Assert.That(parsedData, Is.Not.Null, "Failed to parse error data");
                Assert.That(parsedData.From.ToLower(), Is.EqualTo(revertParams.From.ToLower()));
                Assert.That(parsedData.To.ToLower(), Is.EqualTo(revertParams.To.ToLower()));
                Assert.That(parsedData.ExcessFeeRefundAddress.ToLower(), Is.EqualTo(revertParams.ExcessFeeRefundAddress.ToLower()));
                Assert.That(parsedData.CallValueRefundAddress.ToLower(), Is.EqualTo(revertParams.CallValueRefundAddress.ToLower()));
                Assert.That(parsedData.L2CallValue.ToString(), Is.EqualTo(revertParams.L2CallValue.ToString()));
                Assert.That(parsedData.Data, Is.EqualTo(revertParams.Data));
                Assert.That(parsedData.MaxSubmissionCost.ToString(), Is.EqualTo(revertParams.MaxSubmissionCost.ToString()));
                Assert.That(parsedData.Value.ToString(), Is.EqualTo(revertParams.Value.ToString()));
                Assert.That(parsedData.GasLimit.ToString(), Is.EqualTo(revertParams.GasLimit.ToString()));
                Assert.That(parsedData.MaxFeePerGas.ToString(), Is.EqualTo(revertParams.MaxFeePerGas.ToString()));
            }
        }

        [Test]
        public async Task TestERC20DepositComparison()
        {
            var setupState = TestSetupUtils.TestSetup().Result;
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;
            var erc20Bridger = setupState.Erc20Bridger;

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

            var contractByteCode = (await LogParser.LoadAbi("TestERC20", true)).Item2;
            var erc20ContractDeployment = new TestERC20Deployment(contractByteCode);

            var deploymentReceipt = await l1Provider.Eth.GetContractDeploymentHandler<TestERC20Deployment>()
                .SendRequestAndWaitForReceiptAsync(erc20ContractDeployment);

            var contractHandler = l1Provider.Eth.GetContractHandler(deploymentReceipt.ContractAddress);

            await contractHandler.SendRequestAndWaitForReceiptAsync<MintFunction>();

            var l1TokenAddress = deploymentReceipt.ContractAddress;

            await erc20Bridger.ApproveToken(new ApproveParamsOrTxRequest() { Erc20L1Address = l1TokenAddress, L1Signer = l1Signer });

            var retryableOverrides = new GasOverrides
            {
                MaxFeePerGas = new PercentIncreaseType { Base = RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas, PercentIncrease = 0 },
                GasLimit = new PercentIncreaseWithMin { Base = RetryableDataTools.ErrorTriggeringParams.GasLimit, Min = 0, PercentIncrease = 0 }
            };

            var erc20Params = new Erc20DepositParams
            {
                L1Signer = l1Signer,
                L2Provider = l2Provider,
                From = l1Signer.Account.Address,
                Erc20L1Address = l1TokenAddress,
                Amount = DEPOSIT_AMOUNT,
                RetryableGasOverrides = retryableOverrides,
            };

            var depositParams = await erc20Bridger.GetDepositRequest(new DepositRequest
            {
                L1Provider = l1Provider,
                L2Provider = l2Provider,
                L1Signer = erc20Params.L1Signer,
                Erc20L1Address = erc20Params.Erc20L1Address,
                Amount = erc20Params.Amount,
                RetryableGasOverrides = erc20Params.RetryableGasOverrides,
                From = l1Signer.Account.Address
            });

            try
            {
                await erc20Bridger.Deposit(erc20Params);
                Assert.Fail("Expected estimateGas to fail");
            }
            catch (RpcResponseException ex)
            {
                await Task.Delay(200);
                var parsedData = RetryableDataTools.TryParseError(ex.RpcError.Data.ToString());

                Assert.That(parsedData, Is.Not.Null, "Failed to parse error data");
                Assert.That(parsedData.From.ToLower(), Is.EqualTo(depositParams.RetryableData.From.ToLower()));
                Assert.That(parsedData.To.ToLower(), Is.EqualTo(depositParams.RetryableData.To.ToLower()));
                Assert.That(parsedData.ExcessFeeRefundAddress.ToLower(), Is.EqualTo(depositParams.RetryableData.ExcessFeeRefundAddress.ToLower()));
                Assert.That(parsedData.CallValueRefundAddress.ToLower(), Is.EqualTo(depositParams.RetryableData.CallValueRefundAddress.ToLower()));
                Assert.That(parsedData.L2CallValue.ToString(), Is.EqualTo(depositParams.RetryableData.L2CallValue.ToString()));
                Assert.That(parsedData.Data, Is.EqualTo(depositParams.RetryableData.Data));
                Assert.That(parsedData.MaxSubmissionCost.ToString(), Is.EqualTo(depositParams.RetryableData.MaxSubmissionCost.ToString()));
                Assert.That(parsedData.Value.ToString(), Is.EqualTo(depositParams.TxRequest.Value.ToString()));
                Assert.That(parsedData.GasLimit.ToString(), Is.EqualTo(depositParams.RetryableData.GasLimit.ToString()));
                Assert.That(parsedData.MaxFeePerGas.ToString(), Is.EqualTo(depositParams.RetryableData.MaxFeePerGas.ToString()));
            }
        }
    }
}