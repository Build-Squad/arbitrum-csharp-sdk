using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using Arbitrum.DataEntities;
using Nethereum.JsonRpc.Client;
using Arbitrum.Scripts;

namespace Abitrum.Scripts
{
    //configuring and initializing a web3 provider for Ethereum and Arbitrum networks,
    //setting up deployers, signing transactions, and then saving network configurations to a JSON file.
    class GenNetwork
    {

        static async Task Main(string[] args)
        {
            var ethProvider = new Web3(new RpcClient(new Uri(TestSetupUtils.Config["ETH_URL"])));
            var arbProvider = new Web3(new RpcClient(new Uri(TestSetupUtils.Config["ARB_URL"])));

            // Inject middleware for Ethereum provider       ///////////
            //ethProvider.Client.OverridingRequestInterceptor = new AccountTransactionSigningInterceptor();

            //// Inject middleware for Arbitrum provider      ////////////
            //arbProvider.Client.OverridingRequestInterceptor = new RequestInterceptor();

            var l1Deployer = await TestSetupUtils.GetSigner(ethProvider, TestSetupUtils.Config["ETH_KEY"]);
            var l2Deployer = await TestSetupUtils.GetSigner(arbProvider, TestSetupUtils.Config["ARB_KEY"]);

            //////////////////

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
