# **Arbitrum C# SDK**

C# library for client-side interactions with Arbitrum. Arbitrum C# SDK provides common helper functionality as well as access to the underlying smart contract interfaces.

Below is an overview of the Arbitrum C# SDK functionality. See the [tutorials](https://github.com/Build-Squad/arbitrum-csharp-tutorials) for further examples of how to use these classes.

> **Note**: Arbitrum C# SDK is NOT official and currently in alpha. It is NOT recommended for production use. Go to [Arbitrum Typescript SDK](https://github.com/OffchainLabs/arbitrum-sdk) for official library.

### Quickstart Recipes

- ##### Deposit Ether Into Arbitrum

```
using System;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Arbitrum;
using Arbitrum.DataEntities;
using Arbitrum.AssetBridgerModule;
using Nethereum.Util;

class ArbitrumDeposit
{
    public static async Task DepositEtherAsync(string l2ChainId, string l1Url, string l2Url, string privateKey)
    {
        // Get the L2 network
        var l2Network = await NetworkUtils.GetL2Network(l2ChainId);

        // Create an instance of the EthBridger
        var ethBridger = new EthBridger(l2Network);

        // Create the L1 and L2 providers
        var l1Web3 = new Web3(l1Url);
        var l2Web3 = new Web3(l2Url);

        // Create an instance of the account using the private key
        var account = new Account(privateKey);

        // Set up the L1 signer
        var l1Signer = account;

        // Deposit Ether into Arbitrum
        var amountInEther = Web3.Convert.ToWei(23);
        var depositParams = new EthDepositParams
        {
            Amount = amountInEther,
            L1Signer = l1Signer,
            L2Provider = l2Web3
        };

        // Execute the deposit transaction
        var ethDepositTxResponse = await ethBridger.Deposit(depositParams);

        // Wait for the transaction to be mined and get the receipt
        var ethDepositTxReceipt = await l1Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(ethDepositTxResponse.TransactionHash);

        // Check the transaction receipt status
        if (ethDepositTxReceipt != null)
        {
            Console.WriteLine($"Transaction Receipt Status: {ethDepositTxReceipt.Status}");
            if (ethDepositTxReceipt.Status.Value == 1)
            {
                Console.WriteLine("Transaction successful");
            }
            else
            {
                Console.WriteLine("Transaction failed");
            }
        }
        else
        {
            Console.WriteLine("Transaction receipt not available");
        }
    }
}

```

- ##### Redeem an L1 to L2 Message

```
using System.Threading.Tasks;
using Arbitrum.Message;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using static Arbitrum.Message.L1ToL2MessageUtils;

class ArbitrumMessage
{
    public async Task RedeemL1ToL2MessageAsync(TransactionReceipt txnReceipt, string l2SignerPrivateKey)
    {
        // Create an instance of L1TransactionReceipt using the provided transaction receipt
        var l1TxnReceipt = new L1TransactionReceipt(txnReceipt);

        // Create an instance of an L2 signer using the provided private key
        var l2Signer = new Account(l2SignerPrivateKey);

        // Get the L1 to L2 message from the transaction receipt
        var l1ToL2Message = (await l1TxnReceipt.GetL1ToL2Messages(l2Signer)).FirstOrDefault() as L1ToL2MessageReader;

        // Wait for the message status
        var res = await l1ToL2Message?.WaitForStatus();

        if (res.Status == L1ToL2MessageStatus.FUNDS_DEPOSITED_ON_L2)
        {
            // Message wasn't auto-redeemed; redeem it now
            var receipt = await l1ToL2Message.GetAutoRedeemAttempt();
            Console.WriteLine("Message wasn't auto-redeemed; redeem it now");
        }
        else if (res.Status == L1ToL2MessageStatus.REDEEMED)
        {
            Console.WriteLine("Message successfully redeemed");
        }
    }
}
```

- ##### Check if sequencer has included a transaction in L1 data

