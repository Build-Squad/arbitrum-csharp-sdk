using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.Scripts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using NUnit.Framework;
using static Arbitrum.Inbox.ForceInclusionParams;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class SendL2MessageTest
    {
        [Test]
        public async Task CanDeployContract()
        {
            var testState = await TestSetupUtils.TestSetup();
            var l2Deployer = testState.L2Deployer;
            var l2Provider = l2Deployer.Provider;

            var contractByteCode = (await LogParser.LoadAbi("Greeter")).Item2;

            var currentNonce = await l2Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(l2Deployer.Account.Address, BlockParameter.CreatePending());
            var constructTxn = new TransactionRequest
            {
                Data = contractByteCode,
                Value = new HexBigInteger(0),
                Gas = await l2Deployer.Provider.Eth.GasPrice.SendRequestAsync(),
                MaxPriorityFeePerGas = new HexBigInteger(0),
                Nonce = currentNonce
            };

            var returnData = await SendSignedTx(testState, constructTxn);
            var l1TransactionReceipt = returnData.L1TransactionReceipt;
            var signedMsg = returnData.SignedMsg;

            Assert.That(l1TransactionReceipt.Status, Is.EqualTo(1.ToHexBigInteger()), "L1 transaction failed");

            var l2TxReceipt = await l2Provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(signedMsg);
            Assert.That(l2TxReceipt.Status, Is.EqualTo(1.ToHexBigInteger()), "L2 transaction failed");

            var greeterDeployment = new GreeterDeployment(contractByteCode);

            var txnReceiptDeployment = await l2Provider.Eth.GetContractDeploymentHandler<GreeterDeployment>().SendRequestAndWaitForReceiptAsync(greeterDeployment);            
            var contractHandler = l2Provider.Eth.GetContractHandler(txnReceiptDeployment.ContractAddress);
            var greetResult = await contractHandler.QueryAsync<GreetFunction, string>();

            Assert.That(greetResult, Is.EqualTo("Hello, World!"), "Contract returned unexpected value");
        }

        [Test]
        public async Task ShouldConfirmSameTxOnL2()
        {
            var testState = await TestSetupUtils.TestSetup();

            var l2Deployer = testState.L2Deployer;
            var l2Provider = l2Deployer.Provider;

            var currentNonce = await l2Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(l2Deployer.Account.Address, BlockParameter.CreatePending());
            var constructTxn = new TransactionRequest
            {
                Data = "0x12",
                Value = new HexBigInteger(0),
                Gas = await l2Deployer.Provider.Eth.GasPrice.SendRequestAsync(),
                MaxPriorityFeePerGas = new HexBigInteger(0),
                Nonce = currentNonce,
                To = l2Deployer.Account.Address
            };

            var returnData = await SendSignedTx(testState, constructTxn);
            var l1TransactionReceipt = returnData.L1TransactionReceipt;
            var signedMsg = returnData.SignedMsg;

            Assert.That(l1TransactionReceipt.Status, Is.EqualTo(1.ToHexBigInteger()), "L1 transaction failed");

            var l2TxReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(signedMsg);

            Assert.That(l2TxReceipt.Status, Is.EqualTo(1.ToHexBigInteger()), "L2 transaction failed");
        }

        [Test]
        public async Task SendTwoTxShareSameNonce()
        {
            var testState = await TestSetupUtils.TestSetup();

            var l2Deployer = testState.L2Deployer;
            var l2Provider = l2Deployer?.Provider;

            var currentNonce = await l2Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(l2Deployer.Account.Address, BlockParameter.CreatePending());
            var lowFeeInfo = new TransactionRequest
            {
                Data = "0x12",
                Nonce = currentNonce,
                To = testState.L2Deployer.Account.Address,
                Gas = await l2Deployer.Provider.Eth.GasPrice.SendRequestAsync(),
                MaxFeePerGas = new HexBigInteger(1000000000),
                MaxPriorityFeePerGas = new HexBigInteger(100000000)
            };

            var lowFeeTxData = await SendSignedTx(testState, lowFeeInfo);

            Assert.That(lowFeeTxData.L1TransactionReceipt.Status, Is.EqualTo(1.ToHexBigInteger()), "L1 transaction (low fee) failed");

            currentNonce = await l2Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(l2Deployer.Account.Address, BlockParameter.CreatePending());
            var enoughFeeInfo = new TransactionRequest
            {
                Data = "0x12",
                To = testState.L2Deployer.Account.Address,
                Nonce = currentNonce,
                Gas = await l2Deployer.Provider.Eth.GasPrice.SendRequestAsync()
            };

            var enoughFeeTxData = await SendSignedTx(testState, enoughFeeInfo);

            Assert.That(enoughFeeTxData.L1TransactionReceipt.Status, Is.EqualTo(1.ToHexBigInteger()), "L1 transaction (enough fee) failed");

            var l2EnoughFeeReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(enoughFeeTxData.SignedMsg);
            Assert.That(l2EnoughFeeReceipt.Status, Is.EqualTo(1.ToHexBigInteger()), "L2 transaction (enough fee) failed");

            var receipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(lowFeeTxData.SignedMsg);
            Assert.That(receipt.Logs.Count, Is.EqualTo(0));
        }

        public class TxResult
        {
            public string? SignedMsg { get; set; }
            public TransactionReceipt? L1TransactionReceipt { get; set; }
        }

        private static async Task<TxResult> SendSignedTx(TestState testState, TransactionRequest? info = null)
        {
            var l1Deployer = testState.L1Deployer;
            var l2Deployer = testState.L2Deployer;
            var l1Provider = l1Deployer.Provider;
            var l2Provider = l2Deployer.Provider;
            var chainId = await l2Deployer.Provider.Eth.ChainId.SendRequestAsync();
            var l2Network = await NetworkUtils.GetL2Network((int)(chainId).Value);
            var inbox = new InboxTools(l1Deployer, l2Network);

            var message = new TransactionRequest
            {
                Value = new HexBigInteger(Web3.Convert.ToWei(0, UnitConversion.EthUnit.Ether)),
                From = info.From,
                MaxFeePerGas = info.MaxFeePerGas ?? new HexBigInteger(200000000),
                MaxPriorityFeePerGas = info.MaxPriorityFeePerGas,
                AccessList = info.AccessList,
                ChainId = info.ChainId ?? chainId,
                Data = info.Data,
                Gas = info.Gas,
                GasPrice = info.GasPrice,
                Nonce = info.Nonce,
                To = info.To,
                Type = info.Type
            };

            var signedTx = await inbox.SignL2Tx(message, l2Deployer);
            var l2TxHash = await l2Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);
            var l1Tx = await inbox.SendL2SignedTx(signedTx);

            return new TxResult
            {
                SignedMsg = l2TxHash,
                L1TransactionReceipt = l1Tx
            };
        }
    }
}
