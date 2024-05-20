using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Web3;
using NUnit.Framework;
using Arbitrum.Tests.Integration;
using Arbitrum.Utils;
using Nethereum.Util;
using Org.BouncyCastle.Asn1.X509;
using static Arbitrum.Message.L1ToL2MessageUtils;
using Arbitrum.Scripts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class TokenBridgeTests
    {
        [Test]
        public async Task DepositWETH()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l2Network = setupState.L2Network;
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;
            var erc20Bridger = setupState.Erc20Bridger;

            var l1WethAddress = l2Network.TokenBridge.L1Weth;

            var wethToWrap = Web3.Convert.ToWei(0.00001, UnitConversion.EthUnit.Ether);
            var wethToDeposit = Web3.Convert.ToWei(0.0000001, UnitConversion.EthUnit.Ether);

            await TestHelpers.FundL1(l1Signer, Web3.Convert.ToWei(1, UnitConversion.EthUnit.Ether));

            var l2WETH = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                address: l2Network.TokenBridge.L2Weth,
                contractName: "AeWETH",
                isClassic: true
            );
            Assert.That(l2WETH.GetFunction("balanceOf").CallAsync<BigInteger>(l2Signer.Account.Address).Result, Is.Zero);

            var l1WETH = await LoadContractUtils.LoadContract(
                provider: l1Provider,
                address: l1WethAddress,
                contractName: "AeWETH",
                isClassic: true
            );

            var tx = await l1WETH.GetFunction("deposit").SendTransactionAsync(from: l1Signer.Account.Address, new CallInput{Value = new HexBigInteger(wethToWrap) });

            await l1Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tx);

            await TestHelpers.DepositToken(
                depositAmount: wethToDeposit,
                l1TokenAddress: l1WethAddress,
                erc20Bridger: erc20Bridger,
                l1Signer: l1Signer,
                l2Signer: l2Signer,
                expectedStatus: L1ToL2MessageStatus.REDEEMED,
                expectedGatewayType: GatewayType.WETH
            );

            var l2WethGateway = await erc20Bridger.GetL2GatewayAddress(l1WethAddress, l2Provider);
            Assert.That(l2WethGateway, Is.EqualTo(l2Network.TokenBridge.L2WethGateway));

            var l2Token = await erc20Bridger.GetL2TokenContract(l2Provider, l2Network.TokenBridge.L2Weth);
            Assert.That(l2Token.Address, Is.EqualTo(l2Network.TokenBridge.L2Weth));

            await TestHelpers.FundL2(l2Signer);

            var l2Weth = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                address: l2Token.Address,
                contractName: "AeWETH",
                isClassic: true
            );

            // Generate a new private key
            var privateKey = EthECKey.GenerateKey().GetPrivateKey();

            // Create a new account with the generated private key
            var account = new Account(privateKey);

            //random address
            var randomAddr = account.Address;

            tx = await l2Weth.GetFunction("withdrawTo").SendTransactionAsync(from: l2Signer.Account.Address, randomAddr, wethToDeposit);

            await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tx);

            var afterBalance = await l2Provider.Eth.GetBalance.SendRequestAsync(randomAddr);
            Assert.That(afterBalance, Is.EqualTo(wethToDeposit));
        }

        [Test]
        public async Task WithdrawWETH()
        {
            var setupState = await TestSetupUtils.TestSetup();

            var wethToWrap = Web3.Convert.ToWei(0.00001, UnitConversion.EthUnit.Ether);
            var wethToWithdraw = Web3.Convert.ToWei(0.00000001, UnitConversion.EthUnit.Ether);

            var l2Network = setupState.L2Network;
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = l1Signer.Provider;
            var l2Provider = l2Signer.Provider;
            var erc20Bridger = setupState.Erc20Bridger;

            await TestHelpers.FundL1(l1Signer);
            await TestHelpers.FundL2(l2Signer);

            var l2Weth = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                address: l2Network.TokenBridge.L2Weth,
                contractName: "AeWETH",
                isClassic: true
            );

            var tx = await l2Weth.GetFunction("deposit").SendTransactionAsync(from: l2Signer.Account.Address, new CallInput{ Value = new HexBigInteger(wethToWrap) });

            var rec = await l2Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tx);

            Assert.That(rec.Status, Is.EqualTo(1));

            await TestHelpers.WithdrawToken(new WithdrawalParams
            {
                Amount = wethToWithdraw,
                Erc20Bridger = erc20Bridger,
                GatewayType = GatewayType.WETH,
                L1Signer = l1Signer,
                L1Token= await LoadContractUtils.LoadContract(
                    provider: l1Provider,
                    address: l2Network.TokenBridge.L1Weth,
                    contractName: "ERC20",
                    isClassic: true
                ),
                L2Signer = setupState.L2Signer,
                StartBalance = wethToWrap
            });
        }
    }
}
