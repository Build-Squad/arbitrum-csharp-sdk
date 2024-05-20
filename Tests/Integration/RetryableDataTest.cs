using Arbitrum.AssetBridger;
using Arbitrum.DataEntities;
using Arbitrum.Scripts;
using Arbitrum.Tests.Integration;
using Arbitrum.Utils;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NUnit.Framework;
using Arbitrum.Message;

using Org.BouncyCastle.Crypto.Tls;
using Serilog.Parsing;
using System.Numerics;
using System.Security.Cryptography;

namespace Arbitrum.Tests.Integration
{
    public class RevertParams
    {
        public string? To { get; set; }
        public string? ExcessFeeRefundAddress { get; set; }
        public string? CallValueRefundAddress { get; set; }
        public BigInteger? L2CallValue { get; set; }
        public byte[]? Data { get; set; }
        public BigInteger? MaxSubmissionCost { get; set; }
        public BigInteger? Value { get; set; }
        public BigInteger? GasLimit { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }
    }
    public class RetryableDataParsingTests
    {
        private readonly BigInteger DEPOSIT_AMOUNT = Web3.Convert.ToWei(100, UnitConversion.EthUnit.Wei);

        private RevertParams CreateRevertParams()
        {
            var l2CallValue = new BigInteger(137);
            var maxSubmissionCost = new BigInteger(1618);

            return new RevertParams
            {
                To = new Account(EthECKey.GenerateKey().GetPrivateKey()).Address,
                ExcessFeeRefundAddress = new Account(EthECKey.GenerateKey().GetPrivateKey()).Address,
                CallValueRefundAddress = new Account(EthECKey.GenerateKey().GetPrivateKey()).Address,
                L2CallValue = l2CallValue,
                Data = HelperMethods.GenerateRandomHex(32).HexToByteArray(),
                MaxSubmissionCost = maxSubmissionCost,
                Value = l2CallValue + maxSubmissionCost + RetryableDataTools.ErrorTriggeringParams.GasLimit + RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas,
                GasLimit = RetryableDataTools.ErrorTriggeringParams.GasLimit,
                MaxFeePerGas = RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas
            };
        }

        private async Task RetryableDataParsing(string funcName)
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l1Provider = l1Signer.Provider;
            var l2Network = setupState.L2Network;

            await TestHelpers.FundL1(l1Signer);

            var inboxContract = await LoadContractUtils.LoadContract(
                provider: l1Provider,
                contractName: "Inbox",
                address: l2Network.EthBridge.Inbox
            );

            var revertParams = CreateRevertParams();
            try
            {
                if (funcName == "estimateGas")
                {
                    await inboxContract.GetFunction("createRetryableTicket").EstimateGasAsync(
                        from: l1Signer.Account.Address,
                        gas: null,
                        value: new HexBigInteger(revertParams.Value.ToString()),
                        functionInput: new object[] {
                        revertParams.To,
                        revertParams.L2CallValue,
                        revertParams.MaxSubmissionCost,
                        revertParams.ExcessFeeRefundAddress,
                        revertParams.CallValueRefundAddress,
                        revertParams.GasLimit,
                        revertParams.MaxFeePerGas,
                        revertParams.Data
                        }
                    );

                }
                else if (funcName == "callStatic")
                {
                    await inboxContract.GetFunction("createRetryableTicket").CallAsync<BigInteger>(
                        from: l1Signer.Account.Address,
                        gas: null,
                        value: new HexBigInteger(revertParams.Value.ToString()),
                        functionInput: new object[] {
                        revertParams.To,
                        revertParams.L2CallValue,
                        revertParams.MaxSubmissionCost,
                        revertParams.ExcessFeeRefundAddress,
                        revertParams.CallValueRefundAddress,
                        revertParams.GasLimit,
                        revertParams.MaxFeePerGas,
                        revertParams.Data,
                        }
                    );
                }

                Assert.Fail($"Expected {funcName} to fail");
            }
            catch (SmartContractCustomErrorRevertException e) when (e.Message.Contains("Smart contract error"))
            {
                var parsedData = RetryableDataTools.TryParseError(e.Message);

                Assert.That(parsedData, Is.Not.Null, "Failed to parse error data");
                Assert.That(parsedData.CallValueRefundAddress, Is.EqualTo(revertParams.CallValueRefundAddress));
                Assert.That(parsedData.Data, Is.EqualTo(revertParams.Data));
                Assert.That(parsedData.Deposit.ToString(), Is.EqualTo(revertParams.Value.ToString()));
                Assert.That(parsedData.ExcessFeeRefundAddress, Is.EqualTo(revertParams.ExcessFeeRefundAddress));
                Assert.That(parsedData.From, Is.EqualTo(l1Signer.Account.Address));
                Assert.That(parsedData.GasLimit.ToString(), Is.EqualTo(revertParams.GasLimit.ToString()));
                Assert.That(parsedData.L2CallValue.ToString(), Is.EqualTo(revertParams.L2CallValue.ToString()));
                Assert.That(parsedData.MaxFeePerGas.ToString(), Is.EqualTo(revertParams.MaxFeePerGas.ToString()));
                Assert.That(parsedData.MaxSubmissionCost.ToString(), Is.EqualTo(revertParams.MaxSubmissionCost.ToString()));
                Assert.That(parsedData.To, Is.EqualTo(revertParams.To));
            }
        }

