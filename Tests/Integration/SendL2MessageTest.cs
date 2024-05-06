using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using YourNamespace.Lib.DataEntities;
using YourNamespace.Lib.DataEntities.Networks;
using YourNamespace.Lib.Inbox;
using YourNamespace.Lib.Utils.Helper;
using YourNamespace.Scripts;
using Arbitrum.DataEntities;
using static Arbitrum.Inbox.ForceInclusionParams;

namespace YourNamespace.Tests.Integration
{
    [TestFixture]
    public class ContractTests
    {
        private Web3 l1Provider;
        private Web3 l2Provider;
        private Account l1Signer;
        private Account l2Signer;
        private Address erc20Bridger;

        [SetUp]
        public async Task Setup()
        {
            var testState = await TestSetup.TestSetup();
            l1Provider = testState.L1Deployer.Provider;
            l2Provider = testState.L2Deployer.Provider;
            l1Signer = testState.L1Deployer.Account;
            l2Signer = testState.L2Deployer.Account;
            erc20Bridger = testState.ERC20Bridger;
        }

        [Test]
        public async Task CanDeployContract()
        {
            var abiBytecode = ReadGreeterContract();
            var abi = abiBytecode["abi"];
            var bytecode = abiBytecode["bytecode_hex"];

            var greeterContract = new Contract(null, abi, null);
            greeterContract.ByteCode = bytecode;

            var constructTxn = greeterContract.GetFunction("constructor").CreateTransactionInput(new HexBigInteger(0));
            var returnData = await SendSignedTx(constructTxn);

            var l1TransactionReceipt = returnData["l1TransactionReceipt"];
            var signedMsg = returnData["signedMsg"];

            Assert.AreEqual(1, l1TransactionReceipt["status"], "L1 transaction failed");

            var l2Tx = ParseRawTx(signedMsg.RawTransaction);
            var l2TxHash = l2Tx["hash"];
            var l2TxReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2TxHash);

            Assert.AreEqual(1, l2TxReceipt["status"], "L2 transaction failed");

            var senderAddress = l2Tx["from"];
            var nonce = l2Tx["nonce"];

            var contractAddress = GetContractAddress(senderAddress, nonce);

            var greeter = new Contract(null, abi, contractAddress);
            var greetResult = await greeter.GetFunction("greet").CallAsync<string>();
            Assert.AreEqual("hello world", greetResult, "Contract returned unexpected value");
        }

        [Test]
        public async Task ShouldConfirmSameTxOnL2()
        {
            var info = new
            {
                data = "0x12",
                to = l2Signer.Address
            };

            var returnData = await SendSignedTx(info);
            var l1TransactionReceipt = returnData["l1TransactionReceipt"];
            var signedMsg = returnData["signedMsg"];

            Assert.AreEqual(1, l1TransactionReceipt["status"], "L1 transaction failed");

            var l2Tx = ParseRawTx(signedMsg.RawTransaction);
            var l2TxHash = l2Tx["hash"];
            var l2TxReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2TxHash);

            Assert.AreEqual(1, l2TxReceipt["status"], "L2 transaction failed");
        }

        [Test]
        public async Task SendTwoTxShareSameNonce()
        {
            var currentNonce = await l2Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(l2Signer.Address);

            var lowFeeInfo = new
            {
                data = "0x12",
                nonce = currentNonce,
                to = l2Signer.Address,
                maxFeePerGas = 10000000,
                maxPriorityFeePerGas = 10000000
            };

            var lowFeeTxData = await SendSignedTx(lowFeeInfo);
            Assert.AreEqual(1, lowFeeTxData["l1TransactionReceipt"]["status"], "L1 transaction (low fee) failed");

            var enoughFeeInfo = new
            {
                data = "0x12",
                to = l2Signer.Address,
                nonce = currentNonce
            };

            var enoughFeeTxData = await SendSignedTx(enoughFeeInfo);
            Assert.AreEqual(1, enoughFeeTxData["l1TransactionReceipt"]["status"], "L1 transaction (enough fee) failed");

            var l2LowFeeTxHash = ParseRawTx(lowFeeTxData["signedMsg"].RawTransaction)["hash"];
            var l2EnoughFeeTxHash = ParseRawTx(enoughFeeTxData["signedMsg"].RawTransaction)["hash"];

            var l2EnoughFeeReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2EnoughFeeTxHash);
            Assert.AreEqual(1, l2EnoughFeeReceipt["status"], "L2 transaction (enough fee) failed");

            Assert.ThrowsAsync<TransactionNotFoundException>(() => l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2LowFeeTxHash));
        }

        private async Task<dynamic> SendSignedTx(object info = null)
        {
            var l1Deployer = l1Signer;
            var l2Deployer = l2Signer;
            var l2Network = GetL2Network(l2Deployer.ChainId);
            var inbox = new InboxTools(l1Deployer, l2Network);

            var message = new
            {
                value = Web3.Convert.ToWei(0, "ether"),
                info
            };
            var signedTx = await inbox.SignL2Tx(message, l2Deployer);

            var l1Tx = await inbox.SendL2SignedTx(signedTx);
            return new
            {
                signedMsg = signedTx,
                l1TransactionReceipt = l1Tx
            };
        }

        private dynamic ReadGreeterContract()
        {
            var abiBytecode = System.IO.File.ReadAllText("tests/integration/helper/greeter.json");
            dynamic contractData = Newtonsoft.Json.JsonConvert.DeserializeObject(abiBytecode);
            if (contractData.abi == null)
            {
                throw new Exception("No ABI found for contract greeter");
            }

            return new
            {
                abi = contractData.abi,
                bytecode_hex = contractData.bytecode_hex
            };
        }

        private dynamic ParseRawTx(string rawTx)
        {
            // Implement your logic to parse raw transaction here
        }

        private Address GetContractAddress(string senderAddress, string nonce)
        {
            // Implement your logic to generate contract address here
        }
    }
}
