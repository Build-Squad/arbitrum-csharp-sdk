using System;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Arbitrum.Tests.Integration;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources;
using NBitcoin;
using Nethereum.Contracts;
using Nethereum.HdWallet;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NUnit.Framework;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static NBitcoin.Protocol.Behaviors.ChainBehavior;

namespace Arbitrum.Message.Tests.Integration
{
    public class L2TransactionReceiptTests
    {
        private static readonly BigInteger AMOUNT_TO_SEND = Web3.Convert.ToWei(0.000005m, UnitConversion.EthUnit.Ether);

        [Test]
        public async Task TestFindL1BatchInfo()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = new Web3(l1Signer.TransactionManager.Client);
            var l2Provider = new Web3(l2Signer.TransactionManager.Client);

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

            var miner1 = new SignerOrProvider(miner1Account, l1Provider);
            var miner2 = new SignerOrProvider(miner2Account, l2Provider);

            await TestHelpers.FundL1(miner1.Account, Web3.Convert.ToWei(1, UnitConversion.EthUnit.Ether));
            await TestHelpers.FundL2(miner2.Account, Web3.Convert.ToWei(1, UnitConversion.EthUnit.Ether));
            var state = new Dictionary<string, object> { { "mining", true } };

            //starts mining process
            var miner1Task = TestHelpers.MineUntilStop(miner1.Account, state);
            var miner2Task = TestHelpers.MineUntilStop(miner2.Account, state);

            await TestHelpers.FundL2(l2Signer);

            // Generate a new private key
            var privateKey = EthECKey.GenerateKey().GetPrivateKey();

            // Create a new account with the generated private key
            var account = new Account(privateKey);

            //get the random address
            var randomAddress = account.Address;

            var tx = new TransactionInput()
            {
                From = l2Signer.Address,
                To = randomAddress,
                Value = AMOUNT_TO_SEND.ToHexBigInteger()
            };

            var txHash = await l2Provider.Eth.TransactionManager.SendTransactionAsync(tx);

            var receipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            while (true)
            {
                await Task.Delay(300);
                var arbTxReceipt = new L2TransactionReceipt(receipt);
                BigInteger l1BatchNumber;
                try
                {
                    l1BatchNumber = await arbTxReceipt.GetBatchNumber(l2Provider.Client);
                }
                catch (SmartContractCustomErrorRevertException)
                {
                    l1BatchNumber = BigInteger.Zero;
                }

                var l1BatchConfirmations = await arbTxReceipt.GetBatchConfirmations(l2Provider);

                if (l1BatchNumber > BigInteger.Zero)
                {
                    Assert.That(l1BatchConfirmations, Is.GreaterThan(0), "Missing confirmations");
                }

                if (l1BatchConfirmations > 0)
                {
                    Assert.That(l1BatchNumber, Is.GreaterThan(0), "Missing batch number");
                }

                if (l1BatchConfirmations > 8)
                {
                    break;
                }
            }

            state["mining"] = false;

            //to stop the mining processes and clean up any associated resources.
            miner1Task.Dispose();
            miner2Task.Dispose();
        }
    }
}
