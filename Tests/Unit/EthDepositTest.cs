using System;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Util;
using Arbitrum.DataEntities;
using Nethereum.JsonRpc.Client;
using static Arbitrum.DataEntities.NetworkUtils;
using Arbitrum.AssetBridgerModule;
using NUnit.Framework;
using Arbitrum.Tests.Integration;
using Arbitrum.Scripts;
using Newtonsoft.Json;


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
            var walletPrivateKey = Environment.GetEnvironmentVariable("DEVNET_PRIVKEY");
            var l1RpcUrl = Config.ETH_URL; //Environment.GetEnvironmentVariable("L1RPC");
            var l2RpcUrl = Config.ARB_URL; //Environment.GetEnvironmentVariable("L2RPC");

            var account = new Account(walletPrivateKey);
            var senderAddress = account.Address;


            var l1Provider = new Web3(account, l1RpcUrl);
            var l2Provider = new Web3(account, l2RpcUrl);

            //account.TransactionManager.Client = l1Client;
            var l1Signer = new SignerOrProvider(account, l1Provider);

            // Set the amount to be deposited in L2 (in wei)
            var ethToL2DepositAmount = UnitConversion.Convert.ToWei(0.0001m);

            // Add the default local network configuration to the SDK
            // to allow this script to run on a local node
            AddDefaultLocalNetwork();

            // Use l2Network to create an Arbitrum SDK EthBridger instance
            // We'll use EthBridger for its convenience methods around transferring ETH to L2

            var l2Network = AddDefaultLocalNetwork().l2Network; //await GetL2Network(l2Provider);
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

            // Get the final balance of the sender wallet on L1
            var senderL1BalanceFinal = await l1Provider.Eth.GetBalance.SendRequestAsync(account.Address);
            Console.WriteLine($"Sender L1 Balance now is: {Web3.Convert.FromWei(senderL1Balance)} ETH");

            // Get the final balance of the receiver wallet on L1
            var receiverL1BalanceFinal = await l1Provider.Eth.GetBalance.SendRequestAsync(receiverAddress);
            Console.WriteLine($"Receiver L1 Balance: {Web3.Convert.FromWei(receiverL1Balance)} ETH");

            Console.WriteLine($"Your L2 ETH balance is updated from {receiverL1Balance.ToString()} to {receiverL1BalanceFinal.ToString()}");
            Assert.That(receiverL1BalanceFinal.Value, Is.EqualTo(receiverL1Balance.Value+ethToL2DepositAmount));
        }
    }
}