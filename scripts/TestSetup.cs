using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Arbitrum.AssetBridger;
using Arbitrum.AssetBridgerModule;
using Arbitrum.DataEntities;
using static Arbitrum.AssetBridger.Erc20Bridger;
using static Arbitrum.Inbox.ForceInclusionParams;

namespace Arbitrum.Scripts
{
    public class SetupHelper
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private static async Task<string> GetDeploymentDataAsync()
        {
            string[] dockerNames = { "nitro_sequencer_1", "nitro-sequencer-1", "nitro-testnode-sequencer-1", "nitro-testnode-sequencer-1" };
            foreach (var dockerName in dockerNames)
            {
                try
                {
                    var response = await httpClient.GetAsync($"http://docker-api.com/{dockerName}/config/deployment.json");
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception)
                {
                    continue;
                }
            }
            throw new Exception("nitro-testnode sequencer not found");
        }

        private static async Task<Dictionary<string, object>> GetCustomNetworksAsync(string l1Url, string l2Url)
        {
            var l1Provider = new Web3(l1Url);
            var l2Provider = new Web3(l2Url);

            // Inject middleware
            // l1Provider and l2Provider configurations

            var deploymentData = JsonSerializer.Deserialize<Dictionary<string, object>>(await GetDeploymentDataAsync());

            // Extract necessary data and construct network objects

            return new Dictionary<string, object>
        {
            { "l1Network", l1Network },
            { "l2Network", l2Network }
        };
        }

        public static async Task<Dictionary<string, object>> SetupNetworksAsync(string l1Url, string l2Url, string l1Deployer, string l2Deployer)
        {
            var customNetworks = await GetCustomNetworksAsync(l1Url, l2Url);

            // Deploy ERC20 and initialize contracts

            return new Dictionary<string, object>
        {
            { "l1Network", l1Network },
            { "l2Network", l2Network }
        };
        }

        private static object GetSigner(string provider, string key = null)
        {
            // Logic for determining signer
            // Return signer object
        }

        public static async Task<Dictionary<string, object>> TestSetupAsync(string ethUrl, string arbUrl, string ethKey, string arbKey)
        {
            var ethProvider = new Web3(ethUrl);
            var arbProvider = new Web3(arbUrl);

            // Configure providers
            // Add middleware

            var l1Deployer = GetSigner(ethProvider, ethKey);
            var l2Deployer = GetSigner(arbProvider, arbKey);

            // Other setup logic

            return new Dictionary<string, object>
        {
            { "l1Signer", l1Signer },
            { "l2Signer", l2Signer },
            { "l1Network", l1Network },
            { "l2Network", l2Network },
            { "erc20Bridger", erc20Bridger },
            { "adminErc20Bridger", adminErc20Bridger },
            { "ethBridger", ethBridger },
            { "inboxTools", inboxTools },
            { "l1Deployer", l1Deployer },
            { "l2Deployer", l2Deployer }
        };
        }
    }

}