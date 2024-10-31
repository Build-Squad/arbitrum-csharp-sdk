using Arbitrum.ContractFactory.aeWETH;
using Arbitrum.Message;
using Arbitrum.Scripts;
using Arbitrum.Utils;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using NUnit.Framework;
using System.Numerics;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class WethTests
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

            var wethToWrap = Web3.Convert.ToWei(0.00001, UnitConversion.EthUnit.Ether);
            var wethToDeposit = Web3.Convert.ToWei(0.0000001, UnitConversion.EthUnit.Ether);

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);

            var l2WETH = await LoadContractUtils.LoadContract("AeWETH", l2Provider, l2Network.TokenBridge.L2Weth, true);

            var initialBalance = await l2WETH.GetFunction("balanceOf").CallAsync<BigInteger>(l2Signer.Account.Address);

            Assert.That(initialBalance.Equals(BigInteger.Zero), "start balance weth");

            var l1WETH = await LoadContractUtils.LoadContract("AeWETH", l1Provider, l2Network.TokenBridge.L1Weth, true);

            var txRequest = new DepositFunction
            {
                FromAddress = l1Signer.Account.Address,
                AmountToSend = new HexBigInteger(wethToWrap)
            };

            var l1WethHandler = l1Provider.Eth.GetContractHandler(l1WETH.Address);
            await l1WethHandler.SendRequestAndWaitForReceiptAsync(txRequest);

            await TestHelpers.DepositToken(
                depositAmount: wethToDeposit,
                l1TokenAddress: l1WETH.Address,
                l1Signer: l1Signer,
                l2Signer: l2Signer,
                l2Network: l2Network,
                erc20Bridger: erc20Bridger,
                expectedGatewayType: GatewayType.WETH,
                expectedStatus: L1ToL2MessageUtils.L1ToL2MessageStatus.REDEEMED
            );

            var l2WethGateway = await erc20Bridger.GetL2GatewayAddress(l2Signer, l2Network, l1WETH.Address);
            Assert.That(l2WethGateway, Is.EqualTo(l2Network.TokenBridge.L2WethGateway));

            var l2Token = await erc20Bridger.GetL2TokenContract(l2Signer, l2Network.TokenBridge.L2Weth);
            Assert.That(l2Token.Address, Is.EqualTo(l2Network.TokenBridge.L2Weth));

            await TestHelpers.FundL2(setupState.L2Deployer.Provider, address: l2Signer.Account.Address);

            var l2Weth = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                address: l2Token.Address,
                contractName: "AeWETH",
                isClassic: true
            );

            var privateKey = EthECKey.GenerateKey().GetPrivateKey();
            var account = new Nethereum.Web3.Accounts.Account(privateKey);

            var randomAddr = account.Address;

            var withdrawToFunction = new WithdrawToFunction
            {
                Account = randomAddr,
                Amount = new HexBigInteger(wethToDeposit)
            };

            var contractHandler = l2Provider.Eth.GetContractHandler(l2Weth.Address);            
            await contractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction);

            var afterBalance = await l2Provider.Eth.GetBalance.SendRequestAsync(randomAddr);
            Assert.That(afterBalance.Value, Is.EqualTo(wethToDeposit));
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

            await TestHelpers.FundL1(setupState.L1Deployer.Provider, address: l1Signer.Account.Address);
            await Task.Delay(2000);
            await TestHelpers.FundL2(setupState.L2Deployer.Provider, address: l2Signer.Account.Address);

            var l2Weth = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                address: l2Network.TokenBridge.L2Weth,
                contractName: "AeWETH",
                isClassic: true
            );

            var txRequest = new DepositFunction
            {
                FromAddress = l2Signer.Account.Address,
                AmountToSend = new HexBigInteger(wethToWrap)
            };

            var l2WethHandler = l2Provider.Eth.GetContractHandler(l2Weth.Address);
            var txReceipt = await l2WethHandler.SendRequestAndWaitForReceiptAsync(txRequest);

            Assert.That(txReceipt.Status.Value, Is.EqualTo(BigInteger.One));

            var l1Token = await LoadContractUtils.LoadContract(
                    provider: l1Provider,
                    address: l2Network.TokenBridge.L1Weth,
                    contractName: "ERC20",
                    isClassic: true
                );

            await TestHelpers.WithdrawToken(new WithdrawalParams
            {
                L1FundProvider = setupState.L1Deployer.Provider,
                L2FundProvider = setupState.L2Deployer.Provider,
                Amount = wethToWithdraw,
                Erc20Bridger = erc20Bridger,
                GatewayType = GatewayType.WETH,
                L1Signer = l1Signer,
                L1Token= l1Token,
                L2Signer = setupState.L2Signer,
                StartBalance = wethToWrap
            }, l2Network);
        }
    }
}
