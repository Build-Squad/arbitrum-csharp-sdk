﻿using System;
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
using Nethereum.RPC.Eth.DTOs;
using Arbitrum.Utils;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Arbitrum.Message.Tests.Integration
{
    [TestFixture]
    public class SendL2MessageTest
    {
        public class TxResult
        {
            public string? SignedMsg { get; set; }
            public TransactionReceipt? L1TransactionReceipt { get; set; }
        }
        private async Task<TxResult> SendSignedTx(TestState testState, TransactionInput info = null)
        {
            var l1Deployer = testState.L1Deployer;
            var l2Deployer = testState.L2Deployer;
            var l1Provider = new Web3(l1Deployer.TransactionManager.Client);
            var l2Provider = new Web3(l2Deployer.TransactionManager.Client);
            var l2Network = await NetworkUtils.GetL2NetworkAsync(l2Provider.Eth.ChainId);
            var inbox = new InboxTools(l1Deployer, l2Network);

            var message = new TransactionRequest
            {
                Value = Web3.Convert.ToWei(0, UnitConversion.EthUnit.Ether),
                From = info.From,
                MaxFeePerGas = info.MaxFeePerGas,
                MaxPriorityFeePerGas = info.MaxPriorityFeePerGas,
                AccessList = info.AccessList,
                ChainId = info.ChainId,
                Data = info.Data,
                Gas = info.Gas,
                GasPrice = info.GasPrice,
                Nonce = info.Nonce,
                To = info.To,
                Type = info.Type
            };

            var signedTx = await inbox.SignL2Tx(message, l2Deployer);
            var l1Tx = await inbox.SendL2SignedTx(signedTx);

            return new TxResult
            {
                SignedMsg = signedTx,
                L1TransactionReceipt = l1Tx
            };
        }

        private (string, string) ReadGreeterContract()
        {
            var contractData = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("tests/integration/helper/greeter.json"));

            if (contractData.abi == null)
            {
                throw new Exception("No ABI found for contract greeter");
            }

            var abi = contractData.abi.ToObject<string>();
            var bytecode = contractData.bytecode_hex;

            return (abi, bytecode);
        }

        [Test]
        public async Task CanDeployContract(TestState testState)
        {
            var l2Deployer = testState.L2Deployer;
            var l2Provider = new Web3(l2Deployer.TransactionManager.Client);
            var (abi, bytecode) = ReadGreeterContract();
            var greeterContract = l2Provider.Eth.GetContract(abi, bytecode);

            var constructTxn = greeterContract.GetFunction("constructor").CreateTransactionInput(from: l2Deployer.Address,functionInput: new TransactionInput() { Value = new HexBigInteger(0) });
            var returnData = await SendSignedTx(testState, constructTxn);
            var l1TransactionReceipt = returnData.L1TransactionReceipt;
            var signedMsg = returnData.SignedMsg;

            Assert.That(l1TransactionReceipt.Status, Is.EqualTo(1), "L1 transaction failed");

            var l2TxHash = await l2Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedMsg);
            var l2Tx = await l2Provider.Eth.Transactions.GetTransactionByHash.SendRequestAsync(l2TxHash);
            var l2TxReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2TxHash);

            Assert.That(l2TxReceipt.Status, Is.EqualTo(1), "L2 transaction failed");

            var senderAddress = l2Tx.From;
            var nonce = l2Tx.Nonce;

            var contractAddress = LoadContractUtils.GetContractAddress(senderAddress, nonce);

            var greeter = l2Provider.Eth.GetContract(abi: abi,contractAddress: AddressUtil.Current.ConvertToChecksumAddress(contractAddress));
            var greetResult = await greeter.GetFunction("greet").CallAsync<string>();

            Assert.That(greetResult, Is.EqualTo("hello world"), "Contract returned unexpected value");
        }

        [Test]
        public async Task ShouldConfirmSameTxOnL2(TestState testState)
        {
            var l2Deployer = testState.L2Deployer;
            var l2Provider = new Web3(l2Deployer.TransactionManager.Client);
            var returnData = await SendSignedTx(testState, new TransactionInput { Data = "0x12", To = l2Deployer.Address });
            var l1TransactionReceipt = returnData.L1TransactionReceipt;
            var signedMsg = returnData.SignedMsg;

            Assert.That(l1TransactionReceipt.Status, Is.EqualTo(1), "L1 transaction failed");

            var l2TxHash = await l2Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedMsg);
            var l2Tx = await l2Provider.Eth.Transactions.GetTransactionByHash.SendRequestAsync(l2TxHash);
            var l2TxReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2TxHash);

            Assert.That(l2TxReceipt.Status, Is.EqualTo(1), "L2 transaction failed");
        }

        [Test]
        public async Task SendTwoTxShareSameNonce(TestState testState)
        {
            var l2Deployer = testState.L2Deployer;
            var l2Provider = new Web3(l2Deployer.TransactionManager.Client);

            var currentNonce = await l2Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(testState.L2Deployer.Address);
            var lowFeeInfo = new TransactionInput { Data = "0x12", Nonce = currentNonce, To = testState.L2Deployer.Address, MaxFeePerGas = new HexBigInteger(10000000), MaxPriorityFeePerGas = new HexBigInteger(10000000) };
            var lowFeeTxData = await SendSignedTx(testState, lowFeeInfo);
            
            Assert.That(lowFeeTxData.L1TransactionReceipt.Status, Is.EqualTo(1), "L1 transaction (low fee) failed");

            var enoughFeeInfo = new TransactionInput{ Data = "0x12", To = testState.L2Deployer.Address, Nonce = currentNonce };
            var enoughFeeTxData = await SendSignedTx(testState, enoughFeeInfo);

            Assert.That(enoughFeeTxData.L1TransactionReceipt.Status, Is.EqualTo(1), "L1 transaction (enough fee) failed");

            var l2LowFeeTxHash = await l2Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(lowFeeTxData.SignedMsg);
            var l2EnoughFeeTxHash = await l2Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(enoughFeeTxData.SignedMsg);
            //var l2Tx = await l2Provider.Eth.Transactions.GetTransactionByHash.SendRequestAsync(l2LowFeeTxHash);

            var l2EnoughFeeReceipt = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2EnoughFeeTxHash);

            Assert.That(l2EnoughFeeReceipt.Status, Is.EqualTo(1), "L2 transaction (enough fee) failed");
            try
            {
                await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(l2LowFeeTxHash);
                Assert.Fail("Expected RpcResponseException was not thrown");
            }
            catch(RpcResponseException ex)
            {
                if (!ex.Message.Contains("Transaction not found"))
                {
                    // If the exception message does not contain the expected error message,
                    // rethrow the exception to fail the test
                    throw;
                }
            }
        }
    }
}