```
using System;
using System.Threading.Tasks;
using Arbitrum.Message;
using Arbitrum.SDK;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

class ArbitrumSequencer
{
    public async Task CheckTransactionInL1DataAsync(TransactionReceipt txnReceipt, string l2ProviderUrl)
    {
        // Create an instance of L2TransactionReceipt using the provided transaction receipt
        var l2TxnReceipt = new L2TransactionReceipt(txnReceipt);

        // Create the L2 and L1 providers
        var l2Provider = new Web3(l2ProviderUrl);

        // Wait for 3 minutes
        await Task.Delay(3 * 60 * 1000);

        // Check if data is available on L1
        var dataIsOnL1 = await l2TxnReceipt.IsDataAvailable(l2Provider);

        if (dataIsOnL1)
        {
            Console.WriteLine("Sequencer has posted data, and it inherits full rollup/L1 security.");
        }
        else
        {
            Console.WriteLine("Data is not yet available on L1.");
        }
    }
}

```

### Bridging assets

Arbitrum C# SDK can be used to bridge assets to/from the rollup chain. The following asset bridgers are currently available:

- EthBridger
- Erc20Bridger

All asset bridgers have the following methods:

- **deposit** - moves assets from the L1 to the L2
- **deposit_estimate_gas** - estimates the gas required to do the deposit
- **withdraw** - moves assets from the L2 to the L1
- **withdraw_estimate_gas** - estimates the gas required to do the withdrawal
  Which accept different parameters depending on the asset bridger type

### Cross chain messages

When assets are moved by the L1 and L2 cross chain messages are sent. The lifecycles of these messages are encapsulated in the classes `L1ToL2Message` and `L2ToL1Message`. These objects are commonly created from the receipts of transactions that send cross chain messages. A cross chain message will eventually result in a transaction being executed on the destination chain, and these message classes provide the ability to wait for that finalizing transaction to occur.

### Networks

Arbitrum C# SDK comes pre-configured for Mainnet and Goerli, and their Arbitrum counterparts. However, the networks functionality can be used to register networks for custom Arbitrum instances. Most of the classes in Arbitrum C# SDK depend on network objects so this must be configured before using other Arbitrum C# SDK functionality.

### Inbox tools

As part of normal operation the Arbitrum sequencer will send messages into the rollup chain. However, if the sequencer is unavailable and not posting batches, the inbox tools can be used to force the inclusion of transactions into the rollup chain.

### Utils

- **EventFetcher** - A utility to provide typing for the fetching of events
- **MultiCaller** - A utility for executing multiple calls as part of a single RPC request. This can be useful for reducing round trips.
- **constants** - A list of useful Arbitrum related constants

### Run tests

1. First, make sure you have a Nitro test node running. Follow the instructions [here](https://docs.arbitrum.io/node-running/how-tos/local-dev-node).

2. Install the library dependencies by running `npm install`.

3. After the node has started up (that could take up to 20-30 mins), run `dotnet run --project GenNetwork.csproj
`.

4. Once done, finally run `dotnet test IntegrationTests/IntegrationTests.csproj` to run the integration tests.

### Note

The Arbitrum C# SDK was converted from the Arbitrum TypeScript SDK. To avoid introducing errors and to facilitate ease of use, some functionalities have retained the structure from the TypeScript code, even though this may have made the library less idiomatic for C#. For instance, there might be use of asynchronous (async) methods in scenarios where asynchronous behavior is not strictly necessary.This approach allows for a smoother conversion from the TypeScript SDK and may help maintain consistency between the two versions, but it may also result in less optimal C# code. Users of the SDK may notice patterns and practices that do not fully align with conventional C# programming practices, such as unnecessary use of asynchronous methods where synchronous ones would suffice.

Despite these potential deviations, the SDK is designed to provide a reliable and familiar experience for developers transitioning from the TypeScript version to C#. As the library evolves, it may undergo refactoring to improve code quality and better adhere to C# idioms, such as removing unnecessary asynchronous behavior and optimizing method signatures for more efficient code execution.