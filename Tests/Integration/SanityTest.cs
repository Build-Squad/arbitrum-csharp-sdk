using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Arbitrum.AssetBridger;
using Arbitrum.DataEntities;
using static Arbitrum.AssetBridger.Erc20Bridger;
using Arbitrum.Utils;
using Nethereum.Web3.Accounts;
using Arbitrum.Tests.Integration;
using Arbitrum.Scripts;

namespace Arbitrum.Message.Tests.Integration
{
    [TestFixture]
    public class SanityTest
    {

        [Test]
        public async Task TestStandardGatewaysPublicStorageVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = new Web3(l1Signer.TransactionManager.Client);
            var l2Provider = new Web3(l2Signer.TransactionManager.Client);
            var l2Network = setupState.L2Network;

            var l1Gateway = await LoadContractUtils.LoadContract(contractName: "L1ERC20Gateway", provider: l1Provider, address: l2Network.TokenBridge.L1ERC20Gateway, isClassic: true);
            Assert.That(l1Gateway, Is.Not.Null);

            var l2Gateway = await LoadContractUtils.LoadContract(contractName: "L2ERC20Gateway", provider: l2Provider, address: l2Network.TokenBridge.L2ERC20Gateway, isClassic: true);
            Assert.That(l2Gateway, Is.Not.Null);

            var l1ClonableProxyHash = await l1Gateway.GetFunction("cloneableProxyHash").CallAsync<string>();
            var l2ClonableProxyHash = await l2Gateway.GetFunction("cloneableProxyHash").CallAsync<string>();
            Assert.That(l1ClonableProxyHash, Is.EqualTo(l2ClonableProxyHash));

            var l1BeaconProxyHash = await l1Gateway.GetFunction("l2BeaconProxyFactory").CallAsync<string>();
            var l2BeaconProxyHash = await l2Gateway.GetFunction("beaconProxyFactory").CallAsync<string>();
            Assert.That(l1BeaconProxyHash, Is.EqualTo(l2BeaconProxyHash));

            var l1GatewayCounterparty = await l1Gateway.GetFunction("counterpartGateway").CallAsync<string>();
            await ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2ERC20Gateway);

