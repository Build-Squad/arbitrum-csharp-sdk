using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using YourNamespace.Lib.Utils.Helper;
using YourNamespace.Scripts;
using Arbitrum.AssetBridger;
using Arbitrum.DataEntities;
using static Arbitrum.AssetBridger.Erc20Bridger;

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
            var setupState = await TestSetup.TestSetup();
            l1Provider = setupState.L1Signer.Provider;
            l2Provider = setupState.L2Signer.Provider;
            l1Signer = setupState.L1Signer.Account;
            l2Signer = setupState.L2Signer.Account;
            erc20Bridger = setupState.ERC20Bridger;
        }

        [Test]
        public async Task TestStandardGatewaysPublicStorageVarsProperlySet()
        {
            var l1Gateway = LoadContract("L1ERC20Gateway", l2Network.TokenBridge.L1ERC20Gateway, l1Provider);
            var l2Gateway = LoadContract("L2ERC20Gateway", l2Network.TokenBridge.L2ERC20Gateway, l2Provider);

            var l1ClonableProxyHash = await l1Gateway.GetFunction("cloneableProxyHash").CallAsync<string>();
            var l2ClonableProxyHash = await l2Gateway.GetFunction("cloneableProxyHash").CallAsync<string>();
            Assert.AreEqual(l1ClonableProxyHash, l2ClonableProxyHash);

            var l1BeaconProxyHash = await l1Gateway.GetFunction("l2BeaconProxyFactory").CallAsync<string>();
            var l2BeaconProxyHash = await l2Gateway.GetFunction("beaconProxyFactory").CallAsync<string>();
            Assert.AreEqual(l1BeaconProxyHash, l2BeaconProxyHash);

            var l1GatewayCounterparty = await l1Gateway.GetFunction("counterpartGateway").CallAsync<Address>();
            ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2ERC20Gateway);

            var l2GatewayCounterparty = await l2Gateway.GetFunction("counterpartGateway").CallAsync<Address>();
            ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1ERC20Gateway);

            var l1Router = await l1Gateway.GetFunction("router").CallAsync<Address>();
            ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2Gateway.GetFunction("router").CallAsync<Address>();
            ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestCustomGatewaysPublicStorageVarsProperlySet()
        {
            var l1CustomGateway = LoadContract("L1CustomGateway", l2Network.TokenBridge.L1CustomGateway, l1Provider);
            var l2CustomGateway = LoadContract("L2CustomGateway", l2Network.TokenBridge.L2CustomGateway, l2Provider);

            var l1GatewayCounterparty = await l1CustomGateway.GetFunction("counterpartGateway").CallAsync<Address>();
            ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2CustomGateway);

            var l2GatewayCounterparty = await l2CustomGateway.GetFunction("counterpartGateway").CallAsync<Address>();
            ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1CustomGateway);

            var l1Router = await l1CustomGateway.GetFunction("router").CallAsync<Address>();
            ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2CustomGateway.GetFunction("router").CallAsync<Address>();
            ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestWethGatewaysPublicStorageVarsProperlySet()
        {
            var l1WethGateway = LoadContract("L1WethGateway", l2Network.TokenBridge.L1WethGateway, l1Provider);
            var l2WethGateway = LoadContract("L2WethGateway", l2Network.TokenBridge.L2WethGateway, l2Provider);

            var l1Weth = await l1WethGateway.GetFunction("l1Weth").CallAsync<Address>();
            ExpectIgnoreCase(l1Weth, l2Network.TokenBridge.L1Weth);

            var l2Weth = await l2WethGateway.GetFunction("l2Weth").CallAsync<Address>();
            ExpectIgnoreCase(l2Weth, l2Network.TokenBridge.L2Weth);

            var l1GatewayCounterparty = await l1WethGateway.GetFunction("counterpartGateway").CallAsync<Address>();
            ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2WethGateway);

            var l2GatewayCounterparty = await l2WethGateway.GetFunction("counterpartGateway").CallAsync<Address>();
            ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1WethGateway);

            var l1Router = await l1WethGateway.GetFunction("router").CallAsync<Address>();
            ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2WethGateway.GetFunction("router").CallAsync<Address>();
            ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestAeWethPublicVarsProperlySet()
        {
            var aeWeth = LoadContract("AeWETH", l2Network.TokenBridge.L2Weth, l2Provider);

            var l2GatewayOnAeWeth = await aeWeth.GetFunction("l2Gateway").CallAsync<Address>();
            ExpectIgnoreCase(l2GatewayOnAeWeth, l2Network.TokenBridge.L2WethGateway);

            var l1AddressOnAeWeth = await aeWeth.GetFunction("l1Address").CallAsync<Address>();
            ExpectIgnoreCase(l1AddressOnAeWeth, l2Network.TokenBridge.L1Weth);
        }

        [Test]
        public async Task TestL1GatewayRouterPointsToRightWethGateways()
        {
            var gateway = await AdminERC20Bridger.GetL1GatewayAddress(l2Network.TokenBridge.L1Weth, l1Provider);
            Assert.AreEqual(gateway, l2Network.TokenBridge.L1WethGateway);
        }

        [Test]
        public async Task TestL1AndL2ImplementationsOfCalculateL2Erc20AddressMatch()
        {
            var address = new byte[20]; // Replace with your logic to generate address
            var erc20L2AddressAsPerL1 = await ERC20Bridger.GetL2Erc20Address(address, l1Provider);

            var l2GatewayRouter = LoadContract("L2GatewayRouter", l2Network.TokenBridge.L2GatewayRouter, l2Provider);
            var erc20L2AddressAsPerL2 = await l2GatewayRouter.GetFunction("calculateL2TokenAddress").CallAsync<Address>(address);

            Assert.AreEqual(erc20L2AddressAsPerL1, erc20L2AddressAsPerL2);
        }

        private async Task ExpectIgnoreCase(string expected, string actual)
        {
            Assert.AreEqual(expected.ToLower(), actual.ToLower());
        }

        private Contract LoadContract(string contractName, string address, Web3 provider)
        {
            // Implement your contract loading logic here
        }
    }
}
