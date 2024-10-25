/*
 * Copyright 2021, Offchain Labs, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using Arbitrum.Utils;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.Web3;

namespace Arbitrum.DataEntities
{
    public class INetwork
    {
        public TokenBridge? TokenBridge { get; set; }
        public EthBridge? EthBridge { get; set; }
        public int PartnerChainID { get; set; }
        public bool IsArbitrum { get; set; }
        public int ConfirmPeriodBlocks { get; set; }
        public int RetryableLifetimeSeconds { get; set; }
        public int NitroGenesisBlock { get; set; }
        public int NitroGenesisL1Block { get; set; }
        public int DepositTimeout { get; set; }
        public string? NativeToken { get; set; }
        public int ChainID { get; set; }
        public string? Name { get; set; }
        public string? ExplorerUrl { get; set; }
        public string? Gif { get; set; }
        public bool IsCustom { get; set; }
        public double BlockTime { get; set; }
        public int[]? PartnerChainIDs { get; set; }
    }
    public class Network
    {
        public int ChainID { get; set; }
        public string? Name { get; set; }
        public string? ExplorerUrl { get; set; }
        public string? Gif { get; set; }
        public bool IsCustom { get; set; }
        /* Minimum possible block time for the chain (in seconds). */
        public double BlockTime { get; set; }
        /* Chain ids of children chains, i.e. chains that settle to this chain. */
        public int[]? PartnerChainIDs { get; set; }
    }

    /**
     * Represents an L1 chain, e.g. Ethereum Mainnet or Sepolia.
     */
    public class L1Network : Network
    {
        public bool IsArbitrum { get; set; }  ///////
    }

    /**
     * Represents an Arbitrum chain, e.g. Arbitrum One, Arbitrum Sepolia, or an L3 chain.
     */
    public class L2Network : Network
    {
        public TokenBridge? TokenBridge { get; set; }
        public EthBridge? EthBridge { get; set; }
        /**
           * Chain id of the parent chain, i.e. the chain on which this chain settles to.
           */
        public int PartnerChainID { get; set; }
        public bool IsArbitrum { get; set; } 
        public int ConfirmPeriodBlocks { get; set; }
        public int RetryableLifetimeSeconds { get; set; }
        public int NitroGenesisBlock { get; set; }
        public int NitroGenesisL1Block { get; set; }
        /**
         * How long to wait (ms) for a deposit to arrive on l2 before timing out a request
         */
        public int DepositTimeout { get; set; }
        /**
           * In case of a chain that uses ETH as its native/gas token, this is either `undefined` or the zero address
           *
           * In case of a chain that uses an ERC-20 token from the parent chain as its native/gas token, this is the address of said token on the parent chain
           */
        public string? NativeToken { get; set; }

    }

    public class TokenBridge
    {
        public string? L1GatewayRouter { get; set; }
        public string? L2GatewayRouter { get; set; }
        public string? L1ERC20Gateway { get; set; }
        public string? L2ERC20Gateway { get; set; }
        public string? L1CustomGateway { get; set; }
        public string? L2CustomGateway { get; set; }
        public string? L1WethGateway { get; set; }
        public string? L2WethGateway { get; set; }
        public string? L2Weth { get; set; }
        public string? L1Weth { get; set; }
        public string? L1ProxyAdmin { get; set; }
        public string? L2ProxyAdmin { get; set; }
        public string? L1MultiCall { get; set; }
        public string? L2Multicall { get; set; }
    }

    public class EthBridge
    {
        public string? Bridge { get; set; }
        public string? Inbox { get; set; }
        public string? SequencerInbox { get; set; }
        public string? Outbox { get; set; }
        public string? Rollup { get; set; }
        public Dictionary<string, int>? ClassicOutboxes { get; set; }
    }

    public class L1Networks
    {
        public Dictionary<int, L1Network>? L1NetworksDict { get; set; }
    }
    public class L2Networks
    {
        public Dictionary<int, L2Network>? L2NetworksDict { get; set; }
    }
    public class EthBridgeInformation
    {
        public string? Bridge { get; set; }
        public string? Inbox { get; set; }
        public string? SequencerInbox { get; set; }
        public string? Outbox { get; set; }
        public string? Rollup { get; set; }
    }

    public static class NetworkUtils
    {


        public static readonly TokenBridge MainnetTokenBridge = new TokenBridge
        {
            L1GatewayRouter = "0x72Ce9c846789fdB6fC1f34aC4AD25Dd9ef7031ef",
            L2GatewayRouter = "0x5288c571Fd7aD117beA99bF60FE0846C4E84F933",
            L1ERC20Gateway = "0xa3A7B6F88361F48403514059F1F16C8E78d60EeC",
            L2ERC20Gateway = "0x09e9222E96E7B4AE2a407B98d48e330053351EEe",
            L1CustomGateway = "0xcEe284F754E854890e311e3280b767F80797180d",
            L2CustomGateway = "0x096760F208390250649E3e8763348E783AEF5562",
            L1WethGateway = "0xd92023E9d9911199a6711321D1277285e6d4e2db",
            L2WethGateway = "0x6c411aD3E74De3E7Bd422b94A27770f5B86C623B",
            L2Weth = "0x82aF49447D8a07e3bd95BD0d56f35241523fBab1",
            L1Weth = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
            L1ProxyAdmin = "0x9aD46fac0Cf7f790E5be05A0F15223935A0c0aDa",
            L2ProxyAdmin = "0xd570aCE65C43af47101fC6250FD6fC63D1c22a86",
            L1MultiCall = "0x5ba1e12693dc8f9c48aad8770482f4739beed696",
            L2Multicall = "0x842eC2c7D803033Edf55E478F461FC547Bc54EB2"
        };

        public static readonly EthBridge MainnetETHBridge = new EthBridge
        {
            Bridge = "0x8315177aB297bA92A06054cE80a67Ed4DBd7ed3a",
            Inbox = "0x4Dbd4fc535Ac27206064B68FfCf827b0A60BAB3f",
            SequencerInbox = "0x1c479675ad559DC151F6Ec7ed3FbF8ceE79582B6",
            Outbox = "0x0B9857ae2D4A3DBe74ffE1d7DF045bb7F96E4840",
            Rollup = "0x5eF0D09d1E6204141B4d37530808eD19f60FBa35",
            ClassicOutboxes = new Dictionary<string, int>
            {
                { "0x667e23ABd27E623c11d4CC00ca3EC4d0bD63337a", 0 },
                { "0x760723CD2e632826c38Fef8CD438A4CC7E7E1A40", 30 },
            }
        };

        /**
         * Storage for all networks, either L1, L2 or L3.
         */
        public static Dictionary<int, L1Network> l1Networks = new Dictionary<int, L1Network>()
        {
            {
                1, new L1Network
                {
                    ChainID = 1,
                    Name = "Mainnet",
                    ExplorerUrl = "https://etherscan.io",
                    PartnerChainIDs = new[] { 42161, 42170},
                    BlockTime = 14,
                    IsCustom = false,
                    IsArbitrum = false
                }
            },
            {
                1338, new L1Network
                {
                    ChainID = 1338,
                    Name = "Hardhat_Mainnet_Fork",
                    ExplorerUrl = "https://etherscan.io",
                    PartnerChainIDs = new[] { 42161 },
                    BlockTime = 1,
                    IsCustom = false,
                    IsArbitrum = false
                }
            },
            {
                11155111, new L1Network
                {
                    ChainID = 11155111,
                    Name = "Sepolia",
                    ExplorerUrl = "https://sepolia.etherscan.io",
                    PartnerChainIDs = new[] { 421614 },
                    BlockTime = 12,
                    IsCustom = false,
                    IsArbitrum = false
                }
            },
            {
                17000, new L1Network
                {
                    ChainID = 17000,
                    Name = "Holesky",
                    ExplorerUrl = "https://holesky.etherscan.io",
                    PartnerChainIDs = Array.Empty<int>(),
                    BlockTime = 12,
                    IsCustom = false,
                    IsArbitrum = false
                }
            }
        };

        public static Dictionary<int, L2Network> l2Networks = new()
        {
                {
                  41234, new L2Network {
                    ChainID = 41234,
                    ConfirmPeriodBlocks = 45818,
                    RetryableLifetimeSeconds = Constants.SEVEN_DAYS_IN_SECONDS,
                    EthBridge = new EthBridge
                    {
                        Bridge = "0x8315177aB297bA92A06054cE80a67Ed4DBd7ed3a",
                        Inbox = "0x4Dbd4fc535Ac27206064B68FfCf827b0A60BAB3f",
                        Outbox = "0x0B9857ae2D4A3DBe74ffE1d7DF045bb7F96E4840",
                        Rollup = "0x5eF0D09d1E6204141B4d37530808eD19f60FBa35",
                        SequencerInbox = "0x1c479675ad559DC151F6Ec7ed3FbF8ceE79582B6"
                    },
                    IsArbitrum = true,
                    IsCustom = false,
                    Name = "Arbitrum One",
                    PartnerChainID = 1,
                    TokenBridge = new TokenBridge
                    {
                        L1CustomGateway = "0xcEe284F754E854890e311e3280b767F80797180d",
                        L1ERC20Gateway = "0xa3A7B6F88361F48403514059F1F16C8E78d60EeC",
                        L1GatewayRouter = "0x72Ce9c846789fdB6fC1f34aC4AD25Dd9ef7031ef",
                        L1MultiCall = "0x5ba1e12693dc8f9c48aad8770482f4739beed696",
                        L1ProxyAdmin = "0x9aD46fac0Cf7f790E5be05A0F15223935A0c0aDa",
                        L1Weth = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
                        L1WethGateway = "0xd92023E9d9911199a6711321D1277285e6d4e2db",
                        L2CustomGateway = "0x096760F208390250649E3e8763348E783AEF5562",
                        L2ERC20Gateway = "0x09e9222E96E7B4AE2a407B98d48e330053351EEe",
                        L2GatewayRouter = "0x5288c571Fd7aD117beA99bF60FE0846C4E84F933",
                        L2Multicall = "0x842eC2c7D803033Edf55E478F461FC547Bc54EB2",
                        L2ProxyAdmin = "0xd570aCE65C43af47101fC6250FD6fC63D1c22a86",
                        L2Weth = "0x82aF49447D8a07e3bd95BD0d56f35241523fBab1",
                        L2WethGateway = "0x6c411aD3E74De3E7Bd422b94A27770f5B86C623B"
                    },
                    NitroGenesisBlock = 0,
                    NitroGenesisL1Block = 0,
                    DepositTimeout = 3960000
                  }
            },
            { 42170, new L2Network
                {
                    ChainID = 42170,
                    ConfirmPeriodBlocks = 45818,
                    EthBridge = new EthBridge
                    {
                        Bridge = "0xC1Ebd02f738644983b6C4B2d440b8e77DdE276Bd",
                        Inbox = "0xc4448b71118c9071Bcb9734A0EAc55D18A153949",
                        Outbox = "0xD4B80C3D7240325D18E645B49e6535A3Bf95cc58",
                        Rollup = "0xFb209827c58283535b744575e11953DCC4bEAD88",
                        SequencerInbox = "0x211E1c4c7f1bF5351Ac850Ed10FD68CFfCF6c21b"
                    },
                    ExplorerUrl = "https=//nova.arbiscan.io",
                    IsArbitrum = true,
                    IsCustom = false,
                    Name = "Arbitrum Nova",
                    PartnerChainID = 1,
                    RetryableLifetimeSeconds = Constants.SEVEN_DAYS_IN_SECONDS,
                    TokenBridge = new TokenBridge
                    {
                        L1CustomGateway = "0x23122da8C581AA7E0d07A36Ff1f16F799650232f",
                        L1ERC20Gateway = "0xB2535b988dcE19f9D71dfB22dB6da744aCac21bf",
                        L1GatewayRouter = "0xC840838Bc438d73C16c2f8b22D2Ce3669963cD48",
                        L1MultiCall = "0x8896D23AfEA159a5e9b72C9Eb3DC4E2684A38EA3",
                        L1ProxyAdmin = "0xa8f7DdEd54a726eB873E98bFF2C95ABF2d03e560",
                        L1Weth = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2",
                        L1WethGateway = "0xE4E2121b479017955Be0b175305B35f312330BaE",
                        L2CustomGateway = "0xbf544970E6BD77b21C6492C281AB60d0770451F4",
                        L2ERC20Gateway = "0xcF9bAb7e53DDe48A6DC4f286CB14e05298799257",
                        L2GatewayRouter = "0x21903d3F8176b1a0c17E953Cd896610Be9fFDFa8",
                        L2Multicall = "0x5e1eE626420A354BbC9a95FeA1BAd4492e3bcB86",
                        L2ProxyAdmin = "0xada790b026097BfB36a5ed696859b97a96CEd92C",
                        L2Weth = "0x722E8BdD2ce80A4422E880164f2079488e115365",
                        L2WethGateway = "0x7626841cB6113412F9c88D3ADC720C9FAC88D9eD"
                    },
                    NitroGenesisBlock = 0,
                    NitroGenesisL1Block = 0,
                    DepositTimeout = 1800000
                }
            },
            { 421614, new L2Network
                {
                    ChainID = 421614,
                    ConfirmPeriodBlocks = 20,
                    EthBridge = new EthBridge
                    {
                        Bridge = "0x38f918D0E9F1b721EDaA41302E399fa1B79333a9",
                        Inbox = "0xaAe29B0366299461418F5324a79Afc425BE5ae21",
                        Outbox = "0x65f07C7D521164a4d5DaC6eB8Fac8DA067A3B78F",
                        Rollup = "0xd80810638dbDF9081b72C1B33c65375e807281C8",
                        SequencerInbox = "0x6c97864CE4bEf387dE0b3310A44230f7E3F1be0D"
                    },
                    ExplorerUrl = "https://sepolia-explorer.arbitrum.io",
                    IsArbitrum = true,
                    IsCustom = false,
                    Name = "Arbitrum Rollup Sepolia Testnet",
                    PartnerChainID = 11155111,
                    RetryableLifetimeSeconds = Constants.SEVEN_DAYS_IN_SECONDS,
                    TokenBridge = new TokenBridge
                    {
                        L1CustomGateway = "0xba2F7B6eAe1F9d174199C5E4867b563E0eaC40F3",
                        L1ERC20Gateway = "0x902b3E5f8F19571859F4AB1003B960a5dF693aFF",
                        L1GatewayRouter = "0xcE18836b233C83325Cc8848CA4487e94C6288264",
                        L1MultiCall = "0xded9AD2E65F3c4315745dD915Dbe0A4Df61b2320",
                        L1ProxyAdmin = "0xDBFC2FfB44A5D841aB42b0882711ed6e5A9244b0",
                        L1Weth = "0x7b79995e5f793A07Bc00c21412e50Ecae098E7f9",
                        L1WethGateway = "0xA8aD8d7e13cbf556eE75CB0324c13535d8100e1E",
                        L2CustomGateway = "0x8Ca1e1AC0f260BC4dA7Dd60aCA6CA66208E642C5",
                        L2ERC20Gateway = "0x6e244cD02BBB8a6dbd7F626f05B2ef82151Ab502",
                        L2GatewayRouter = "0x9fDD1C4E4AA24EEc1d913FABea925594a20d43C7",
                        L2Multicall = "0xA115146782b7143fAdB3065D86eACB54c169d092",
                        L2ProxyAdmin = "0x715D99480b77A8d9D603638e593a539E21345FdF",
                        L2Weth = "0x980B62Da83eFf3D4576C647993b0c1D7faf17c73",
                        L2WethGateway = "0xCFB1f08A4852699a979909e22c30263ca249556D"
                    },
                    NitroGenesisBlock = 0,
                    NitroGenesisL1Block = 0,
                    DepositTimeout = 1800000
                }
            },
            { 23011913, new L2Network
                {
                    ChainID = 23011913,
                    ConfirmPeriodBlocks = 20,
                    EthBridge = new EthBridge
                    {
                        Bridge = "0x35aa95ac4747D928E2Cd42FE4461F6D9d1826346",
                        Inbox = "0xe1e3b1CBaCC870cb6e5F4Bdf246feB6eB5cD351B",
                        Outbox = "0x98fcA8bFF38a987B988E54273Fa228A52b62E43b",
                        Rollup = "0x94db9E36d9336cD6F9FfcAd399dDa6Cc05299898",
                        SequencerInbox = "0x00A0F15b79d1D3e5991929FaAbCF2AA65623530c"
                    },
                    ExplorerUrl = "https://stylus-testnet-explorer.arbitrum.io",
                    IsArbitrum = true,
                    IsCustom = false,
                    Name = "Stylus Testnet",
                    PartnerChainID = 421614,
                    RetryableLifetimeSeconds = Constants.SEVEN_DAYS_IN_SECONDS,
                    TokenBridge = new TokenBridge
                    {
                        L1CustomGateway = "0xd624D491A5Bc32de52a2e1481846752213bF7415",
                        L1ERC20Gateway = "0x7348Fdf6F3e090C635b23D970945093455214F3B",
                        L1GatewayRouter = "0x0057892cb8bb5f1cE1B3C6f5adE899732249713f",
                        L1MultiCall = "0xBEbe3BfBF52FFEA965efdb3f14F2101c0264c940",
                        L1ProxyAdmin = "0xB9E77732f32831f09e2a50D6E71B2Cca227544bf",
                        L1Weth = "0x980B62Da83eFf3D4576C647993b0c1D7faf17c73",
                        L1WethGateway = "0x39845e4a230434D218b907459a305eBA61A790d4",
                        L2CustomGateway = "0xF6dbB0e312dF4652d59ce405F5E00CC3430f19c5",
                        L2ERC20Gateway = "0xe027f79CE40a1eF8e47B51d0D46Dc4ea658C5860",
                        L2GatewayRouter = "0x4c3a1f7011F02Fe4769fC704359c3696a6A60D89",
                        L2Multicall = "0xEb4A260FD16aaf18c04B1aeaDFE20E622e549bd3",
                        L2ProxyAdmin = "0xE914c0d417E8250d0237d2F4827ed3612e6A9C3B",
                        L2Weth = "0x61Dc4b961D2165623A25EB775260785fE78BD37C",
                        L2WethGateway = "0x7021B4Edd9f047772242fc948441d6e0b9121175"
                    },
                    NitroGenesisBlock = 0,
                    NitroGenesisL1Block = 0,
                    DepositTimeout = 900000
                }
            }
        };

        public async static Task<Network> GetNetwork(dynamic signerOrProviderOrChainId, int layer)
        {
            int chainId;

            if (signerOrProviderOrChainId is int)
            {
                chainId = (int)signerOrProviderOrChainId;
            }
            else if (signerOrProviderOrChainId is Web3)
            {
                chainId = await GetChainIdAsync((Web3)signerOrProviderOrChainId);
            }
            else if (signerOrProviderOrChainId is SignerOrProvider)
            {
                chainId = await GetChainIdAsync(((SignerOrProvider)signerOrProviderOrChainId)?.Provider!);
            }
            else if(signerOrProviderOrChainId is IClient)
            {
                chainId = await GetChainIdAsync(new Web3(signerOrProviderOrChainId));
            }
            else if (signerOrProviderOrChainId is IWeb3)
            {
                chainId = await GetChainIdAsync(new Web3(signerOrProviderOrChainId));
            }
            else if(signerOrProviderOrChainId is RpcClient)
            {
                chainId = await GetChainIdAsync(new Web3(signerOrProviderOrChainId));
            }
            else if(signerOrProviderOrChainId is EthChainId)
            {
                chainId = await GetChainIdAsync(new Web3(signerOrProviderOrChainId.Client));
            }
            else
            {
                throw new ArbSdkError($"Please provide a Web3 instance or chain ID. You have provided {signerOrProviderOrChainId.GetType()}");
            }

            Dictionary<int, Network> networks;

            if (layer == 1)
            {
                networks = l1Networks.ToDictionary(entry => entry.Key, entry => (Arbitrum.DataEntities.Network)entry.Value);
            }
            else
            {
                networks = l2Networks.ToDictionary(entry => entry.Key, entry => (Arbitrum.DataEntities.Network)entry.Value);
            }

            if (networks.TryGetValue(chainId, out Network? value))
            {
                return value;
            }
            else
            {
                throw new ArbSdkError($"Unrecognized network {chainId}.");
            }
        }

        public async static Task<int> GetChainIdAsync(Web3 web3)
        {
            return (int)(await web3.Eth.ChainId.SendRequestAsync()).Value;
        }

        public async static Task<int> GetChainIdAsync(SignerOrProvider signerOrProvider)
        {
            return (int)(await signerOrProvider.Provider.Eth.ChainId.SendRequestAsync()).Value;
        }


        public static async Task<L1Network> GetL1Network(dynamic signerOrProviderOrChainId)
        {
            return (L1Network)await GetNetwork(signerOrProviderOrChainId, 1);
        }

        public static async Task<L2Network> GetL2Network(dynamic signerOrProviderOrChainId)
        {
            return (L2Network)await GetNetwork(signerOrProviderOrChainId, 2);
        }

        public static async Task<EthBridgeInformation> GetEthBridgeInformation(string rollup_contract_address, Web3 l1Provider)
        {
            var contract = await LoadContractUtils.LoadContract("RollupAdminLogic", l1Provider, rollup_contract_address, false);

            return new EthBridgeInformation
            {
                Bridge = await contract.GetFunction("bridge").CallAsync<string>(),
                Inbox = await contract.GetFunction("inbox").CallAsync<string>(),
                SequencerInbox = await contract.GetFunction("sequencerInbox").CallAsync<string>(),
                Outbox = await contract.GetFunction("outbox").CallAsync<string>(),
                Rollup = rollup_contract_address
            };
        }

        /**
         * Registers a pair of custom L1 and L2 chains, or a single custom Arbitrum chain (L2 or L3).
         *
         * @param customL1Network the custom L1 chain
         * @param customL2Network the custom L2 or L3 chain
         */
        public static void AddCustomNetwork(L1Network? customL1Network, L2Network customL2Network)
        {
            if (customL1Network == null && customL2Network == null)
            {
                throw new ArgumentNullException(nameof(customL1Network), "Both customL1Network and customL2Network cannot be null.");
            }
            if (customL1Network != null)
            {
                int customL1ChainID = customL1Network.ChainID;

                if (l1Networks.ContainsKey(customL1ChainID))
                {
                    return;
                }
                else if (!customL1Network.IsCustom)
                {
                    throw new ArbSdkError($"Custom network {customL1ChainID} must have isCustom flag set to true");
                }
                else
                {
                    l1Networks[customL1ChainID] = customL1Network;
                }
            }

            int customL2ChainID = customL2Network.ChainID;

            if (l2Networks.ContainsKey(customL2ChainID))
            {
                throw new ArbSdkError($"ChainId {customL2ChainID} already included.");
            }
            else if (!customL2Network.IsCustom)
            {
                throw new ArbSdkError($"Custom network {customL2ChainID} must have isCustom flag set to true");
            }

            l2Networks[customL2ChainID] = customL2Network;

            L1Network? l1PartnerChain = l1Networks.GetValueOrDefault(customL2Network.PartnerChainID);

            if (l1PartnerChain == null)
            {
                throw new ArbSdkError($"Network {customL2Network.ChainID}'s partner network, {customL2Network.PartnerChainID}, not recognized");
            }

            if (!l1PartnerChain.PartnerChainIDs!.Contains(customL2Network.ChainID))
            {
                l1PartnerChain.PartnerChainIDs!.Append(customL2Network.ChainID);
            }
        }


        /**
         * Registers a custom network that matches the one created by a Nitro local node. Useful in development.
         *
         * @see {@link https://github.com/OffchainLabs/nitro}
         */
        public static (L1Network l1Network, L2Network l2Network) AddDefaultLocalNetwork()
        {
            var defaultLocalL1Network = new L1Network
            {
                BlockTime = 10,
                ChainID = 1337,
                ExplorerUrl = string.Empty,
                IsCustom = true,
                Name = "EthLocal",
                PartnerChainIDs = new[] { 42161 },
                IsArbitrum = false,
            };

            var defaultLocalL2Network = new L2Network
            {
                ChainID = 42161,
                ConfirmPeriodBlocks = 20,
                ExplorerUrl = string.Empty,
                IsArbitrum = true,
                IsCustom = true,
                Name = "ArbLocal",
                PartnerChainID = 1337,
                PartnerChainIDs = Array.Empty<int>(),
                RetryableLifetimeSeconds = 604800,
                NitroGenesisBlock = 0,
                NitroGenesisL1Block = 0,
                DepositTimeout = 900000,
                TokenBridge = new TokenBridge
                {
                    L1CustomGateway = "0x3DF948c956e14175f43670407d5796b95Bb219D8",
                    L1ERC20Gateway = "0x4A2bA922052bA54e29c5417bC979Daaf7D5Fe4f4",
                    L1GatewayRouter = "0x525c2aBA45F66987217323E8a05EA400C65D06DC",
                    L1MultiCall = "0xDB2D15a3EB70C347E0D2C2c7861cAFb946baAb48",
                    L1ProxyAdmin = "0xe1080224B632A93951A7CFA33EeEa9Fd81558b5e",
                    L1Weth = "0x408Da76E87511429485C32E4Ad647DD14823Fdc4",
                    L1WethGateway = "0xF5FfD11A55AFD39377411Ab9856474D2a7Cb697e",
                    L2CustomGateway = "0x525c2aBA45F66987217323E8a05EA400C65D06DC",
                    L2ERC20Gateway = "0xe1080224B632A93951A7CFA33EeEa9Fd81558b5e",
                    L2GatewayRouter = "0x1294b86822ff4976BfE136cB06CF43eC7FCF2574",
                    L2Multicall = "0xDB2D15a3EB70C347E0D2C2c7861cAFb946baAb48",
                    L2ProxyAdmin = "0xda52b25ddB0e3B9CC393b0690Ac62245Ac772527",
                    L2Weth = "0x408Da76E87511429485C32E4Ad647DD14823Fdc4",
                    L2WethGateway = "0x4A2bA922052bA54e29c5417bC979Daaf7D5Fe4f4"
                },
                EthBridge = new EthBridge
                {
                    Bridge = "0x2b360A9881F21c3d7aa0Ea6cA0De2a3341d4eF3C",
                    Inbox = "0xfF4a24b22F94979E9ba5f3eb35838AA814bAD6F1",
                    Outbox = "0x49940929c7cA9b50Ff57a01d3a92817A414E6B9B",
                    Rollup = "0x65a59D67Da8e710Ef9A01eCa37f83f84AEdeC416",
                    SequencerInbox = "0xE7362D0787b51d8C72D504803E5B1d6DcdA89540",
                },
                BlockTime = Constants.ARB_MINIMUM_BLOCK_TIME_IN_SECONDS,
            };

            var result = new Dictionary<string, object>
            {
                { "l1_network", defaultLocalL1Network },
                { "l2_network", defaultLocalL2Network }
            };

            return (defaultLocalL1Network, defaultLocalL2Network);
        }

        public static bool IsL1Network(Network network)
        {
            if (network is L1Network)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
