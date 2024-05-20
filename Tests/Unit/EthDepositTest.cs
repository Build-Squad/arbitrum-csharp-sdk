using System;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using Nethereum.RPC.Eth.DTOs;
using Arbitrum.AssetBridger;
using Arbitrum.Utils;
using Arbitrum.Message;
using Arbitrum.DataEntities;
using Arbitrum.Inbox;
using System.Security.Principal;
using Nethereum.JsonRpc.Client;
using static Arbitrum.DataEntities.NetworkUtils;
using Arbitrum.AssetBridgerModule;
using NUnit.Framework;
using Nethereum.HdWallet;
using Nethereum.Signer;
using Nethereum.Contracts;
using Nethereum.ABI.Model;


namespace Arbitrum.Tests.Unit
{
    [TestFixture]
    class EthDepositTesting
    {
        [Test]
        public async Task EthDepositTest()
        {
            Console.WriteLine("Deposit Eth via Arbitrum SDK");

            // Set up L1 / L2 wallets connected to providers
            var walletPrivateKey = "0xa7f8b3b7ffb6440756813c5f85d5d5a6c18d7ebbbb49887037b89380cbef5b88";//Environment.GetEnvironmentVariable("DEVNET_PRIVKEY");
            var l1RpcUrl = "http://127.0.0.1:8545"; //Environment.GetEnvironmentVariable("L1RPC");
            var l2RpcUrl = "https://sepolia-rollup.arbitrum.io/rpc"; //Environment.GetEnvironmentVariable("L2RPC");

            var l1Client = new RpcClient(new Uri(l1RpcUrl));

            var l1Provider = new Web3(l1RpcUrl);
            var l2Provider = new Web3(l2RpcUrl);

            var account = new Account(walletPrivateKey);
            var senderAddress = account.Address;

            //account.TransactionManager.Client = l1Client;
            var l1Signer = new SignerOrProvider(account, l1Provider);

            // Set the amount to be deposited in L2 (in wei)
            var ethToL2DepositAmount = UnitConversion.Convert.ToWei(0.0001m);

            // Add the default local network configuration to the SDK
            // to allow this script to run on a local node
            AddDefaultLocalNetwork();

            // Use l2Network to create an Arbitrum SDK EthBridger instance
            // We'll use EthBridger for its convenience methods around transferring ETH to L2
            var l2Network = await GetL2NetworkAsync(l2Provider);
            var ethBridger = new EthBridger(l2Network);
            var receiverAddress = l2Network?.EthBridge?.Inbox;

            // Get the initial balance of the sender wallet 
            var senderL1Balance = await l1Provider.Eth.GetBalance.SendRequestAsync(account.Address);
            Console.WriteLine($"Sender L1 Balance: {Web3.Convert.FromWei(senderL1Balance)} ETH");

            // Get the initial balance of the receiver wallet
            var receiverL1Balance = await l1Provider.Eth.GetBalance.SendRequestAsync(receiverAddress);
            Console.WriteLine($"Receiver L1 Balance: {Web3.Convert.FromWei(receiverL1Balance)} ETH");

            // Get the l2Wallet initial ETH balance
            //var l2WalletInitialEthBalance = await l2Provider.Eth.GetBalance.SendRequestAsync(account.Address);
            // Transfer ether from L1 to L2
            // This convenience method automatically queries for the retryable's max submission cost and forwards the appropriate amount to L2
            var depositTx = await ethBridger.Deposit(new EthDepositParams { L1Signer = l1Signer, Amount = ethToL2DepositAmount });
            
            // Display transaction receipt details
            Console.WriteLine($"Transaction Hash: {depositTx.TransactionHash}");
            Console.WriteLine($"Transaction was mined in block: {depositTx.BlockNumber.Value}");
            Console.WriteLine($"Transaction status: {(depositTx.Status.Value == 1 ? "Success" : "Failed")}");
            Console.WriteLine($"Gas used: {depositTx.GasUsed.Value}");
            Console.WriteLine($"Cumulative gas used: {depositTx.CumulativeGasUsed.Value}");

            // Get the initial balance of the receiver wallet on L1
            var receiverL1Balance1 = await l1Provider.Eth.GetBalance.SendRequestAsync(receiverAddress);
            Console.WriteLine($"Receiver L1 Balance: {Web3.Convert.FromWei(receiverL1Balance)} ETH");

            Console.WriteLine($"Your L2 ETH balance is updated from {receiverL1Balance.ToString()} to {receiverL1Balance1.ToString()}");
            Assert.That(receiverL1Balance1, Is.EqualTo(receiverL1Balance+ethToL2DepositAmount));
        }
    }
}
