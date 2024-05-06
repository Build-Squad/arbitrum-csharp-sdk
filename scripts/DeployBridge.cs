using Arbitrum.DataEntities;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace YourNamespace
{
    public class ContractDeploymentHelper
    {
        private readonly Web3 ethProvider;
        private readonly Web3 arbProvider;

        public ContractDeploymentHelper(string ethUrl, string arbUrl)
        {
            ethProvider = new Web3(ethUrl);
            arbProvider = new Web3(arbUrl);
        }

        public async Task DeployERC20AndInit(SignerOrProvider l1Signer, SignerOrProvider l2Signer, string inboxAddress)
        {
            Console.WriteLine("Deploying L1 contracts...");
            var l1Contracts = DeployERC20L1(l1Signer);

            Console.WriteLine("Deploying L2 contracts...");
            var l2Contracts = DeployERC20L2(l2Signer);

            Console.WriteLine("Initializing L2 contracts...");
            await SendTransactionWrapper(l2Signer, l2Contracts["router"].Initialize(l1Contracts["router"].Address, l2Contracts["standardGateway"].Address));
            await SendTransactionWrapper(l2Signer, l2Contracts["beaconProxyFactory"].Initialize(l2Contracts["beacon"].Address));
            await SendTransactionWrapper(l2Signer, l2Contracts["standardGateway"].Initialize(l1Contracts["standardGateway"].Address, l2Contracts["router"].Address, l2Contracts["beaconProxyFactory"].Address));
            await SendTransactionWrapper(l2Signer, l2Contracts["customGateway"].Initialize(l1Contracts["customGateway"].Address, l2Contracts["router"].Address));
            await SendTransactionWrapper(l2Signer, l2Contracts["weth"].Initialize("WETH", "WETH", 18, l2Contracts["wethGateway"].Address, l1Contracts["weth"].Address));
            await SendTransactionWrapper(l2Signer, l2Contracts["wethGateway"].Initialize(l1Contracts["wethGateway"].Address, l2Contracts["router"].Address, l1Contracts["weth"].Address, l2Contracts["weth"].Address));

            Console.WriteLine("Initializing L1 contracts...");
            await SendTransactionWrapper(l1Signer, l1Contracts["router"].Initialize(l1Signer.Account.Address, l1Contracts["standardGateway"].Address, ADDRESS_ZERO, l2Contracts["router"].Address, inboxAddress));
            var cloneableProxyHash = await l2Signer.Provider.Eth.GetContractQueryHandler<string>(l2Contracts["beaconProxyFactory"].Address).QueryAsync<string>("cloneableProxyHash");
            await SendTransactionWrapper(l1Signer, l1Contracts["standardGateway"].Initialize(l2Contracts["standardGateway"].Address, l1Contracts["router"].Address, inboxAddress, cloneableProxyHash, l2Contracts["beaconProxyFactory"].Address));
            await SendTransactionWrapper(l1Signer, l1Contracts["customGateway"].Initialize(l2Contracts["customGateway"].Address, l1Contracts["router"].Address, inboxAddress, l1Signer.Account.Address));
            await SendTransactionWrapper(l1Signer, l1Contracts["wethGateway"].Initialize(l2Contracts["wethGateway"].Address, l1Contracts["router"].Address, inboxAddress, l1Contracts["weth"].Address, l2Contracts["weth"].Address));
        }

        private Dictionary<string, object> DeployERC20L1(SignerOrProvider deployer)
        {
            var proxyAdmin = DeployAbiContract(deployer, "ProxyAdmin", false);
            Console.WriteLine("proxy admin address " + proxyAdmin.Address);
            var router = DeployBehindProxy(deployer, "L1GatewayRouter", proxyAdmin, true);
            Console.WriteLine("router address " + router.Address);
            var standardGateway = DeployBehindProxy(deployer, "L1ERC20Gateway", proxyAdmin, true);
            Console.WriteLine("standard gateway address " + standardGateway.Address);
            var customGateway = DeployBehindProxy(deployer, "L1CustomGateway", proxyAdmin, true);
            Console.WriteLine("custom gateway address " + customGateway.Address);
            var wethGateway = DeployBehindProxy(deployer, "L1WethGateway", proxyAdmin, true);
            Console.WriteLine("weth gateway address " + wethGateway.Address);
            var weth = DeployAbiContract(deployer, "TestWETH9", new object[] { "WETH", "WETH" }, true);
            Console.WriteLine("weth address " + weth.Address);
            var multicall = DeployAbiContract(deployer, "Multicall2", true);
            Console.WriteLine("multicall address " + multicall.Address);

            return new Dictionary<string, object>
            {
                { "proxyAdmin", proxyAdmin },
                { "router", router },
                { "standardGateway", standardGateway },
                { "customGateway", customGateway },
                { "wethGateway", wethGateway },
                { "weth", weth },
                { "multicall", multicall }
            };
        }

        private Dictionary<string, object> DeployERC20L2(SignerOrProvider deployer)
        {
            var proxyAdmin = DeployAbiContract(deployer, "ProxyAdmin", false);
            Console.WriteLine("proxy admin address " + proxyAdmin.Address);
            var router = DeployBehindProxy(deployer, "L2GatewayRouter", proxyAdmin, true);
            Console.WriteLine("router address " + router.Address);
            var standardGateway = DeployBehindProxy(deployer, "L2ERC20Gateway", proxyAdmin, true);
            Console.WriteLine("standard gateway address " + standardGateway.Address);
            var customGateway = DeployBehindProxy(deployer, "L2CustomGateway", proxyAdmin, true);
            Console.WriteLine("custom gateway address " + customGateway.Address);
            var wethGateway = DeployBehindProxy(deployer, "L2WethGateway", proxyAdmin, true);
            Console.WriteLine("weth gateway address " + wethGateway.Address);
            var standardArbERC20 = DeployAbiContract(deployer, "StandardArbERC20", true);
            Console.WriteLine("standard arb erc20 address " + standardArbERC20.Address);
            var beacon = DeployAbiContract(deployer, "UpgradeableBeacon", new object[] { standardArbERC20.Address }, false);
            Console.WriteLine("beacon address " + beacon.Address);
            var beaconProxy = DeployBehindProxy(deployer, "BeaconProxyFactory", proxyAdmin, true);
            Console.WriteLine("beacon proxy address " + beaconProxy.Address);
            var weth = DeployBehindProxy(deployer, "AeWETH", proxyAdmin, true);
            Console.WriteLine("weth address " + weth.Address);
            var multicall = DeployAbiContract(deployer, "ArbMulticall2", true);
            Console.WriteLine("multicall address " + multicall.Address);

            return new Dictionary<string, object>
            {
                { "proxyAdmin", proxyAdmin },
                { "router", router },
                { "standardGateway", standardGateway },
                { "customGateway", customGateway },
                { "wethGateway", wethGateway },
                { "beacon", beacon },
                { "beaconProxyFactory", beaconProxy },
                { "weth", weth },
                { "multicall", multicall }
            };
        }

        private async Task SendTransactionWrapper(SignerOrProvider signer, object contractFunction)
        {
            var txHash = await ((Nethereum.Contracts.ContractHandler)contractFunction).SendTransactionAsync(signer.Account.Address);
            var txReceipt = await signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            // Handle transaction receipt
        }

        private object DeployBehindProxy(SignerOrProvider deployer, string contractName, object admin, bool isClassic = false)
        {
            // Your deploy_behind_proxy logic here
            return null;
        }

        private object DeployAbiContract(SignerOrProvider deployer, string contractName, object[] constructorArgs, bool isClassic = false)
        {
            // Your deploy_abi_contract logic here
            return null;
        }
    }
}
