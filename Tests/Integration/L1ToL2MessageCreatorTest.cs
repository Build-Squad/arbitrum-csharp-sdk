using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Web3;
using NUnit.Framework;
using System.Numerics;
using static Arbitrum.Message.L1ToL2MessageUtils;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class L1ToL2MessageCreatorTest
    {
        private readonly BigInteger TEST_AMOUNT = Web3.Convert.ToWei("0.01", UnitConversion.EthUnit.Ether);

        [Test]
        [Category("Async")]
        public async Task L1ToL2MessageCreationWithParameters()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var signerAddress = l1Signer.Account.Address;
            var arbProvider = l2Signer.Provider;

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

            var initialL2Balance = await l2Signer.Provider.Eth.GetBalance.SendRequestAsync(l2Signer?.Account?.Address);

            var retryableTicketParams = new L1ToL2MessageParams
            {
                From = signerAddress,
                To = signerAddress,
                L2CallValue = TEST_AMOUNT,
                CallValueRefundAddress = signerAddress,
                Data = "0x".HexToByteArray()
            };

            var l1ToL2MessageCreator = new L1ToL2MessageCreator(l1Signer);

            var l1SubmissionTxReceipt = await l1ToL2MessageCreator.CreateRetryableTicket(retryableTicketParams, arbProvider);
            var l1ToL2Messages = await l1SubmissionTxReceipt.GetL1ToL2Messages(arbProvider, l1SubmissionTxReceipt.ContractAddress);

            Assert.That(l1ToL2Messages.Count(), Is.EqualTo(1));

            /* 
            // Message status to be tested only on live networks. Local node doesn't have support for it
            var l1ToL2Message = l1ToL2Messages.FirstOrDefault();
            var retryableTicketResult = await l1ToL2Message.WaitForStatus();
            Assert.That(retryableTicketResult.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));
            */

            Task.Delay(TimeSpan.FromSeconds(3)).Wait();

            var finalL2Balance = await l2Signer.Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);

            Assert.That(finalL2Balance.Value, Is.GreaterThan(initialL2Balance.Value));
        }

        [Test]
        [Category("Async")]
        public async Task L1ToL2MessageCreationWithRequest()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var signerAddress = l1Signer.Account.Address;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

            var l1ToL2MessageCreator = new L1ToL2MessageCreator(l1Signer);

            var initialL2Balance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);

            var l1ToL2TransactionRequestParams = new L1ToL2MessageParams
            {
                From = signerAddress,
                To = signerAddress,
                L2CallValue = TEST_AMOUNT,
                CallValueRefundAddress = signerAddress,
                Data = "0x".HexToByteArray()
            };

            var l1ToL2TransactionRequest = await L1ToL2MessageCreator.GetTicketCreationRequest(l1ToL2TransactionRequestParams, l1Provider, l2Provider);

            var l1SubmissionTxReceipt = await l1ToL2MessageCreator.CreateRetryableTicket(l1ToL2TransactionRequest, l2Provider);

            var l1ToL2Messages = await l1SubmissionTxReceipt.GetL1ToL2Messages(l2Provider, l1SubmissionTxReceipt.ContractAddress);

            Assert.That(l1ToL2Messages.Count(), Is.EqualTo(1));
            /* 
            // Message status to be tested only on live networks. Local node doesn't have support for it
            var l1ToL2Message = l1ToL2Messages.FirstOrDefault();
            var retryableTicketResult = await l1ToL2Message.WaitForStatus();
            Assert.That(retryableTicketResult.Status, Is.EqualTo(L1ToL2MessageStatus.REDEEMED));
            */

            Task.Delay(TimeSpan.FromSeconds(3)).Wait();

            var finalL2Balance = await l2Provider.Eth.GetBalance.SendRequestAsync(l2Signer.Account.Address);

            Assert.That(finalL2Balance.Value, Is.GreaterThan(initialL2Balance.Value));
        }
    }
}
