using Arbitrum.DataEntities;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;
using NUnit.Framework;
using System;

namespace Arbitrum.Tests.Integration
{
    public class EthBridgeTests
    {
        [Test]
        public async Task TestObtainDeployedBridgeAddresses()
        {
            var arbOneL2Network = await NetworkUtils.GetL2NetworkAsync(42161);
            var ethProvider = new Web3(new RpcClient(new Uri(Environment.GetEnvironmentVariable("MAINNET_RPC"))));

            var ethBridge = await NetworkUtils.GetEthBridgeInformation(arbOneL2Network.EthBridge.Rollup, new SignerOrProvider(ethProvider));

            Assert.That(arbOneL2Network.EthBridge.Bridge, Is.EqualTo(ethBridge.Bridge), "Bridge contract is not correct");
            Assert.That(arbOneL2Network.EthBridge.Inbox, Is.EqualTo(ethBridge.Inbox), "Inbox contract is not correct");
            Assert.That(arbOneL2Network.EthBridge.SequencerInbox, Is.EqualTo(ethBridge.SequencerInbox), "SequencerInbox contract is not correct");
            Assert.That(arbOneL2Network.EthBridge.Outbox, Is.EqualTo(ethBridge.Outbox), "Outbox contract is not correct");
        }
    }
}
