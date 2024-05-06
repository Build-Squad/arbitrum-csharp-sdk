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
using Newtonsoft.Json;
using Arbitrum.Utils;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3.Accounts.Managed;
using Nethereum.Util;
using NBitcoin;
using Nethereum.HdWallet;
using Nethereum.Web3.Accounts;
using System.Numerics;

namespace Arbitrum.Scripts
{
    public class CustomNetworks
    {
        public L1Network L1Network { get; set; }
        public L2Network L2Network { get; set; }
    }

    public class TestSetupResult
    {
        public Account L1Signer { get; set; }
        public Account L2Signer { get; set; }
        public L1Network L1Network { get; set; }
        public L2Network L2Network { get; set; }
        public Erc20Bridger Erc20Bridger { get; set; }
        public AdminErc20Bridger AdminErc20Bridger { get; set; }
        public EthBridger EthBridger { get; set; }
        public InboxTools InboxTools { get; set; }
        public Account L1Deployer { get; set; }
        public Account L2Deployer { get; set; }
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
            var dockerNames = new List<string>
            {
                "nitro_sequencer_1",
                "nitro-sequencer-1",
                "nitro-testnode-sequencer-1",
                "nitro-testnode-sequencer-1"
            };

            foreach (var dockerName in dockerNames)
            {
                try
                {
                    var result = await ExecuteCommandAsync("docker", $"exec {dockerName} cat /config/deployment.json");
                    return result;
                }
                catch (Exception)
                {
                    // Ignore and try next docker name
                }
            }

            throw new Exception("nitro-testnode sequencer not found");
        }

        public static async Task<string> ExecuteCommandAsync(string command, string arguments)
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            return await process.StandardOutput.ReadToEndAsync();
        }

        public static async Task<CustomNetworks> GetCustomNetworks(string l1Url, string l2Url)
        {
            var l1Provider = new RpcClient(new Uri(l1Url));
            var l2Provider = new RpcClient(new Uri(l2Url));

            //l1Provider.AddMiddleware(new GethPOAMiddleware(), 0);
            //l2Provider.AddMiddleware(new GethPOAMiddleware(), 0);

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

            var l1Network = new L1Network
            {
                BlockTime = 10,
                ChainID = ((int)(await new Web3(l1Provider).Eth.ChainId.SendRequestAsync()).Value),
                ExplorerUrl = "",
                IsCustom = true,
                Name = "EthLocal",
                PartnerChainIDs = new[] { (int)(await new Web3(l2Provider).Eth.ChainId.SendRequestAsync()).Value },
                IsArbitrum = false
            };

            var l2Network = new L2Network
            {
                ChainID = ((int)(await new Web3(l2Provider).Eth.ChainId.SendRequestAsync()).Value),
                IsCustom = true,
                Name = "ArbLocal",
                PartnerChainIDs = new[] { (int)(await new Web3(l1Provider).Eth.ChainId.SendRequestAsync()).Value },
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

        public static async Task<CustomNetworks> SetupNetworks(string l1Url, string l2Url, Account l1Deployer, Account l2Deployer)
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
            await adminErc20Bridger.SetGateways(l1Signer: l1Deployer, l2Provider: new Web3(l2Deployer.TransactionManager.Client), tokenGateways:
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
            if (key == null && provider == null)
            {
                throw new Exception("Provide at least one of key or provider.");
            }

            return key != null ? new Account(key) : new Account((await provider.Eth.Accounts.SendRequestAsync())[0]);
        }

        public static async Task<TestSetupResult> TestSetup()
        {
            // Assuming __src_directory is the directory of the current assembly file
            string assemblyDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string PROJECT_DIRECTORY = Path.GetDirectoryName(assemblyDirectory);

            var ethProvider = new RpcClient(new Uri(Config["ETH_URL"]));
            var arbProvider = new RpcClient(new Uri(Config["ARB_URL"]));

            //ethProvider.OverridingRequestInterceptor = new GethPOAMiddleware();
            //arbProvider.OverridingRequestInterceptor = new GethPOAMiddleware();

            var l1Deployer = new SignerOrProvider(await GetSigner(new Web3(ethProvider), Config["ETH_KEY"]), new Web3(ethProvider));
            var l2Deployer = new SignerOrProvider(await GetSigner(new Web3(arbProvider), Config["ARB_KEY"]), new Web3(arbProvider));

            var mnemo = new Mnemonic("stem true medal chronic lion machine mask road rabbit process movie account", Wordlist.English);
            var seed = mnemo.DeriveSeed();
            var ethWallet = new Wallet(seed);
            var account = new Account(ethWallet.GetPrivateKey(0));

            var l1SignerAddress = account.Address.ConvertToEthereumChecksumAddress();
            var l2SignerAddress = account.Address.ConvertToEthereumChecksumAddress();

            var signerPrivateKey = account.PrivateKey.EnsureHexPrefix();
            var signerAccount = new Account(signerPrivateKey);

            //ethProvider.OverridingRequestInterceptor = new GethPOAMiddleware();
            //arbProvider.OverridingRequestInterceptor = new GethPOAMiddleware();

            var l1Signer = new SignerOrProvider(signerAccount, new Web3(ethProvider));
            var l2Signer = new SignerOrProvider(signerAccount, new Web3(arbProvider));

            L1Network setL1Network;
            L2Network setL2Network;
            try
            {
                setL1Network = await NetworkUtils.GetL1NetworkAsync(ethProvider);
                setL2Network = await NetworkUtils.GetL2NetworkAsync(arbProvider);
            }
            catch (ArbSdkError)
            {
                var localNetworkFile = Path.Combine(PROJECT_DIRECTORY, "localNetwork.json");
                CustomNetworks networkData;
                if (File.Exists(localNetworkFile))
                {
                    var json = File.ReadAllText(localNetworkFile);
                    networkData = JsonConvert.DeserializeObject<CustomNetworks>(json);

                    setL1Network = new L1Network()
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

                    //networkData.L2Network.TokenBridge = new TokenBridge()
                    //{

                    //};
                    //networkData.L2Network.EthBridge = new EthBridge()
                    //{

                    //};
                    setL2Network = new L2Network()
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

                    NetworkUtils.AddCustomNetwork(setL1Network, setL2Network);
                }
                else
                {
                    networkData = await SetupNetworks(Config["ETH_URL"], Config["ARB_URL"], l1Deployer.Account, l2Deployer.Account);
                    setL1Network = networkData.L1Network;
                    setL2Network = networkData.L2Network;
                }
            }
            var erc20Bridger = new Erc20Bridger(setL2Network);
            var adminErc20Bridger = new AdminErc20Bridger(setL2Network);
            var ethBridger = new EthBridger(setL2Network);
            var inboxTools = new InboxTools(signerAccount, setL2Network);

            var result = new TestSetupResult
            {
                L1Signer = l1Signer.Account,
                L2Signer = l2Signer.Account,
                L1Network = setL1Network,
                L2Network = setL2Network,
                Erc20Bridger = erc20Bridger,
                AdminErc20Bridger = adminErc20Bridger,
                EthBridger = ethBridger,
                InboxTools = inboxTools,
                L1Deployer = l1Deployer.Account,
                L2Deployer = l2Deployer.Account
            };

            return result;
        }
    }

}