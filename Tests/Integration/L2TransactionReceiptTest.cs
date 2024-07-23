using System;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Arbitrum.Tests.Integration;
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

namespace Arbitrum.Tests.Integration
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

            // generate keys  
            var miner1Seed = EthECKey.GenerateKey();
            var miner2Seed = EthECKey.GenerateKey();

            // Get private keys as hex strings
            var miner1PrivateKey = miner1Seed.GetPrivateKey();
            var miner2PrivateKey = miner2Seed.GetPrivateKey();

            // Create accounts from private keys
            var miner1Account = new Account(miner1PrivateKey);
            var miner2Account = new Account(miner2PrivateKey);

            var miner1 = new SignerOrProvider(miner1Account, l1Signer.Provider);
            var miner2 = new SignerOrProvider(miner2Account, l2Signer.Provider);

            //await TestHelpers.FundL1(miner1, Web3.Convert.ToWei(1, UnitConversion.EthUnit.Ether));
            //await TestHelpers.FundL2(miner2, Web3.Convert.ToWei(1, UnitConversion.EthUnit.Ether));
            var state = new Dictionary<string, object> { { "mining", true } };

            //starts mining process
            var miner1Task = TestHelpers.MineUntilStop(miner1, state);
            var miner2Task = TestHelpers.MineUntilStop(miner2, state);

            await TestHelpers.FundL2(l2Signer);

            // Generate a new private key
            var privateKey = EthECKey.GenerateKey().GetPrivateKey();

            // Create a new account with the generated private key
            var account = new Account(privateKey);

            //get the random address
            var randomAddress = account.Address;

            var tx = new TransactionRequest()
            {
                From = l2Signer.Account.Address,
                To = randomAddress,
                Value = AMOUNT_TO_SEND.ToHexBigInteger()
            };

            var txHash = await l2Signer.Provider.Eth.TransactionManager.SendTransactionAsync(tx);

            var receipt = await l2Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            while (true)
            {
                await Task.Delay(300);
                var arbTxReceipt = new L2TransactionReceipt(receipt);
                BigInteger l1BatchNumber;
                try
                {
                    l1BatchNumber = await arbTxReceipt.GetBatchNumber(l2Signer.Provider.Client);
                }
                catch (SmartContractCustomErrorRevertException)
                {
                    l1BatchNumber = BigInteger.Zero;
                }

                var l1BatchConfirmations = await arbTxReceipt.GetBatchConfirmations(l2Signer.Provider);

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
