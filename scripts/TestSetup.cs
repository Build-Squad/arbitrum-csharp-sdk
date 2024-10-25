using Arbitrum.AssetBridger;
using Arbitrum.AssetBridgerModule;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Numerics;
using static Arbitrum.AssetBridger.Erc20Bridger;
using static Arbitrum.Inbox.ForceInclusionParams;

namespace Arbitrum.Scripts
{
    public class CustomNetworks
    {
        public L1Network L1Network { get; set; }
        public L2Network L2Network { get; set; }
    }

    public class TestState
    {
        public SignerOrProvider? L1Signer { get; set; }
        public SignerOrProvider? L2Signer { get; set; }
        public L1Network? L1Network { get; set; }
        public L2Network? L2Network { get; set; }
        public Erc20Bridger? Erc20Bridger { get; set; }
        public AdminErc20Bridger? AdminErc20Bridger { get; set; }
        public EthBridger? EthBridger { get; set; }
        public InboxTools? InboxTools { get; set; }
        public SignerOrProvider? L1Deployer { get; set; }
        public SignerOrProvider? L2Deployer { get; set; }
        public Contract? L1CustomToken { get; set; }
        public Contract? L1Token { get; set; }

    }

    public static class TestSetupUtils
    {
        public static readonly Dictionary<string, string> Config = new Dictionary<string, string>
        {
            { "ARB_URL", Environment.GetEnvironmentVariable("ARB_URL") },
            { "ETH_URL", Environment.GetEnvironmentVariable("ETH_URL") },
            { "ARB_KEY", Environment.GetEnvironmentVariable("ARB_KEY") },
            { "ETH_KEY", Environment.GetEnvironmentVariable("ETH_KEY") }
        };

        public static async Task<string> GetDeploymentData()
        {
            var dir = @"C:\Dev\nitro-testnode";
            var dockerNames = new List<string>
            {
                "nitro-testnode-sequencer-1",
                "nitro_sequencer_1",
                "nitro-sequencer-1",
                "nitro-testnode-sequencer-1"
            };

            foreach (var dockerName in dockerNames)
            {
                try
                {
                    var result = await ExecuteCommandAsync("docker", $"compose run --entrypoint sh tokenbridge -c \"cat l1l2_network.json\"", dir);
                    if (string.IsNullOrEmpty(result)) continue;
                    return result;
                }
                catch (Exception)
                {
                    // Ignore and try next docker name
                }
            }

            throw new Exception("nitro-testnode sequencer not found");
        }

