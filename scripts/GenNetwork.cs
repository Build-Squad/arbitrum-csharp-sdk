using Arbitrum.DataEntities;
using Arbitrum.Scripts;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;
using System.Text.Json;

namespace Abitrum.Scripts
{
    class GenNetwork
    {

        static async Task Main(string[] args)
        {
            var ethProvider = new Web3(new RpcClient(new Uri(TestSetupUtils.Config["ETH_URL"])));
            var arbProvider = new Web3(new RpcClient(new Uri(TestSetupUtils.Config["ARB_URL"])));

            var l1Deployer = await TestSetupUtils.GetSigner(ethProvider, TestSetupUtils.Config["ETH_KEY"]);
            var l2Deployer = await TestSetupUtils.GetSigner(arbProvider, TestSetupUtils.Config["ARB_KEY"]);

            var l1Signer = new SignerOrProvider(l1Deployer, ethProvider);
            var l2Signer = new SignerOrProvider(l2Deployer, arbProvider);

            var networkAndDeployers = await TestSetupUtils.SetupNetworks(
                l1Url: TestSetupUtils.Config["ETH_URL"],
                l2Url: TestSetupUtils.Config["ARB_URL"],
                l1Deployer: l1Signer,
                l2Deployer: l2Signer
            );

            var l1Network = networkAndDeployers.L1Network;
            var l2Network = networkAndDeployers.L2Network;

            using (StreamWriter file = File.CreateText("localNetwork.json"))
            {
                var json = JsonSerializer.Serialize(new CustomNetworks
                {
                    L1Network = l1Network,
                    L2Network = l2Network
                }, new JsonSerializerOptions { WriteIndented = true });

                file.Write(json);
            }

            Console.WriteLine("localNetwork.json updated");
            Console.WriteLine("Done.");
        }
    }
}