        [Test]
        public async Task DoesParseErrorInEstimateGas()
        {
            await RetryableDataParsing("estimateGas");
        }

        [Test]
        public async Task DoesParseFromCallStatic()
        {
            await RetryableDataParsing("callStatic");
        }

        [Test]
        public async Task TestERC20DepositComparison()
        {

            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;

            var erc20Bridger = setupState.Erc20Bridger;

            await TestHelpers.FundL1(l1Signer, Web3.Convert.ToWei(2, UnitConversion.EthUnit.Ether));

            var testToken = await LoadContractUtils.DeployAbiContract(
                provider: l1Provider,
                deployer: l1Signer,
                contractName: "TestERC20",
                isClassic: true
                );

            string txHash = await testToken.GetFunction("mint").SendTransactionAsync(l1Signer.Account.Address);

            TransactionReceipt receipt = await l1Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            string l1TokenAddress = testToken.Address;

            await erc20Bridger.ApproveToken(new ApproveParamsOrTxRequest() { Erc20L1Address = l1TokenAddress, L1Signer = l1Signer });

            var retryableOverrides = new GasOverrides
            {
                MaxFeePerGas = new PercentIncreaseType
                {
                    Base = RetryableDataTools.ErrorTriggeringParams.MaxFeePerGas,
                    PercentIncrease = 0
                },
                GasLimit = new PercentIncreaseWithMin
                {
                    Base = RetryableDataTools.ErrorTriggeringParams.GasLimit,
                    Min = 0,
                    PercentIncrease = 0
                }
            };

            var erc20Params = new Erc20DepositParams
            {
                L1Signer = l1Signer,
                L2Provider = l2Provider,
                //from = l1Signer.Address,
                Erc20L1Address = l1TokenAddress,
                Amount = DEPOSIT_AMOUNT,
                RetryableGasOverrides = retryableOverrides
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
            catch (SmartContractCustomErrorRevertException e)
            {
                var parsedData = RetryableDataTools.TryParseError(e.Message);

                Assert.That(parsedData, Is.Not.Null, "Failed to parse error");
                Assert.That(parsedData.CallValueRefundAddress, Is.EqualTo(depositParams.RetryableData.CallValueRefundAddress));
                Assert.That(parsedData.Data, Is.EqualTo(depositParams.RetryableData.Data));
                Assert.That(parsedData.Deposit.ToString(), Is.EqualTo(depositParams.TxRequest.Value.ToString()));
                Assert.That(parsedData.ExcessFeeRefundAddress, Is.EqualTo(depositParams.RetryableData.ExcessFeeRefundAddress));
                Assert.That(parsedData.From, Is.EqualTo(depositParams.RetryableData.From));
                Assert.That(parsedData.GasLimit.ToString(), Is.EqualTo(depositParams.RetryableData.GasLimit.ToString()));
                Assert.That(parsedData.L2CallValue.ToString(), Is.EqualTo(depositParams.RetryableData.L2CallValue.ToString()));
                Assert.That(parsedData.MaxFeePerGas.ToString(), Is.EqualTo(depositParams.RetryableData.MaxFeePerGas.ToString()));
                Assert.That(parsedData.MaxSubmissionCost.ToString(), Is.EqualTo(depositParams.RetryableData.MaxSubmissionCost.ToString()));
                Assert.That(parsedData.To, Is.EqualTo(depositParams.RetryableData.To));
            }
        }
    }
}