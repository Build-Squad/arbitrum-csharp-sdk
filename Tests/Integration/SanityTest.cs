using Arbitrum.Scripts;
using Arbitrum.Utils;
using NUnit.Framework;

namespace Arbitrum.Tests.Integration
{
    [TestFixture]
    public class SanityTest
    {

        [Test]
        public async Task TestStandardGatewaysPublicStorageVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Provider = setupState.L1Signer.Provider;
            var l2Provider = setupState.L2Signer.Provider;
            var l2Network = setupState.L2Network;

            // Load L1 and L2 ERC20 Gateways
            var l1Gateway = await LoadContractUtils.LoadContract("L1ERC20Gateway", l1Provider, l2Network.TokenBridge.L1ERC20Gateway, true);
            var l2Gateway = await LoadContractUtils.LoadContract("L2ERC20Gateway", l2Provider, l2Network.TokenBridge.L2ERC20Gateway, true);

            //Verify clonableProxyHash matches between L1 and L2 Gateways
            var l1ClonableProxyHash = await l1Gateway.GetFunction("cloneableProxyHash").CallAsync<byte[]>();
            var l2ClonableProxyHash = await l2Gateway.GetFunction("cloneableProxyHash").CallAsync<byte[]>();
            Assert.That(l1ClonableProxyHash, Is.EqualTo(l2ClonableProxyHash));

            // Verify beaconProxyFactory matches between L1 and L2 Gateways
            var l1BeaconProxyHash = await l1Gateway.GetFunction("l2BeaconProxyFactory").CallAsync<string>();
            var l2BeaconProxyHash = await l2Gateway.GetFunction("beaconProxyFactory").CallAsync<string>();
            Assert.That(l1BeaconProxyHash, Is.EqualTo(l2BeaconProxyHash));

            // Verify L1 and L2 counterparts
            var l1GatewayCounterparty = await l1Gateway.GetFunction("counterpartGateway").CallAsync<string>();
            ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2ERC20Gateway);

            var l2GatewayCounterparty = await l2Gateway.GetFunction("counterpartGateway").CallAsync<string>();
            ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1ERC20Gateway);

            // Verify routers
            var l1Router = await l1Gateway.GetFunction("router").CallAsync<string>();
            ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2Gateway.GetFunction("router").CallAsync<string>();
            ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestCustomGatewaysPublicStorageVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Provider = setupState.L1Signer.Provider;
            var l2Provider = setupState.L2Signer.Provider;
            var l2Network = setupState.L2Network;

            var l1CustomGateway = await LoadContractUtils.LoadContract("L1CustomGateway", l1Provider, l2Network.TokenBridge.L1CustomGateway, true);
            var l2CustomGateway = await LoadContractUtils.LoadContract("L2CustomGateway", l2Provider, l2Network.TokenBridge.L2CustomGateway, true);

            var l1GatewayCounterparty = await l1CustomGateway.GetFunction("counterpartGateway").CallAsync<string>();
            ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2CustomGateway);

            var l2GatewayCounterparty = await l2CustomGateway.GetFunction("counterpartGateway").CallAsync<string>();
            ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1CustomGateway);

            var l1Router = await l1CustomGateway.GetFunction("router").CallAsync<string>();
            ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2CustomGateway.GetFunction("router").CallAsync<string>();
            ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestWethGatewaysPublicStorageVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l2Network = setupState.L2Network;
            var l1Provider = setupState.L1Signer.Provider;
            var l2Provider = setupState.L2Signer.Provider;

            var l1WethGateway = await LoadContractUtils.LoadContract("L1WethGateway", l1Provider, l2Network.TokenBridge.L1WethGateway, true);
            var l2WethGateway = await LoadContractUtils.LoadContract("L2WethGateway", l2Provider, l2Network.TokenBridge.L2WethGateway, true);

            var l1Weth = await l1WethGateway.GetFunction("l1Weth").CallAsync<string>();
            ExpectIgnoreCase(l1Weth, l2Network.TokenBridge.L1Weth);

            var l2Weth = await l2WethGateway.GetFunction("l2Weth").CallAsync<string>();
            ExpectIgnoreCase(l2Weth, l2Network.TokenBridge.L2Weth);

            var l1GatewayCounterparty = await l1WethGateway.GetFunction("counterpartGateway").CallAsync<string>();
            ExpectIgnoreCase(l1GatewayCounterparty, l2Network.TokenBridge.L2WethGateway);

            var l2GatewayCounterparty = await l2WethGateway.GetFunction("counterpartGateway").CallAsync<string>();
            ExpectIgnoreCase(l2GatewayCounterparty, l2Network.TokenBridge.L1WethGateway);

            var l1Router = await l1WethGateway.GetFunction("router").CallAsync<string>();
            ExpectIgnoreCase(l1Router, l2Network.TokenBridge.L1GatewayRouter);

            var l2Router = await l2WethGateway.GetFunction("router").CallAsync<string>();
            ExpectIgnoreCase(l2Router, l2Network.TokenBridge.L2GatewayRouter);
        }

        [Test]
        public async Task TestAeWethPublicVarsProperlySet()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l2Provider = setupState.L2Signer.Provider;
            var l2Network = setupState.L2Network;

            var aeWeth = await LoadContractUtils.LoadContract("AeWETH", l2Provider, l2Network.TokenBridge.L2Weth, true);

            var l2GatewayOnAeWeth = await aeWeth.GetFunction("l2Gateway").CallAsync<string>();
            ExpectIgnoreCase(l2GatewayOnAeWeth, l2Network.TokenBridge.L2WethGateway);

            var l1AddressOnAeWeth = await aeWeth.GetFunction("l1Address").CallAsync<string>();
            ExpectIgnoreCase(l1AddressOnAeWeth, l2Network.TokenBridge.L1Weth);
        }

        [Test]
        public async Task TestL1GatewayRouterPointsToRightWethGateways()
        {
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Network = setupState.L2Network;
            var admin_erc20_bridger = setupState.Erc20Bridger;

            var gateway = await admin_erc20_bridger.GetL1GatewayAddress(l2Network.TokenBridge.L1Weth, l1Signer, l2Network);

            Assert.That(gateway, Is.EqualTo(l2Network.TokenBridge.L1WethGateway));
        }

        [Test]
        public async Task TestL1AndL2ImplementationsOfCalculateL2Erc20AddressMatch()
        {
            // Set up the environment and state
            var setupState = await TestSetupUtils.TestSetup();
            var l1Signer = setupState.L1Signer;
            var l2Provider = setupState.L2Signer.Provider;
            var l2Network = setupState.L2Network;
            var erc20_bridger = setupState.Erc20Bridger;

            var address = HelperMethods.GenerateRandomHex(20);

            var erc20L2AddressAsPerL1 = await erc20_bridger.GetL2ERC20Address(address, l1Signer, l2Network);

            var l2GatewayRouter = await LoadContractUtils.LoadContract(
                provider: l2Provider,
                contractName: "L2GatewayRouter",
                address: l2Network.TokenBridge.L2GatewayRouter,
                isClassic: true
            );

            var erc20L2AddressAsPerL2 = await l2GatewayRouter.GetFunction("calculateL2TokenAddress").CallAsync<string>(address);

            Assert.That(erc20L2AddressAsPerL1, Is.EqualTo(erc20L2AddressAsPerL2));
        }

        public static void ExpectIgnoreCase(string? expected, string? actual)
        {
            Assert.That(expected?.ToLower(), Is.EqualTo(actual?.ToLower()));
        }
    }
}
