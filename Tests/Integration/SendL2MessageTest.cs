using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Arbitrum.AssetBridger.Tests.Integration;
using Arbitrum.DataEntities;
using static Arbitrum.Inbox.ForceInclusionParams;
using Arbitrum.Scripts;
using Nethereum.Util;
using System.Net.WebSockets;

namespace Arbitrum.Message.Tests.Integration
{
    [TestFixture]
    public class SendL2MessageTest
    {
        private async Task<dynamic> SendSignedTx(TestState testState, object info = null)
        {
            var l1Deployer = testState.L1Deployer;
            var l2Deployer = testState.L2Deployer;
            var l1Provider = new Web3(l1Deployer.TransactionManager.Client);
            var l2Provider = new Web3(l2Deployer.TransactionManager.Client);
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider.Eth.ChainId);
            var inbox = new InboxTools(l1Deployer, l2Network);

            var message = new
            {
                value = Web3.Convert.ToWei(0, UnitConversion.EthUnit.Ether),
                ...info
            };

            var signedTx = await inbox.SignL2Tx(message, l2Deployer);
            var l1Tx = await inbox.SendL2SignedTx(signedTx);

            return new
            {
                SignedMsg = signedTx,
                L1TransactionReceipt = l1Tx
            };
        }

        private (string[], string) ReadGreeterContract()
        {
            var contractData = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("tests/integration/helper/greeter.json"));

            if (contractData.abi == null)
            {
                throw new Exception("No ABI found for contract greeter");
            }

            var abi = contractData.abi.ToObject<string[]>();
            var bytecode = contractData.bytecode_hex;

            return (abi, bytecode);
        }

        [Test]
        public async Task CanDeployContract(TestState testState)
        {
            var l2Deployer = testState.L2Deployer;
            var (abi, bytecode) = ReadGreeterContract();
            var greeterContract = new Contract(null, abi, null, null, null);

            var constructTxn = greeterContract.GetFunction("constructor").CreateTransactionInput(new HexBigInteger(0));
            var returnData = await SendSignedTx(testState, constructTxn);
            var l1TransactionReceipt = returnData.L1TransactionReceipt;
            var signedMsg = returnData.SignedMsg;

            Assert.That(l1TransactionReceipt.status, Is.EqualTo(1), "L1 transaction failed");

            var l2Tx = ParseRawTx(signedMsg.RawTransaction);
            var l2TxHash = l2Tx.hash;
            var l2TxReceipt = l2Deployer.Provider.Eth.WaitForTransactionReceiptAsync(l2TxHash).Result;

            Assert.That(l2TxReceipt.status, Is.EqualTo(1), "L2 transaction failed");

            var senderAddress = l2Tx.from;
            var nonce = l2Tx.nonce;

            var contractAddress = GetContractAddress(senderAddress, nonce);

            var greeter = new Contract(null, abi, null, contractAddress, null);
            var greetResult = greeter.GetFunction("greet").CallAsync<string>().Result;

            Assert.That(greetResult, Is.EqualTo("hello world"), "Contract returned unexpected value");
        }

        [Test]
        public async Task ShouldConfirmSameTxOnL2(TestState testState)
        {
            var returnData = await SendSignedTx(testState, new { data = "0x12", to = testState.L2Deployer.Account.Address });
            var l1TransactionReceipt = returnData.L1TransactionReceipt;
            var signedMsg = returnData.SignedMsg;

            Assert.That(l1TransactionReceipt.status, Is.EqualTo(1), "L1 transaction failed");

            var l2Tx = ParseRawTx(signedMsg.RawTransaction);
            var l2TxHash = l2Tx.hash;
            var l2TxReceipt = testState.L2Deployer.Provider.Eth.WaitForTransactionReceiptAsync(l2TxHash).Result;

            Assert.That(l2TxReceipt.status, Is.EqualTo(1), "L2 transaction failed");
        }

        [Test]
        public async Task SendTwoTxShareSameNonce(TestState testState)
        {
            var currentNonce = testState.L2Deployer.Provider.Eth.GetTransactionCount.SendRequestAsync(testState.L2Deployer.Account.Address).Result;
            var lowFeeInfo = new { data = "0x12", nonce = currentNonce, to = testState.L2Deployer.Account.Address, maxFeePerGas = 10000000, maxPriorityFeePerGas = 10000000 };
            var lowFeeTxData = await SendSignedTx(testState, lowFeeInfo);
            Assert.That(lowFeeTxData.L1TransactionReceipt.status, Is.EqualTo(1), "L1 transaction (low fee) failed");

            var enoughFeeInfo = new { data = "0x12", to = testState.L2Deployer.Account.Address, nonce = currentNonce };
            var enoughFeeTxData = await SendSignedTx(testState, enoughFeeInfo);
            Assert.That(enoughFeeTxData.L1TransactionReceipt.status, Is.EqualTo(1), "L1 transaction (enough fee) failed");

            var l2LowFeeTxHash = ParseRawTx(lowFeeTxData.SignedMsg.RawTransaction).hash;
            var l2EnoughFeeTxHash = ParseRawTx(enoughFeeTxData.SignedMsg.RawTransaction).hash;

            var l2EnoughFeeReceipt = testState.L2Deployer.Provider.Eth.WaitForTransactionReceiptAsync(l2EnoughFeeTxHash).Result;
            Assert.That(l2EnoughFeeReceipt.status, Is.EqualTo(1), "L2 transaction (enough fee) failed");

            Assert.Throws<TransactionNotFound>(() => testState.L2Deployer.Provider.Eth.GetTransactionReceipt.SendRequestAsync(l2LowFeeTxHash));
        }
    }
}
