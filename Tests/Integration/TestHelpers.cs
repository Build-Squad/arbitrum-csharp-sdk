using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Arbitrum.AssetBridger;
using Arbitrum.DataEntities;
using static Arbitrum.DataEntities.SignerOrProvider;
using Arbitrum.Scripts;
using Nethereum.Util;
using Org.BouncyCastle.Bcpg;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.JsonRpc.Client;
using Arbitrum.Message;
using Nethereum.Contracts;
using Nethereum.HdWallet;
using NBitcoin;

namespace Arbitrum.Tests.Integration
{
    public class Config
    {
        public Dictionary<string, string> Values { get; private set; }

        public Config()
        {
            Values = new Dictionary<string, string>
        {
            { "ARB_URL", Environment.GetEnvironmentVariable("ARB_URL") },
            { "ETH_URL", Environment.GetEnvironmentVariable("ETH_URL") },
            { "ARB_KEY", Environment.GetEnvironmentVariable("ARB_KEY") },
            { "ETH_KEY", Environment.GetEnvironmentVariable("ETH_KEY") }
        };
        }
    }
    public enum GatewayType
    {
        STANDARD = 1,
        CUSTOM = 2,
        WETH = 3
    }

    public class WithdrawalParams
    {
        public BigInteger? StartBalance { get; set; }
        public BigInteger? Amount { get; set; }
        public Erc20Bridger? Erc20Bridger { get; set; }
        public Contract? L1Token { get; set; }
        public Account? L2Signer { get; set; }
        public Account? L1Signer { get; set; }
        public GatewayType? GatewayType { get; set; }
    }

    [TestFixture]
    public class AssetBridgerTests
    {

        private static readonly BigInteger PRE_FUND_AMOUNT = Web3.Convert.ToWei(0.1, UnitConversion.EthUnit.Ether);

        public async Task MineUntilStop(Account miner, Dictionary<string, object> state)
        {
            while ((bool)state["mining"])
            {
                var tx = new TransactionRequest
                {
                    From = miner.Address,
                   To = miner.Address,
                   Value = 0,
                    ChainId = await new Web3(miner.TransactionManager.Client).Eth.ChainId.SendRequestAsync(), // Assuming Provider has an eth property
                    GasPrice = await new Web3(miner.TransactionManager.Client).Eth.GasPrice.SendRequestAsync(), // Assuming Provider has an eth property
                    Nonce = await new Web3(miner.TransactionManager.Client).Eth.Transactions.GetTransactionCount.SendRequestAsync(miner.Address) // Assuming Provider has an eth property
                };

                // Estimate gas asynchronously
                var gasEstimate = await new Web3(miner.TransactionManager.Client).TransactionManager.EstimateGasAsync(tx);

                tx.Gas = gasEstimate;

                // Sign transaction asynchronously
                var signedTx = await new Web3(miner.TransactionManager.Client).TransactionManager.SignTransactionAsync(tx); // Assuming Account has a method for signing transactions

                // Send raw transaction asynchronously
                var txHash = await new Web3(miner.TransactionManager.Client).Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);  // Assuming Provider has a method for sending raw transactions

                // Wait for transaction receipt asynchronously
                await new Web3(miner.TransactionManager.Client).Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

                await Task.Delay(15000); // Delay for 15 seconds
            }
        }

