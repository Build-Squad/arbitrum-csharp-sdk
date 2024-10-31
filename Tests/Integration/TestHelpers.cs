using Arbitrum.AssetBridger;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using NBitcoin;
using Nethereum.Contracts;
using Nethereum.HdWallet;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NUnit.Framework;
using System.Numerics;
using static Arbitrum.Message.L1ToL2MessageUtils;
namespace Arbitrum.Tests.Integration
{
    public enum GatewayType
    {
        STANDARD = 1,
        CUSTOM = 2,
        WETH = 3
    }

    public class WithdrawalParams
    {
        public BigInteger? StartBalance { get; set; }
        public BigInteger? Amount { get; set; }
        public Erc20Bridger? Erc20Bridger { get; set; }
        public Contract? L1Token { get; set; }
        public SignerOrProvider? L2Signer { get; set; }
        public SignerOrProvider? L1Signer { get; set; }
        public GatewayType? GatewayType { get; set; }
        public Web3? L1FundProvider { get; set; }
        public Web3? L2FundProvider { get; set; }
    }
    public class DepositTokenResults
    {
        public Contract? L1Token { get; set; }
        public L1ContractCallTransactionReceiptResults? WaitRes { get; set; }
        public Contract? L2Token { get; set; }
    }

    [TestFixture]
    public static class TestHelpers
    {
        public static async Task MineUntilStop(SignerOrProvider miner, Dictionary<string, object> state)
        {
            while ((bool)state["mining"])
            {
                var tx = new TransactionRequest
                {
                    From = miner.Account.Address,
                    To = miner.Account.Address,
                    Value = new HexBigInteger(0),
                    ChainId = await miner.Provider.Eth.ChainId.SendRequestAsync(),
                    GasPrice = await miner.Provider.Eth.GasPrice.SendRequestAsync(),
                    Nonce = await miner.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(miner.Account.Address)
                };

                tx.Gas ??= await miner.Provider.TransactionManager.EstimateGasAsync(tx);

                var rawTransaction = await miner.Provider.TransactionManager.SignTransactionAsync(tx);

                var txnHash = await miner.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(rawTransaction);

                var transactionReceipt = await miner.Provider.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txnHash);

                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }

