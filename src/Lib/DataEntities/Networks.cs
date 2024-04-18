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
using System;
using System.Numerics;
using System.Xml.Linq;
using Nethereum.Signer;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Contracts;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.Util;

namespace Arbitrum.DataEntities
{
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
        public bool IsArbitrum { get; set; } ///////////
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
                        PartnerChainIDs = new[] { 42161, 42170 },
                        BlockTime = 14,
                        IsCustom = false,
                        IsArbitrum = false
                    }
                }
        };

        public static Dictionary<int, L2Network> l2Networks = new Dictionary<int, L2Network>()
        {
                {
                    42161, new L2Network
                    {
                        ChainID = 42161,
                        Name = "Arbitrum One",
                        ExplorerUrl = "https://arbiscan.io",
                        PartnerChainID = 1,
                        PartnerChainIDs = Array.Empty<int>(),
                        IsArbitrum = true,
                        TokenBridge = MainnetTokenBridge,
                        EthBridge = MainnetETHBridge,
                        ConfirmPeriodBlocks = 45818,
                        IsCustom = false,
                        RetryableLifetimeSeconds = Constants.SEVEN_DAYS_IN_SECONDS,
                        NitroGenesisBlock = 22207817,
                        NitroGenesisL1Block = 15447158,
                        DepositTimeout = 1800000,
                        BlockTime = Constants.ARB_MINIMUM_BLOCK_TIME_IN_SECONDS
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
                chainId = await GetChainIdAsync(((SignerOrProvider)signerOrProviderOrChainId).Provider);
            }
            else
            {
                throw new ArbSdkError($"Please provide a Web3 instance or chain ID. You have provided {signerOrProviderOrChainId.GetType()}");
            }

            Dictionary<int, Arbitrum.DataEntities.Network> networks;

            if (layer == 1)
            {
                networks = l1Networks.ToDictionary(entry => entry.Key, entry => (Arbitrum.DataEntities.Network)entry.Value);
            }
            else
            {
                networks = l2Networks.ToDictionary(entry => entry.Key, entry => (Arbitrum.DataEntities.Network)entry.Value);
            }

            if (networks.ContainsKey(chainId))
            {
                return networks[chainId];
            }
            else
            {
                throw new ArbSdkError($"Unrecognized network {chainId}.");
            }
        }

        public async static Task<int> GetChainIdAsync(IWeb3 web3)
        {
            return (int)(await web3.Eth.ChainId.SendRequestAsync()).Value;
        }

        public async static Task<int> GetChainIdAsync(SignerOrProvider signerOrProvider)
        {
            return (int)(await signerOrProvider.Provider.Eth.ChainId.SendRequestAsync()).Value;
        }


        public static async Task<L1Network> GetL1NetworkAsync(dynamic signerOrProviderOrChainId)
        {
            return (L1Network)await GetNetwork(signerOrProviderOrChainId, 1);
        }

        public static async Task<L2Network> GetL2NetworkAsync(dynamic signerOrProviderOrChainId)
        {
            return (L2Network)await GetNetwork(signerOrProviderOrChainId, 2);
        }

        public static async Task<EthBridgeInformation> GetEthBridgeInformation(string rollupContractAddress, SignerOrProvider l1SignerOrProvider)
        {
            Contract rollup = await LoadContractUtils.LoadContract("RollupAdminLogic", l1SignerOrProvider, rollupContractAddress, false);

            return new EthBridgeInformation
            {
                Bridge = rollup.GetFunction("Bridge").CallAsync<string>().Result,
                Inbox = rollup.GetFunction("Inbox").CallAsync<string>().Result,
                SequencerInbox = rollup.GetFunction("SequencerInbox").CallAsync<string>().Result,
                Outbox = rollup.GetFunction("Outbox").CallAsync<string>().Result,
                Rollup = rollupContractAddress,
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
                    throw new ArbSdkError($"Network {customL1ChainID} already included");
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
                throw new ArbSdkError($"Network {customL2ChainID} already included");
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
                PartnerChainIDs = new[] { 412346 },
                IsArbitrum = false,
            };

            var defaultLocalL2Network = new L2Network
            {
                ChainID = 412346,
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
