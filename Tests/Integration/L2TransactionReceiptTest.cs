using NUnit.Framework;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using System.Threading;
using Nethereum.Signer;
using YourNamespace.Lib.SignerOrProvider;
using YourNamespace.Scripts;
using YourNamespace.Tests.Integration.Helpers;
using Arbitrum.DataEntities;
using Arbitrum.Message;

namespace YourNamespace.Tests.Integration
{
    [TestFixture]
    public class L2TransactionTests
    {
        private Web3 l1Provider;
        private Web3 l2Provider;
        private Account l1Signer;
        private Account l2Signer;

        private const ulong AMOUNT_TO_SEND = 5_000;

        [SetUp]
        public async Task Setup()
        {
            var setupState = await TestSetup.TestSetup();
            l1Provider = setupState.L1Signer.Provider;
            l2Provider = setupState.L2Signer.Provider;
            l1Signer = setupState.L1Signer.Account;
            l2Signer = setupState.L2Signer.Account;

            await FundL1(l1Signer, Web3.Convert.ToWei(1, Unit.Eth));
            await FundL2(l2Signer, Web3.Convert.ToWei(1, Unit.Eth));
        }

        [Test]
        public async Task FindL1BatchInfo()
        {
            var miner1Seed = EthECKey.GenerateKey();
            var miner2Seed = EthECKey.GenerateKey();

            var miner1Account = new Account(miner1Seed.GetPrivateKey());
            var miner2Account = new Account(miner2Seed.GetPrivateKey());

            var miner1 = new SignerOrProvider(miner1Account, l1Provider);
            var miner2 = new SignerOrProvider(miner2Account, l2Provider);

            await FundL1(miner1, Web3.Convert.ToWei(1, Unit.Eth));
            await FundL2(miner2, Web3.Convert.ToWei(1, Unit.Eth));

            var miningCancellation = new CancellationTokenSource();
            var miner1Task = MineUntilStop(miner1, miningCancellation.Token);
            var miner2Task = MineUntilStop(miner2, miningCancellation.Token);

            await FundL2(l2Signer);

            var randomAddress = EthECKey.GenerateKey().GetPublicAddress();

            var tx = new Transaction
            {
                From = l2Signer.Address,
                To = randomAddress,
                Value = new HexBigInteger(AMOUNT_TO_SEND)
            };

            var txHash = await l2Provider.Eth.TransactionManager.SendTransactionAsync(tx);
            var receipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

            while (true)
            {
                await Task.Delay(300);
                var arbTxReceipt = new L2TransactionReceipt(receipt);
                try
                {
                    var l1BatchNumber = await arbTxReceipt.GetBatchNumber(l2Provider);
                }
                catch
                {
                    // handle ContractLogicError
                }

                var l1BatchConfirmations = arbTxReceipt.GetBatchConfirmations(l2Provider);

                if (l1BatchNumber > 0)
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

            miningCancellation.Cancel();
            await Task.WhenAll(miner1Task, miner2Task);
        }
    }
}
