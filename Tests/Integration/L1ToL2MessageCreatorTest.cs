using NUnit.Framework;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using YourNamespace.Lib.Message;
using YourNamespace.Lib.MessageCreator;
using YourNamespace.Scripts;
using YourNamespace.Tests.Integration.Helpers;
using Arbitrum.Message;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace YourNamespace.Tests.Integration
{
    [TestFixture]
    public class L1ToL2MessageTests
    {
        private Web3 l1Provider;
        private Web3 l2Provider;
        private Account l1Signer;
        private Account l2Signer;

        [SetUp]
        public async Task Setup()
        {
            var setupState = await TestSetup.TestSetup();
            l1Provider = setupState.L1Signer.Provider;
            l2Provider = setupState.L2Signer.Provider;
            l1Signer = setupState.L1Signer.Account;
            l2Signer = setupState.L2Signer.Account;

            FundL1(l1Signer);
        }

        [Test]
        public async Task RetryableTicketCreationWithParameters()
        {
            var signerAddress = l1Signer.Address;
            var l1ToL2MessageCreator = new L1ToL2MessageCreator(l1Signer);

            var initialL2Balance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Address);

            var retryableTicketParams = new
            {
                from = signerAddress,
                to = signerAddress,
                l2CallValue = TEST_AMOUNT,
                callValueRefundAddress = signerAddress,
                data = "0x"
            };

            var l1SubmissionTxReceipt = await l1ToL2MessageCreator.CreateRetryableTicket(
                retryableTicketParams, l2Provider);

            var l1ToL2Messages = await l1SubmissionTxReceipt.GetL1ToL2Messages(l2Provider);

            Assert.That(l1ToL2Messages.Length, Is.EqualTo(1));
            var l1ToL2Message = l1ToL2Messages[0];

            var retryableTicketResult = await l1ToL2Message.WaitForStatus();
            Assert.That(retryableTicketResult.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));

            var finalL2Balance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Address);
            Assert.That(finalL2Balance, Is.GreaterThan(initialL2Balance + TEST_AMOUNT));
        }

        [Test]
        public async Task RetryableTicketCreationWithRequest()
        {
            var signerAddress = l1Signer.Address;
            var l1ToL2MessageCreator = new L1ToL2MessageCreator(l1Signer);

            var initialL2Balance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Address);

            var l1ToL2TransactionRequestParams = new
            {
                from = signerAddress,
                to = signerAddress,
                l2CallValue = TEST_AMOUNT,
                callValueRefundAddress = signerAddress,
                data = "0x"
            };

            var l1ToL2TransactionRequest = await L1ToL2MessageCreator.GetTicketCreationRequest(
                l1ToL2TransactionRequestParams, l1Provider, l2Provider);

            var l1SubmissionTxReceipt = await l1ToL2MessageCreator.CreateRetryableTicket(
                l1ToL2TransactionRequest, l2Provider);

            var l1ToL2Messages = await l1SubmissionTxReceipt.GetL1ToL2Messages(l2Provider);
            Assert.That(l1ToL2Messages.Length, Is.EqualTo(1));
            var l1ToL2Message = l1ToL2Messages[0];

            var retryableTicketResult = await l1ToL2Message.WaitForStatus();
            Assert.That(retryableTicketResult.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));

            var finalL2Balance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Address);
            Assert.That(finalL2Balance, Is.GreaterThan(initialL2Balance + TEST_AMOUNT));
        }
    }
}