        [Test]
        public async Task WithdrawToken(WithdrawalParams parameters)
        {

            var withdrawalParams = await parameters.Erc20Bridger.GetWithdrawalRequest(
                new Erc20WithdrawParams()
                {
                    Amount = parameters.Amount,
                    Erc20L1Address = parameters.L1Token.Address,
                    DestinationAddress = parameters.L2Signer.Address,
                    From = parameters.L2Signer.Address,
                }
                );

            var l1GasEstimate = withdrawalParams.EstimateL1GasLimit = await new Web3(parameters.L1Signer.TransactionManager.Client).TransactionManager.EstimateGasAsync();

            var withdrawRec = await parameters.Erc20Bridger.Withdraw(new Erc20WithdrawParams()
            {
                DestinationAddress = parameters.L2Signer.Address,
                Amount = parameters.Amount,
                Erc20L1Address = parameters.L1Token.Address,
                L2Signer = parameters.L2Signer
            });

            Assert.That(withdrawRec.Status, Is.EqualTo(1), "initiate token withdraw txn failed");

            var message = (await withdrawRec.GetL2ToL1Messages(new SignerOrProvider(parameters.L1Signer))).FirstOrDefault();

            Assert.That(message, Is.Not.Null, "withdraw message not found");

            var messageStatus = message.StatusBase(new SignerOrProvider(new Web3(parameters.L2Signer.TransactionManager.Client)));

            Assert.That(messageStatus, Is.EqualTo(L2ToL1MessageStatus.UNCONFIRMED), "invalid withdraw status");

            var l2TokenAddr = await parameters.Erc20Bridger.GetL2ERC20Address(parameters.L1Token.Address, new Web3(parameters.L1Signer.TransactionManager.Client));

            var l2Token = await parameters.Erc20Bridger.GetL2TokenContract(new Web3(parameters.L2Signer.TransactionManager.Client), l2TokenAddr);

            var testWalletL2Balance = await l2Token.GetFunction("balanceOf").CallAsync<BigInteger>(parameters.L2Signer.Address);

            Assert.That(testWalletL2Balance, Is.EqualTo(parameters.StartBalance - parameters.Amount), "token withdraw balance not deducted");

            var walletAddress = parameters.L1Signer.Address;

            var gatewayAddress = await parameters.Erc20Bridger.GetL2GatewayAddress(parameters.L1Token.Address, new Web3(parameters.L2Signer.TransactionManager.Client));

            var expectedGateways = GetGateways(parameters.GatewayType, parameters.Erc20Bridger.L2Network);

            Assert.That(gatewayAddress, Is.EqualTo(expectedGateways.ExpectedL2Gateway), "Gateway is not custom gateway");

            var gatewayWithdrawEvents = await parameters.Erc20Bridger.GetL2WithdrawalEvents(
                new Web3(parameters.L2Signer.TransactionManager.Client),
                gatewayAddress,
                new NewFilterInput() 
                { 
                    FromBlock = new BlockParameter(withdrawRec.BlockNumber),
                    ToBlock = BlockParameter.CreateLatest()
                },
                parameters.L1Token.Address,
                walletAddress);

            Assert.That(gatewayWithdrawEvents.Count, Is.EqualTo(1), "token query failed");

            var balBefore = await parameters.L1Token.GetFunction("balanceOf").CallAsync<BigInteger>(parameters.L1Signer.Address);

            //creating a random wallet
            string mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString();

            // Connect the wallet to the provider
            var miner1Seed = new Wallet(mnemonic, "");
            var miner2Seed = new Wallet(mnemonic, "");

            //private keys of miners
            var miner1PrivateKey = miner1Seed.GetPrivateKey(0);
            var miner2PrivateKey = miner2Seed.GetPrivateKey(0);

            //accounts of miners
            var miner1Account = new Account(miner1PrivateKey);
            var miner2Account = new Account(miner2PrivateKey);

            var miner1 = new SignerOrProvider(miner1Account, new Web3(parameters.L1Signer.TransactionManager.Client));
            var miner2 = new SignerOrProvider(miner2Account, new Web3(parameters.L2Signer.TransactionManager.Client));

            await FundL1(miner1, UnitConversion.Convert.ToWei(1, UnitConversion.EthUnit.Ether));
            await FundL2(miner2, UnitConversion.Convert.ToWei(1, UnitConversion.EthUnit.Ether));
            var state = new Dictionary<string, object> { { "mining", true } };

            await Task.WhenAny(
                MineUntilStop(miner1?.Account, state),
                MineUntilStop(miner2?.Account, state),
                message.WaitUntilReadyToExecuteBase(new SignerOrProvider(new Web3(parameters.L2Signer.TransactionManager.Client)))
            );


            Assert.That(await message.StatusBase(new SignerOrProvider(new Web3(parameters.L2Signer.TransactionManager.Client))), Is.EqualTo(L2ToL1MessageStatus.CONFIRMED), "confirmed status");

            var execTx = await message.WaitUntilReadyToExecuteBase(new SignerOrProvider(new Web3(parameters.L2Signer.TransactionManager.Client)));

            var execRec = await execTx.Wait();




















            var setupState = await TestSetup();
            //var parameters = new Dictionary<string, dynamic>
            //{
            //    {"amount", Web3.Convert.ToWei(10)},
            //    {"l1Token", setupState.L1Token},
            //    {"l1Signer", setupState.L1Signer},
            //    {"l2Signer", setupState.L2Signer},
            //    {"erc20Bridger", setupState.Erc20Bridger},
            //    {"startBalance", Web3.Convert.ToWei(50)},
            //    {"gatewayType", GatewayType.STANDARD}
            //};

            await WithdrawToken(parameters);
        }

