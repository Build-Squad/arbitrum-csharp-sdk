using System.Threading.Tasks;
using NUnit.Framework;
using Nethereum.Web3;
using Arbitrum.Message;
using Nethereum.Util;
using static Arbitrum.Message.L1ToL2MessageUtils;
using System.Numerics;
using Arbitrum.Scripts;
using Arbitrum.Tests.Integration;
using Arbitrum.DataEntities;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class TestRetryableTicketCreation
    {
        private readonly BigInteger TEST_AMOUNT = Web3.Convert.ToWei("0.01", UnitConversion.EthUnit.Ether);

        public async Task<TestState> SetupState()
        {
            var setupState = await TestSetupUtils.TestSetup();
            return setupState;
        }

        [Test]
        [Category("Async")]
        public async Task TestRetryableTicketCreationWithParameters()
        {
            var setupState = await SetupState();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;
            var signerAddress = l1Signer.Account.Address;
            var arbProvider = l2Signer.Provider;

            await TestHelpers.FundL1(l1Signer);

            var l1ToL2MessageCreator = new L1ToL2MessageCreator(l1Signer);

            var initialL2Balance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);

            var retryableTicketParams = new
            {
                from = signerAddress,
                to = signerAddress,
                l2CallValue = TEST_AMOUNT,
                callValueRefundAddress = signerAddress,
                data = "0x"
            };

            var l1SubmissionTxReceipt = await l1ToL2MessageCreator.CreateRetryableTicket(retryableTicketParams, arbProvider);
            var l1ToL2Messages = (await l1SubmissionTxReceipt.GetL1ToL2Messages(arbProvider)).ToList();

            Assert.That(l1ToL2Messages.Count(), Is.EqualTo(1));
            var l1ToL2Message = l1ToL2Messages[0];

            var retryableTicketResult = await l1ToL2Message.WaitForStatus();
            Assert.That(retryableTicketResult.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));
            var finalL2Balance = l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);
            Assert.That(finalL2Balance, Is.GreaterThan(initialL2Balance));
        }

        [Test]
        [Category("Async")]
        public async Task TestRetryableTicketCreationWithRequest()
        {
            var setupState = await SetupState();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var signerAddress = l1Signer.Account.Address;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;
            var ethProvider = l1Signer.Provider;
            var arbProvider = l2Signer.Provider;

            await TestHelpers.FundL1(l1Signer);

            var l1ToL2MessageCreator = new L1ToL2MessageCreator(l1Signer);

            var initialL2Balance = l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);

            var l1ToL2TransactionRequestParams = new L1ToL2MessageParams
            {
                From = signerAddress,
                To = signerAddress,
                L2CallValue = TEST_AMOUNT,
                CallValueRefundAddress = signerAddress,
                Data = "0x".HexToByteArray()
            };

            var l1ToL2TransactionRequest = await L1ToL2MessageCreator.GetTicketCreationRequest(l1ToL2TransactionRequestParams, ethProvider, arbProvider);

            var l1SubmissionTxReceipt = await l1ToL2MessageCreator.CreateRetryableTicket(l1ToL2TransactionRequest, arbProvider);

            var l1ToL2Messages = (await l1SubmissionTxReceipt.GetL1ToL2Messages(arbProvider)).ToList();
            Assert.That(l1ToL2Messages.Count(), Is.EqualTo(1));
            var l1ToL2Message = l1ToL2Messages[0];

            var retryableTicketResult = await l1ToL2Message.WaitForStatus();
            Assert.That(retryableTicketResult.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));

            var finalL2Balance = l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);
            Assert.That(finalL2Balance, Is.GreaterThan(initialL2Balance));
        }
    }
}