        public static async Task WithdrawToken(WithdrawalParams parameters, L2Network l2Network)
        {
            var l1Provider = parameters.L1Signer.Provider;
            var l2Provider = parameters.L2Signer.Provider;

            var withdrawalParams = await parameters.Erc20Bridger.GetWithdrawalRequest(
                new Erc20WithdrawParams()
                {
                    Amount = parameters.Amount,
                    Erc20L1Address = parameters?.L1Token?.Address,
                    DestinationAddress = parameters?.L2Signer?.Account?.Address,
                    From = parameters?.L2Signer?.Account?.Address,
                    L2Signer = parameters?.L2Signer
                });

            var l1GasEstimate = await withdrawalParams.EstimateL1GasLimit(l1Provider);

            var withdrawRec = await parameters.Erc20Bridger.Withdraw(new Erc20WithdrawParams()
            {
                DestinationAddress = parameters.L2Signer.Account.Address,
                Amount = parameters.Amount,
                Erc20L1Address = parameters.L1Token.Address,
                L2Signer = parameters.L2Signer,
                L1Signer = parameters.L1Signer
            });

            Assert.That(withdrawRec.Status.Value, Is.EqualTo(BigInteger.One), "initiate token withdraw txn failed");

            var message = (await withdrawRec.GetL2ToL1Messages(parameters.L1Signer, withdrawRec.ContractAddress)).FirstOrDefault();

            Assert.That(message, Is.Not.Null, "withdraw message not found");

            var messageStatus = await message.StatusBase(new SignerOrProvider(l2Provider));

            Assert.That(messageStatus, Is.EqualTo(L2ToL1MessageStatus.UNCONFIRMED), "invalid withdraw status");

            var l2TokenAddr = await parameters.Erc20Bridger.GetL2ERC20Address(parameters.L1Token.Address, parameters.L1Signer, l2Network);

            var l2Token = await parameters.Erc20Bridger.GetL2TokenContract(parameters.L2Signer, l2TokenAddr);

            var testWalletL2Balance = await l2Token.GetFunction("balanceOf").CallAsync<BigInteger>(parameters.L2Signer.Account.Address);

            Assert.That(testWalletL2Balance, Is.EqualTo(parameters.StartBalance - parameters.Amount), "token withdraw balance not deducted");

            var walletAddress = parameters.L1Signer.Account.Address;

            var gatewayAddress = await parameters.Erc20Bridger.GetL2GatewayAddress(parameters.L2Signer, l2Network, parameters.L1Token.Address);

            var (expectedL1Gateway, expectedL2Gateway) = GetGateways(parameters.GatewayType, parameters.Erc20Bridger.L2Network);

            Assert.That(gatewayAddress, Is.EqualTo(expectedL2Gateway), "Gateway is not custom gateway");

            var gatewayWithdrawEvents = await parameters.Erc20Bridger.GetL2WithdrawalEvents(
                l2Provider,
                gatewayAddress,
                new NewFilterInput()
                {
                    FromBlock = new BlockParameter(withdrawRec.BlockNumber),
                    ToBlock = new BlockParameter()
                },
                parameters.L1Token.Address,
                walletAddress);

            Assert.That(gatewayWithdrawEvents.Count, Is.EqualTo(1), "token query failed");

            var balBefore = await parameters.L1Token.GetFunction("balanceOf").CallAsync<BigInteger>(parameters.L1Signer.Account.Address);

            string mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString();

            var miner1Seed = new Wallet(mnemonic, "");
            var miner2Seed = new Wallet(mnemonic, "");

            var miner1PrivateKey = miner1Seed.GetPrivateKey(0);
            var miner2PrivateKey = miner2Seed.GetPrivateKey(0);

            var miner1Account = new Account(miner1PrivateKey, Nethereum.Signer.Chain.Private);
            var miner2Account = new Account(miner2PrivateKey, new BigInteger(412346));

            var miner1 = new SignerOrProvider(miner1Account, l1Provider);
            var miner2 = new SignerOrProvider(miner2Account, l2Provider);

            await FundL1(parameters.L1FundProvider, address:  miner1.Account.Address);
            await FundL2(parameters.L2FundProvider, address: miner2.Account.Address);

            var state = new Dictionary<string, object> { { "mining", true } };

            await Task.WhenAny(
                MineUntilStop(miner1, state),
                MineUntilStop(miner2, state),
                message.WaitUntilReadyToExecuteBase(parameters?.L2Signer)
            );

            state["mining"] = false;

        /* Not to be tested on local node
            Assert.That(await message.StatusBase(new SignerOrProvider(l2Provider)), Is.EqualTo(L2ToL1MessageStatus.CONFIRMED), "confirmed status");

            var execTx = await message.Execute(parameters.L2Signer.Provider);

            Assert.That(execTx.GasUsed.Value, Is.LessThan(l1GasEstimate), "Gas used greater than estimate");

            Assert.That(await message.StatusBase(new SignerOrProvider(l2Provider)), Is.EqualTo(L2ToL1MessageStatus.EXECUTED), "executed status");

            var balAfter = await parameters.L1Token.GetFunction("balanceOf").CallAsync<BigInteger>(parameters.L1Signer.Account.Address);
            Assert.That(balBefore + parameters.Amount, Is.EqualTo(balAfter), "Not withdrawn");
        */
        }

        public static (string? expectedL1Gateway, string? expectedL2Gateway) GetGateways(GatewayType? gatewayType, L2Network l2Network)
        {
            switch (gatewayType)
            {
                case GatewayType.CUSTOM:
                    return (
                        expectedL1Gateway: l2Network.TokenBridge.L1CustomGateway,
                        expectedL2Gateway: l2Network.TokenBridge.L2CustomGateway
                    );
                case GatewayType.STANDARD:
                    return (
                        expectedL1Gateway: l2Network.TokenBridge.L1ERC20Gateway,
                        expectedL2Gateway: l2Network.TokenBridge.L2ERC20Gateway
                    );
                case GatewayType.WETH:
                    return (
                        expectedL1Gateway: l2Network.TokenBridge.L1WethGateway,
                        expectedL2Gateway: l2Network.TokenBridge.L2WethGateway
                    );
                default:
                    throw new ArbSdkError($"Unexpected gateway type: {gatewayType}");
            }
        }

