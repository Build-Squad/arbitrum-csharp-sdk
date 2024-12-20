﻿using Arbitrum.AssetBridgerModule;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NUnit.Framework;
using System.Numerics;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.Tests.Integration
{
    public class EthTests
    {
        [Test]
        public async Task TestTransfersEtherOnL2()
        {
            TestState setupState = await TestSetupUtils.TestSetup();
            var l2Signer = setupState.L2Signer;
            var l2Provider = l2Signer.Provider;

            await TestHelpers.FundL2(setupState.L2Deployer.Provider, address: l2Signer.Account.Address);

            var privateKey = EthECKey.GenerateKey().GetPrivateKey();
            var account = new Account(privateKey);
            var randomAddress = account.Address;

            var amountToSend = Web3.Convert.ToWei(0.000005, UnitConversion.EthUnit.Ether);

            var balanceBefore = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);
            var tx = new TransactionRequest
            {
                From = l2Signer.Account.Address,
                To = randomAddress,
                Value = amountToSend.ToHexBigInteger(),
                MaxFeePerGas = new HexBigInteger(15000000000),
                MaxPriorityFeePerGas = BigInteger.Zero.ToHexBigInteger()
            };

            tx.Gas ??= await l2Provider.Eth.TransactionManager.EstimateGasAsync(tx);

            var signedTx = await l2Signer.Provider.Eth.TransactionManager.SignTransactionAsync(tx);
            var txnHash = await l2Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);
            var txReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);


            var balanceAfter = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);
            var randomBalanceAfter = await l2Provider.Eth.GetBalance.SendRequestAsync(randomAddress);

            Assert.That(Web3.Convert.FromWei(randomBalanceAfter, UnitConversion.EthUnit.Ether), Is.EqualTo(Web3.Convert.FromWei(amountToSend, UnitConversion.EthUnit.Ether)), "Random address balance after should match the sent amount");

            var expectedBalanceAfter = balanceBefore - txReceipt?.GasUsed?.Value * txReceipt?.EffectiveGasPrice?.Value - amountToSend;

            Assert.That(balanceAfter.Value, Is.EqualTo(expectedBalanceAfter.Value), "L2 signer balance after should be correctly reduced");
        }

        [Test]
        public async Task TestDepositsEther()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var ethBridger = setupState.EthBridger;
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;

            var initialTestWalletL2EthBalance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

            var inboxAddress = ethBridger.L2Network.EthBridge.Inbox;
            var initialInboxBalance = await l1Provider.Eth.GetBalance.SendRequestAsync(inboxAddress);

            var ethToDeposit = Web3.Convert.ToWei(0.0002m);

            var rec = await ethBridger.Deposit(new EthDepositParams()
            {
                Amount = ethToDeposit,
                L1Signer = l1Signer
            });

            Assert.That(rec.Status.Value, Is.EqualTo(BigInteger.One), "ETH deposit L1 transaction failed");

            var finalInboxBalance = await l1Provider.Eth.GetBalance.SendRequestAsync(inboxAddress);

            var result = await l1Signer.Provider.Eth.Transactions.GetTransactionByHash.SendRequestAsync(rec.TransactionHash);

            // Also fails in TS implementation - https://github.com/OffchainLabs/arbitrum-sdk/pull/407
            // Assert.That(finalInboxBalance, Is.EqualTo(initialInboxBalance + ethToDeposit), "Balance failed to update after ETH deposit");

            var waitResult = await rec.WaitForL2(l2Provider);
            Assert.That(waitResult.Complete, Is.True, "Eth deposit not complete");
            Assert.That(waitResult.L2TxReceipt, Is.Not.Null);

            var l1ToL2Messages = (await rec.GetEthDeposits(l2Provider)).ToList();

            Assert.That(l1ToL2Messages.Count, Is.EqualTo(1), "Failed to find 1 L1 to L2 message");
            var l1ToL2Message = l1ToL2Messages.FirstOrDefault();

            var walletAddress = l1Signer.Account.Address;

            Assert.That(l1ToL2Message.ToAddress, Is.EqualTo(walletAddress), "Message inputs value error");

            Assert.That(l1ToL2Message.Value, Is.EqualTo(ethToDeposit), "Message inputs value error");

            await Task.Delay(TimeSpan.FromSeconds(2));
            var finalTestWalletL2EthBalance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);

            Assert.That(finalTestWalletL2EthBalance.Value, Is.EqualTo(initialTestWalletL2EthBalance + ethToDeposit), "Final balance incorrect");
        }

        [Test]
        public async Task TestDepositsEtherToSpecificL2Address()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var ethBridger = setupState.EthBridger;
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

            var inboxAddress = ethBridger.L2Network.EthBridge.Inbox;
            var initialInboxBalance = await l1Provider.Eth.GetBalance.SendRequestAsync(inboxAddress);

            var privateKey = EthECKey.GenerateKey().GetPrivateKey();
            var account = new Account(privateKey);
            var destWalletAddress = account.Address;

            var ethToDeposit = Web3.Convert.ToWei(0.0002m, UnitConversion.EthUnit.Ether);
            var rec = await ethBridger.DepositTo(new EthDepositToParams
            {
                Amount = ethToDeposit,
                L1Signer = l1Signer,
                DestinationAddress = destWalletAddress,
                L2Provider = l2Provider
            });

            Assert.That((int)rec.Status.Value, Is.EqualTo(1), "ETH deposit L1 transaction failed");

            var finalInboxBalance = await l1Provider.Eth.GetBalance.SendRequestAsync(inboxAddress);

            // Also fails in TS implementation - https://github.com/OffchainLabs/arbitrum-sdk/pull/407
            // Assert.That(finalInboxBalance, Is.EqualTo(initialInboxBalance + ethToDeposit), "Balance failed to update after ETH deposit");

            var l1ToL2Messages = (await rec.GetL1ToL2Messages(l2Provider)).ToList();
            Assert.That(l1ToL2Messages.Count, Is.EqualTo(1), "Failed to find 1 L1 to L2 message");
            var l1ToL2Message = l1ToL2Messages[0];

            Assert.That(l1ToL2Message.MessageData.DestAddress, Is.EqualTo(destWalletAddress), "Message destination address mismatch");
            Assert.That(l1ToL2Message.MessageData.L2CallValue, Is.EqualTo(ethToDeposit), "Message value mismatch");

            var retryableTicketResult = await l1ToL2Message.WaitForStatus();
            Assert.That(retryableTicketResult.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED), "Retryable ticket not redeemed");

            var retryableTxReceipt = await l2Provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(l1ToL2Message.RetryableCreationId);

            Assert.That(retryableTxReceipt, Is.Not.Null, "Retryable transaction receipt not found");

            var l2RetryableTxReceipt = new L2TransactionReceipt(retryableTxReceipt);
            var ticketRedeemEvents = (await l2RetryableTxReceipt.GetRedeemScheduledEvents(l2Provider)).ToList();

            Assert.That(ticketRedeemEvents.Count, Is.EqualTo(1), "Failed finding the redeem event");
            Assert.That(ticketRedeemEvents[0].Event.RetryTxHash, Is.Not.Null, "Retry transaction hash not found");

            var testWalletL2EthBalance = await l2Provider.Eth.GetBalance.SendRequestAsync(destWalletAddress);
            Assert.That(testWalletL2EthBalance.Value, Is.EqualTo(ethToDeposit), "Final balance mismatch");
        }

        [Test]
        public async Task TestWithdrawEtherTransactionSucceeds()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l2Signer = setupState.L2Signer;
            var l1Signer = setupState.L1Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;
            var ethBridger = setupState.EthBridger;

            await TestHelpers.FundL2(setupState.L2Deployer.Provider, address: l2Signer.Account.Address);
            await Task.Delay(500);
            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

            var ethToWithdraw = Web3.Convert.ToWei(0.00000002m, UnitConversion.EthUnit.Ether);

            var privateKey = EthECKey.GenerateKey().GetPrivateKey();
            var account = new Account(privateKey);
            var randomAddress = account.Address;

            var request = await ethBridger.GetWithdrawalRequest(new EthWithdrawParams
            {
                Amount = ethToWithdraw,
                DestinationAddress = randomAddress,
                From = l2Signer.Account.Address,
                L2Signer = l2Signer
            });

            var l1GasEstimate = await request.EstimateL1GasLimit(l2Provider);

            var withdrawEthRec = await ethBridger.Withdraw(new EthWithdrawParams
            {
                Amount = ethToWithdraw,
                L2Signer = l2Signer,
                DestinationAddress = randomAddress,
                From = l2Signer.Account.Address
            });

            Assert.That(withdrawEthRec.Status.Value, Is.EqualTo(BigInteger.One), "Initiate ETH withdraw transaction failed");

            var withdrawMessage = (await withdrawEthRec.GetL2ToL1Messages(l1Signer)).FirstOrDefault();

            Assert.That(withdrawMessage, Is.Not.Null, "ETH withdraw getWithdrawalsInL2Transaction query came back empty");

            var withdrawEvents = await L2ToL1Message.GetL2ToL1Events(
                l2Provider: l2Provider,
                filter: new NewFilterInput()
                {
                    FromBlock = new BlockParameter(withdrawEthRec.BlockNumber),
                    ToBlock = BlockParameter.CreateLatest()
                },
                position: null,
                destination: randomAddress
            );

            // Not to be tested on local node
            //Assert.That(withdrawEvents.Count, Is.EqualTo(1), "ETH withdraw getL2ToL1EventData failed");

            var web3 = new SignerOrProvider(l2Provider);
            var messageStatus = await withdrawMessage.StatusBase(web3);

            Assert.That(messageStatus, Is.EqualTo(L2ToL1MessageStatus.UNCONFIRMED), $"ETH withdraw status returned {messageStatus}");

            string mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString();

            var miner1Seed = new Wallet(mnemonic, "");
            var miner2Seed = new Wallet(mnemonic, "");

            var miner1PrivateKey = miner1Seed.GetPrivateKey(0);
            var miner2PrivateKey = miner2Seed.GetPrivateKey(0);

            var miner1Account = new Account(miner1PrivateKey);
            var miner2Account = new Account(miner2PrivateKey);

            var miner1 = new SignerOrProvider(miner1Account, l1Provider);
            var miner2 = new SignerOrProvider(miner2Account, l2Provider);

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: miner1.Account.Address);
            await TestHelpers.FundL2(setupState.L2Deployer.Provider, address: miner2.Account.Address);

            var state = new Dictionary<string, object> { { "mining", true } };

            await Task.WhenAny(
                TestHelpers.MineUntilStop(miner1, state),
                TestHelpers.MineUntilStop(miner2, state),
                withdrawMessage.WaitUntilReadyToExecuteBase(new SignerOrProvider(l2Provider))
            );

            state["mining"] = false;

        /* Not to be tested on local node
            Assert.That(
                await withdrawMessage.StatusBase(new SignerOrProvider(l2Provider)),
                Is.EqualTo(L2ToL1MessageStatus.CONFIRMED),
                "Message status should be confirmed"
            );

            var execTx = await withdrawMessage.Execute(l2Provider);

            Assert.That(execTx.GasUsed.Value, Is.LessThan(l1GasEstimate), "Gas used greater than estimate");

            Assert.That(
                await withdrawMessage.StatusBase(new SignerOrProvider(l2Provider)),
                Is.EqualTo(L2ToL1MessageStatus.EXECUTED),
                "Message status should be executed"
            );

            var finalRandomBalance = await l1Provider.Eth.GetBalance.SendRequestAsync(randomAddress);
            Assert.That(finalRandomBalance.Value, Is.EqualTo(ethToWithdraw), "L1 final balance mismatch");
        */
        }
    }
}