        public static async Task<string> ExecuteCommandAsync(string command, string arguments, string directory)
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = directory
                }
            };

            process.Start();
            return await process.StandardOutput.ReadToEndAsync();
        }

        public static async Task<CustomNetworks> GetCustomNetworks(string l1Url, string l2Url)
        {
            var l1Provider = new Web3(new RpcClient(new Uri(l1Url)));
            var l2Provider = new Web3(new RpcClient(new Uri(l2Url)));

            var deploymentData = JsonConvert.DeserializeObject<Dictionary<string, string>>(await GetDeploymentData());

            var bridgeAddress = deploymentData["bridge"].ConvertToEthereumChecksumAddress();
            var inboxAddress = deploymentData["inbox"].ConvertToEthereumChecksumAddress();
            var sequencerInboxAddress = deploymentData["sequencer-inbox"].ConvertToEthereumChecksumAddress();
            var rollupAddress = deploymentData["rollup"].ConvertToEthereumChecksumAddress();

            var rollupContract = await LoadContractUtils.LoadContract(
                                            contractName: "RollupAdminLogic",
                                            provider: l1Provider,
                                            address: rollupAddress,
                                            isClassic: false
                                            );

            var confirmPeriodBlocks = await rollupContract.GetFunction("confirmPeriodBlocks").CallAsync<BigInteger>();

            var bridgeContract = await LoadContractUtils.LoadContract(
                                            contractName: "Bridge",
                                            provider: l1Provider,
                                            address: bridgeAddress,
                                            isClassic: false
                                            );

            var outboxAddress = await bridgeContract.GetFunction("allowedOutboxList").CallAsync<string>(0);

            var l1NetworkInfo = await l1Provider.Eth.ChainId.SendRequestAsync();
            var l2NetworkInfo = await l2Provider.Eth.ChainId.SendRequestAsync();


            var l1Network = new L1Network
            {
                BlockTime = 10,
                ChainID = (int)l1NetworkInfo.Value,
                ExplorerUrl = "",
                IsCustom = true,
                Name = "EthLocal",
                PartnerChainIDs =new int[]{ (int)l2NetworkInfo.Value },
                IsArbitrum = false
            };

            var l2Network = new L2Network
            {
                ChainID = (int)l2NetworkInfo.Value,
                IsCustom = true,
                Name = "ArbLocal",
                PartnerChainIDs = new int[] { (int)l1NetworkInfo.Value },
                IsArbitrum = true,
                ConfirmPeriodBlocks = (int)confirmPeriodBlocks,
                EthBridge = new EthBridge()
                {
                    Bridge = bridgeAddress,
                    Inbox = inboxAddress,
                    Outbox = outboxAddress,
                    Rollup = rollupAddress,
                    SequencerInbox = sequencerInboxAddress
                },
                TokenBridge = new TokenBridge() { },
                ExplorerUrl = "",
                RetryableLifetimeSeconds = 7 * 24 * 60 * 60,
                NitroGenesisBlock = 0,
                NitroGenesisL1Block = 0,
                DepositTimeout = 900000
            };

            return new CustomNetworks
            {
                L1Network = l1Network,
                L2Network = l2Network
            };
        }

        public static async Task<CustomNetworks> SetupNetworks(string l1Url, string l2Url, SignerOrProvider l1Deployer, SignerOrProvider l2Deployer)
        {
            var customNetworks = await GetCustomNetworks(l1Url, l2Url);

            var l1Contracts = (await DeploymentUtils.DeployERC20AndInit(l1Signer: l1Deployer, l2Signer: l2Deployer,inboxAddress: customNetworks.L2Network.EthBridge.Inbox)).Item1;
            var l2Contracts = (await DeploymentUtils.DeployERC20AndInit(l1Signer: l1Deployer, l2Signer: l2Deployer, inboxAddress: customNetworks.L2Network.EthBridge.Inbox)).Item2;

            var l2Network = customNetworks.L2Network;
            l2Network.TokenBridge = new TokenBridge()
            {
                L1CustomGateway = l1Contracts?.CustomGateway?.Address,
                L1ERC20Gateway = l1Contracts?.StandardGateway?.Address,
                L1GatewayRouter = l1Contracts?.Router?.Address,
                L1MultiCall = l1Contracts?.Multicall?.Address,
                L1ProxyAdmin = l1Contracts?.ProxyAdmin?.Address,
                L1Weth = l1Contracts?.Weth?.Address,
                L1WethGateway = l1Contracts?.WethGateway?.Address,
                L2CustomGateway = l2Contracts?.CustomGateway?.Address,
                L2ERC20Gateway = l2Contracts?.StandardGateway?.Address,
                L2GatewayRouter = l2Contracts?.Router?.Address,
                L2Multicall = l2Contracts?.Multicall?.Address,
                L2ProxyAdmin = l2Contracts?.ProxyAdmin?.Address,
                L2Weth = l2Contracts?.Weth?.Address,
                L2WethGateway = l2Contracts?.WethGateway?.Address
            };



            var l1Network = customNetworks.L1Network;

            NetworkUtils.AddCustomNetwork(l1Network, l2Network);

            var adminErc20Bridger = new AdminErc20Bridger(l2Network);
            await adminErc20Bridger.SetGateways(l1Signer: l1Deployer, l2Provider: l2Deployer.Provider, tokenGateways:
            new List<TokenAndGateway>
            {
                new TokenAndGateway
                {
                    GatewayAddr = l2Network.TokenBridge.L1WethGateway,
                    TokenAddr = l2Network.TokenBridge.L1Weth
                }
            });

            return new CustomNetworks
            {
                L1Network = l1Network,
                L2Network = l2Network
            };
        }

        public static async Task<Account> GetSigner(Web3 provider, string key = null)
        {
            if (key != null)
            {
                var account = new Account(key, await provider.Eth.ChainId.SendRequestAsync());
                var web3WithAccount = new Web3(account, provider.Client);
                return account;
            }
            else
            {
                var defaultAccount = provider.TransactionManager.Account as Account;
                if (defaultAccount == null)
                {
                    throw new Exception("No account available in the provider.");
                }
                return defaultAccount;
            }
        }

        public static async Task<TestState> TestSetup()
        {
            string projectPath = Path.Combine(AppContext.BaseDirectory, @"..\..\..");
            string PROJECT_DIRECTORY = Path.GetFullPath(projectPath);

            var l2ChainId = new BigInteger(412346);

            var l1DeployerAccount = new Account(Config["ETH_KEY"], Chain.Private);
            var l2DeployerAccount = new Account(Config["ARB_KEY"], l2ChainId);

            var l1DeployerWeb = new Web3(l1DeployerAccount, Config["ETH_URL"]);
            var l2DeployerWeb = new Web3(l2DeployerAccount, Config["ARB_URL"]);

            var l1Deployer = new SignerOrProvider(l1DeployerAccount, l1DeployerWeb);
            var l2Deployer = new SignerOrProvider(l2DeployerAccount, l2DeployerWeb);

            var pvtKeyGen = EthECKey.GenerateKey();

            var l1SignerAccount = new Account(pvtKeyGen.GetPrivateKey(), Chain.Private);
            var l2SignerAccount = new Account(pvtKeyGen.GetPrivateKey(), l2ChainId);

            var ethProvider = new Web3(l1SignerAccount, Config["ETH_URL"]);
            var arbProvider = new Web3(l2SignerAccount, Config["ARB_URL"]);

            var l1Signer = new SignerOrProvider(l1SignerAccount, ethProvider);
            var l2Signer = new SignerOrProvider(l2SignerAccount, arbProvider);

            (var l1Network, var l2Network) = await SetupNetworks(PROJECT_DIRECTORY, ethProvider, arbProvider);

            var erc20Bridger = new Erc20Bridger(l2Network);
            var adminErc20Bridger = new AdminErc20Bridger(l2Network);
            var ethBridger = new EthBridger(l2Network);
            var inboxTools = new InboxTools(l1Signer, l2Network);

            return new TestState
            {
                L1Signer = l1Signer,
                L2Signer = l2Signer,
                L1Network = l1Network,
                L2Network = l2Network,
                Erc20Bridger = erc20Bridger,
                AdminErc20Bridger = adminErc20Bridger,
                EthBridger = ethBridger,
                InboxTools = inboxTools,
                L1Deployer = l1Deployer,
                L2Deployer = l2Deployer
            };
        }


        private static async Task<(L1Network, L2Network)> SetupNetworks(string projectDir, Web3 ethProvider, Web3 arbProvider)
        {
            var localNetworkFile = Path.Combine(projectDir, "localNetwork.json");
            if (File.Exists(localNetworkFile))
            {
                var json = File.ReadAllText(localNetworkFile);
                var networkData = JsonConvert.DeserializeObject<CustomNetworks>(json);

                var l1Network = new L1Network()
                {
                    ChainID = networkData.L1Network.ChainID,
                    Name = networkData.L1Network.Name,
                    ExplorerUrl = networkData.L1Network.ExplorerUrl,
                    Gif = networkData.L1Network.Gif,
                    IsCustom = networkData.L1Network.IsCustom,
                    BlockTime = networkData.L1Network.BlockTime,
                    PartnerChainIDs = networkData.L1Network.PartnerChainIDs,
                    IsArbitrum = networkData.L1Network.IsArbitrum
                };

                var l2Network = new L2Network()
                {
                    ChainID = networkData.L2Network.ChainID,
                    Name = networkData?.L2Network?.Name,
                    ExplorerUrl = networkData?.L2Network?.ExplorerUrl,
                    Gif = networkData?.L2Network?.Gif,
                    IsCustom = networkData.L2Network.IsCustom,
                    BlockTime = networkData.L2Network.BlockTime,
                    PartnerChainIDs = networkData?.L2Network?.PartnerChainIDs,
                    IsArbitrum = networkData?.L2Network?.IsArbitrum ?? false,
                    ConfirmPeriodBlocks = networkData.L2Network.ConfirmPeriodBlocks,
                    RetryableLifetimeSeconds = networkData.L2Network.RetryableLifetimeSeconds,
                    NitroGenesisBlock = networkData.L2Network.NitroGenesisBlock,
                    NitroGenesisL1Block = networkData.L2Network.NitroGenesisL1Block,
                    DepositTimeout = networkData.L2Network.DepositTimeout,
                    NativeToken = networkData?.L2Network?.NativeToken,
                    TokenBridge = networkData?.L2Network?.TokenBridge,
                    EthBridge = networkData?.L2Network?.EthBridge,
                    PartnerChainID = networkData.L2Network.PartnerChainID
                };

                NetworkUtils.AddCustomNetwork(l1Network, l2Network);

                return (l1Network, l2Network);
            }

            var l1NetworkFetched = await NetworkUtils.GetL1Network(ethProvider);
            var l2NetworkFetched = await NetworkUtils.GetL2Network(arbProvider);

            return (l1NetworkFetched, l2NetworkFetched);
        }


        public static async Task SkipIfMainnet(int chainId)
        {
            if (chainId == 0)
            {
                // Initialize chainId if not already set
                var l1Network = (await TestSetup()).L1Network;
                chainId = l1Network.ChainID;
            }

            if (chainId == 1)
            {
                Console.WriteLine("You're writing to the chain on mainnet lol stop");
                Assert.Ignore("Skipping test on mainnet");
            }
        }
    }
}