        public static async Task<DepositTokenResults> DepositToken(
            BigInteger depositAmount,
            string l1TokenAddress,
            Erc20Bridger erc20Bridger,
            SignerOrProvider l1Signer,
            SignerOrProvider l2Signer,
            L2Network l2Network,
            L1ToL2MessageStatus expectedStatus,
            GatewayType expectedGatewayType,
            GasOverrides? retryableOverrides = null)
        {
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;

            var approveToken = await erc20Bridger.ApproveToken(
                new ApproveParamsOrTxRequest()
                {
                    L1Signer = l1Signer,
                    Erc20L1Address = l1TokenAddress
                });

            var senderAddress = l1Signer.Account.Address;

            var expectedL1GatewayAddress = await erc20Bridger.GetL1GatewayAddress(l1TokenAddress, l1Signer, l2Network);

            var l1Token = await erc20Bridger.GetL1TokenContract(l1Provider, l1TokenAddress);
            
            var allowance = await l1Token.GetFunction("allowance").CallAsync<BigInteger>(senderAddress, expectedL1GatewayAddress);

            Assert.That(allowance, Is.EqualTo(Erc20Bridger.MAX_APPROVAL), "set token allowance failed");

            var initialBridgeTokenBalance = await l1Token.GetFunction("balanceOf").CallAsync<BigInteger>(expectedL1GatewayAddress);

            var userBalBefore = await l1Token.GetFunction("balanceOf").CallAsync<BigInteger>(senderAddress);
            
            var depositRec = await erc20Bridger.Deposit(new Erc20DepositParams
            {
                L1Signer = l1Signer,
                L2Provider = l2Provider,
                Erc20L1Address = l1TokenAddress,
                Amount = depositAmount,
                RetryableGasOverrides = retryableOverrides
            });

            await Task.Delay(1000);
            var finalBridgeTokenBalance = await l1Token.GetFunction("balanceOf").CallAsync<BigInteger>(expectedL1GatewayAddress);

            BigInteger expectedBalance = expectedGatewayType == GatewayType.WETH ? 0 : initialBridgeTokenBalance + depositAmount;

            Assert.That(finalBridgeTokenBalance, Is.EqualTo(expectedBalance), "bridge balance not updated after L1 token deposit txn");

            var userBalAfter = await l1Token.GetFunction("balanceOf").CallAsync<BigInteger>(l1Signer.Account.Address);

            Assert.That(userBalAfter, Is.EqualTo(userBalBefore - depositAmount), "user bal after");

            var waitRes = await depositRec.WaitForL2(l2Signer);
            Assert.That(waitRes.Result.Status, Is.EqualTo(expectedStatus), "Unexpected status");

            if (retryableOverrides != null)
            {
                return new DepositTokenResults
                {
                    L1Token = l1Token,
                    WaitRes = waitRes
                };
            }

            var gateways = GetGateways(expectedGatewayType, erc20Bridger.L2Network);

            var l1Gateway = await erc20Bridger.GetL1GatewayAddress(l1TokenAddress, l1Signer, l2Network);

            Assert.That(l1Gateway, Is.EqualTo(gateways.expectedL1Gateway), "incorrect l1 gateway address");

            var l2Gateway = await erc20Bridger.GetL2GatewayAddress(l2Signer, l2Network, l1TokenAddress);

            Assert.That(l2Gateway, Is.EqualTo(gateways.expectedL2Gateway), "incorrect l2 gateway address");

            var l2Erc20Addr = await erc20Bridger.GetL2ERC20Address(l1TokenAddress, l1Signer, l2Network);

            var l2Token = await erc20Bridger.GetL2TokenContract(l2Signer, l2Erc20Addr);

            var l1Erc20Addr = await erc20Bridger.GetL1ERC20Address(l2Signer, l2Network, l2Erc20Addr);

            Assert.That(l1Erc20Addr.ToLower(), Is.EqualTo(l1TokenAddress.ToLower()), "getERC20L1Address/getERC20L2Address failed with proper token address");

            await Task.Delay(3000);

            var testWalletL2Balance = await l2Token.GetFunction("balanceOf").CallAsync<BigInteger>(l2Signer.Account.Address);

            Assert.That(testWalletL2Balance, Is.EqualTo(depositAmount), "l2 wallet not updated after deposit");

            return new DepositTokenResults
            {
                L1Token = l1Token,
                WaitRes = waitRes,
                L2Token = l2Token
            };
        }

        private static async Task Fund(Web3 provider, BigInteger amount = default, string? address = null)
        {
            if (amount == default) amount = 1;

            var transaction = await provider.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(address, (decimal)amount);
            var balance = await provider.Eth.GetBalance.SendRequestAsync(address);

            if (transaction.Status.Value == 0 || balance.Value == 0) throw new Exception("Funding failed!");
        }

        public static async Task FundL1(Web3 provider, BigInteger amount = default, string? address = null)
        {
            await Fund(provider, amount, address);
        }

        public static async Task FundL2(Web3 provider, BigInteger amount = default, string? address = null)
        {
            await Fund(provider, amount, address);
        }
    }
}