            var l2GatewayCounterparty = await l2Gateway.GetFunction("counterpartGateway").CallAsync<string>();
            await ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1ERC20Gateway);

            var l1Router = await l1Gateway.GetFunction("router").CallAsync<string>();
            await ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2Gateway.GetFunction("router").CallAsync<string>();
            await ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestCustomGatewaysPublicStorageVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = new Web3(l1Signer.TransactionManager.Client);
            var l2Provider = new Web3(l2Signer.TransactionManager.Client);
            var l2Network = setupState.L2Network;

            var l1CustomGateway = await LoadContractUtils.LoadContract(contractName: "L1CustomGateway", provider: l1Provider, address: l2Network.TokenBridge.L1CustomGateway, isClassic: true);
            Assert.That(l1CustomGateway, Is.Not.Null);

            var l2CustomGateway = await LoadContractUtils.LoadContract(contractName: "L2CustomGateway", provider: l2Provider, address: l2Network.TokenBridge.L2CustomGateway, isClassic: true);
            Assert.That(l2CustomGateway, Is.Not.Null);

            var l1GatewayCounterparty = await l1CustomGateway.GetFunction("counterpartGateway").CallAsync<string>();
            await ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2CustomGateway);

            var l2GatewayCounterparty = await l2CustomGateway.GetFunction("counterpartGateway").CallAsync<string>();
            await ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1CustomGateway);

            var l1Router = await l1CustomGateway.GetFunction("router").CallAsync<string>();
            await ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2CustomGateway.GetFunction("router").CallAsync<string>();
            await ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestWethGatewaysPublicStorageVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = new Web3(l1Signer.TransactionManager.Client);
            var l2Provider = new Web3(l2Signer.TransactionManager.Client);
            var l2Network = setupState.L2Network;

            var l1WethGateway = await LoadContractUtils.LoadContract(contractName: "L1WethGateway", provider: l1Provider, address: l2Network.TokenBridge.L1WethGateway, isClassic: true);
            Assert.That(l1WethGateway, Is.Not.Null);

            var l2WethGateway = await LoadContractUtils.LoadContract(contractName: "L2WethGateway", provider: l2Provider, address: l2Network.TokenBridge.L2WethGateway, isClassic: true);
            Assert.That(l2WethGateway, Is.Not.Null);

            var l1Weth = await l1WethGateway.GetFunction("l1Weth").CallAsync<string>();
            await ExpectIgnoreCase(l1Weth, l2Network.TokenBridge.L1Weth);

            var l2Weth = await l2WethGateway.GetFunction("l2Weth").CallAsync<string>();
            await ExpectIgnoreCase(l2Weth, l2Network.TokenBridge.L2Weth);

            var l1GatewayCounterparty = await l1WethGateway.GetFunction("counterpartGateway").CallAsync<string>();
            await ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2WethGateway);

            var l2GatewayCounterparty = await l2WethGateway.GetFunction("counterpartGateway").CallAsync<string>();
            await ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1WethGateway);

            var l1Router = await l1WethGateway.GetFunction("router").CallAsync<string>();
            await ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2WethGateway.GetFunction("router").CallAsync<string>();
            await ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestAeWethPublicVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l2Signer = setupState.L2Signer;
            var l2Provider = new Web3(l2Signer.TransactionManager.Client);
            var l2Network = setupState.L2Network;

            var aeWeth = await LoadContractUtils.LoadContract(contractName: "AeWETH", provider: l2Provider, address: l2Network.TokenBridge.L2Weth, isClassic: true);
            Assert.That(aeWeth, Is.Not.Null);

            var l2GatewayOnAeWeth = await aeWeth.GetFunction("l2Gateway").CallAsync<string>();
            await ExpectIgnoreCase(l2GatewayOnAeWeth, l2Network.TokenBridge.L2WethGateway);

            var l1AddressOnAeWeth = await aeWeth.GetFunction("l1Address").CallAsync<string>();
            await ExpectIgnoreCase(l1AddressOnAeWeth, l2Network.TokenBridge.L1Weth);
        }

        [Test]
        public async Task TestL1GatewayRouterPointsToRightWethGateways()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l1Provider = new Web3(l1Signer.TransactionManager.Client);
            var l2Network = setupState.L2Network;
            var adminErc20Bridger = setupState.AdminErc20Bridger;

            var gateway = await adminErc20Bridger.GetL1GatewayAddress(l2Network.TokenBridge.L1Weth, l1Provider);
            Assert.That(gateway, Is.EqualTo(l2Network.TokenBridge.L1WethGateway));
        }

        [Test]
        public async Task TestL1AndL2ImplementationsOfCalculateL2Erc20AddressMatch()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Signer = setupState.L2Signer;
            var l1Provider = new Web3(l1Signer.TransactionManager.Client);
            var l2Provider = new Web3(l2Signer.TransactionManager.Client);
            var l2Network = setupState.L2Network;
            var erc20Bridger = setupState.Erc20Bridger;

            //generating random address
            var address = HelperMethods.GenerateRandomHex(32);

            var erc20L2AddressAsPerL1 = await erc20Bridger.GetL2ERC20Address(address, l1Provider);

            var l2GatewayRouter = await LoadContractUtils.LoadContract(contractName: "L2GatewayRouter", provider: l2Provider, address: l2Network.TokenBridge.L2GatewayRouter, isClassic: true);
            Assert.That(l2GatewayRouter, Is.Not.Null);

            var erc20L2AddressAsPerL2 = await l2GatewayRouter.GetFunction("calculateL2TokenAddress").CallAsync<string>(address);

            Assert.That(erc20L2AddressAsPerL1, Is.EqualTo(erc20L2AddressAsPerL2));
        }

        public async Task ExpectIgnoreCase(string expected, string actual)
        {
            await Task.Run(() =>
            {
                Assert.That(expected.ToLower(), Is.EqualTo(actual.ToLower()));
            });
        }

    }
}
