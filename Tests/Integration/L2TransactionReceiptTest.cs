using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NUnit.Framework;
using System.Numerics;

namespace Arbitrum.Tests.Integration
{
    public class L2TransactionReceiptTests
    {
        private static readonly BigInteger AMOUNT_TO_SEND = Web3.Convert.ToWei(0.000005, UnitConversion.EthUnit.Ether);

        [Test]
        public async Task TestFindL1BatchInfo()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;

            var miner1Seed = EthECKey.GenerateKey();
            var miner2Seed = EthECKey.GenerateKey();

            var miner1PrivateKey = miner1Seed.GetPrivateKey();
            var miner2PrivateKey = miner2Seed.GetPrivateKey();

            var miner1Account = new Account(miner1PrivateKey);
            var miner2Account = new Account(miner2PrivateKey);

            var miner1 = new SignerOrProvider(miner1Account, l1Signer.Provider);
            var miner2 = new SignerOrProvider(miner2Account, l2Signer.Provider);

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, Web3.Convert.ToWei(0.1, UnitConversion.EthUnit.Ether), miner1.Account.Address);
            await TestHelpers.FundL2(setupState.L2Deployer.Provider, Web3.Convert.ToWei(0.1, UnitConversion.EthUnit.Ether), miner2.Account.Address);
            var state = new Dictionary<string, object> { { "mining", true } };

            await TestHelpers.FundL2(setupState.L2Deployer.Provider, Web3.Convert.ToWei(0.1, UnitConversion.EthUnit.Ether), l2Signer.Account.Address);

            var randomAddress = new Account(EthECKey.GenerateKey().GetPrivateKey()).Address;

            var tx = new TransactionRequest()
            {
                From = l2Signer.Account.Address,
                To = randomAddress,
                Value = AMOUNT_TO_SEND.ToHexBigInteger()
            };

            var txHash = await l2Signer.Provider.Eth.TransactionManager.SendTransactionAsync(tx);

            var receipt = await l2Signer.Provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txHash);

            while (true)
            {
                await Task.Delay(300);
                var arbTxReceipt = new L2TransactionReceipt(receipt);

                BigInteger l1BatchNumber;
                try
                {
                    l1BatchNumber = await arbTxReceipt.GetBatchNumber(l2Signer.Provider);
                }
                catch (Exception)
                {
                    l1BatchNumber = BigInteger.Zero;
                }

                var l1BatchConfirmations = await arbTxReceipt.GetBatchConfirmations(l2Signer);

                if (l1BatchNumber > BigInteger.Zero)
                {
                    Assert.That(l1BatchConfirmations, Is.GreaterThanOrEqualTo(BigInteger.Zero), "Missing confirmations");
                }

                if (l1BatchConfirmations > 8)
                {
                    Assert.That(l1BatchNumber, Is.GreaterThanOrEqualTo(BigInteger.Zero), "Missing confirmations");
                }

                if (l1BatchConfirmations > new BigInteger(8))
                {
                    break;
                }
            }
            
            state["mining"] = false;

        }
    }
}