        [Test]
        public async Task DepositToken()
        {
            var setupState = await TestSetup();
            var depositAmount = Web3.Convert.ToWei(100);
            var parameters = new Dictionary<string, dynamic>
            {
                {"depositAmount", depositAmount},
                {"l1TokenAddress", setupState.L1Token.Address},
                {"erc20Bridger", setupState.Erc20Bridger},
                {"l1Signer", setupState.L1Signer},
                {"l2Signer", setupState.L2Signer},
                {"expectedStatus", L2ToL1MessageStatus.UNCONFIRMED},
                {"expectedGatewayType", GatewayType.STANDARD},
                {"retryableOverrides", null}
            };

            await DepositToken(parameters);
        }

        private async Task<TestSetup> TestSetup()
        {
            var config = TestSetup.Config;
            var arbSys = "0x0000000000000000000000000000000000000064";

            var l1Signer = new SignerOrProvider(
                new Account(config["ETH_KEY"]),
                new Web3(config["L1_PROVIDER_URL"])
            );

            var l2Signer = new SignerOrProvider(
                new Account(config["ARB_KEY"]),
                new Web3(config["L2_PROVIDER_URL"])
            );

            var erc20Bridger = new Erc20Bridger(arbSys, l1Signer.Provider, l2Signer.Provider);

            var testSetup = new TestSetup
            {
                Erc20Bridger = erc20Bridger,
                L1Signer = l1Signer,
                L2Signer = l2Signer
            };

            await FundL1(testSetup.L1Signer, Web3.Convert.ToWei(1));
            await FundL2(testSetup.L2Signer, Web3.Convert.ToWei(1));

            var testToken = await DeployAbiContract(
                l1Signer.Provider,
                l1Signer.Account,
                "TestERC20",
                true
            );

            var txHash = await testToken.Mint(l1Signer.Account.Address);
            await l1Signer.Provider.Eth.GetTransactionReceipt.SendRequestAsync(txHash);

            testSetup.L1Token = testToken;

            return testSetup;
        }

        private async Task DepositToken(Dictionary<string, dynamic> parameters)
        {
            // Implement deposit token logic
        }

        private async Task WithdrawToken(Dictionary<string, dynamic> parameters)
        {
            // Implement withdraw token logic
        }

        private async Task<dynamic> DeployAbiContract(Web3 provider, Account account, string contractName, bool isClassic)
        {
            // Implement contract deployment logic
        }

        private async Task FundL1(SignerOrProvider signer, BigInteger? amount = null)
        {
            // Implement L1 funding logic
        }

        private async Task FundL2(SignerOrProvider signer, BigInteger? amount = null)
        {
            // Implement L2 funding logic
        }
    }
}
