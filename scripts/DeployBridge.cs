using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Arbitrum.Scripts
{
    public class ERC20DeploymentResult
    {
        public Contract? ProxyAdmin { get; set; }
        public Contract? Router { get; set; }
        public Contract? StandardGateway { get; set; }
        public Contract? CustomGateway { get; set; }
        public Contract? WethGateway { get; set; }
        public Contract? Beacon { get; set; }
        public Contract? BeaconProxyFactory { get; set; }
        public Contract? Weth { get; set; }
        public Contract? Multicall { get; set; }
    }
    public static class DeploymentUtils
    {
        public static async Task<Contract> DeployBehindProxy(
            SignerOrProvider deployer,
            string contractName,
            Contract admin,
            string dataToCallProxy = "0x",
            bool isClassic = false)
        {
            var instance = await LoadContractUtils.DeployAbiContract(
                                    deployer.Provider,
                                    deployer,
                                    contractName,
                                    isClassic:isClassic);

            var (contractAbi, contractAddress) = await LogParser.LoadAbi(contractName, isClassic);

            var proxy = await LoadContractUtils.DeployAbiContract(
                                deployer.Provider,
                                deployer,
                                "TransparentUpgradeableProxy",
                                new object[] { instance.Address, admin.Address, dataToCallProxy },
                                isClassic);

            return deployer.Provider.Eth.GetContract(contractAddress: proxy.Address,abi: contractAbi);
        }

        public static async Task<ERC20DeploymentResult> DeployERC20L1(SignerOrProvider deployer)
        {
            var provider = deployer.Provider;

            var proxyAdmin = await LoadContractUtils.DeployAbiContract(provider, deployer, "ProxyAdmin", isClassic: false);
            Console.WriteLine("Proxy admin address: " + proxyAdmin.Address);

            var router = await DeployBehindProxy(deployer, "L1GatewayRouter", proxyAdmin, isClassic: true);
            Console.WriteLine("Router address: " + router.Address);

            var standardGateway = await DeployBehindProxy(deployer, "L1ERC20Gateway", proxyAdmin, isClassic: true);
            Console.WriteLine("Standard gateway address: " + standardGateway.Address);

            var customGateway = await DeployBehindProxy(deployer, "L1CustomGateway", proxyAdmin, isClassic: true);
            Console.WriteLine("Custom gateway address: " + customGateway.Address);

            var wethGateway = await DeployBehindProxy(deployer, "L1WethGateway", proxyAdmin, isClassic: true);
            Console.WriteLine("WETH gateway address: " + wethGateway.Address);

            var weth = await LoadContractUtils.DeployAbiContract(provider, deployer, "TestWETH9", new[] { "WETH", "WETH" }, isClassic: true);
            Console.WriteLine("WETH address: " + weth.Address);

            var multicall = await LoadContractUtils.DeployAbiContract(provider, deployer, "Multicall2", isClassic: true);
            Console.WriteLine("Multicall address: " + multicall.Address);

            return new ERC20DeploymentResult
            {
                ProxyAdmin = proxyAdmin,
                Router = router,
                StandardGateway = standardGateway,
                CustomGateway = customGateway,
                WethGateway = wethGateway,
                Weth = weth,
                Multicall = multicall
            };
        }

        public static async Task<ERC20DeploymentResult> DeployERC20L2(SignerOrProvider deployer)
        {
            var provider = deployer.Provider;

            var proxyAdmin = await LoadContractUtils.DeployAbiContract(provider, deployer, "ProxyAdmin", isClassic: false);
            Console.WriteLine("Proxy admin address: " + proxyAdmin.Address);

            var router = await DeployBehindProxy(deployer, "L2GatewayRouter", proxyAdmin, isClassic: true);
            Console.WriteLine("Router address: " + router.Address);

            var standardGateway = await DeployBehindProxy(deployer, "L2ERC20Gateway", proxyAdmin, isClassic: true);
            Console.WriteLine("Standard gateway address: " + standardGateway.Address);

            var customGateway = await DeployBehindProxy(deployer, "L2CustomGateway", proxyAdmin, isClassic: true);
            Console.WriteLine("Custom gateway address: " + customGateway.Address);

            var wethGateway = await DeployBehindProxy(deployer, "L2WethGateway", proxyAdmin, isClassic: true);
            Console.WriteLine("WETH gateway address: " + wethGateway.Address);

            var standardArbERC20 = await LoadContractUtils.DeployAbiContract(provider, deployer, "StandardArbERC20", isClassic: true);
            Console.WriteLine("Standard ArbERC20 address: " + standardArbERC20.Address);

            var beacon = await LoadContractUtils.DeployAbiContract(provider, deployer, "UpgradeableBeacon", new[] { standardArbERC20.Address }, isClassic: false);
            Console.WriteLine("Beacon address: " + beacon.Address);

            var beaconProxyFactory = await LoadContractUtils.DeployAbiContract(provider, deployer, "BeaconProxyFactory", isClassic: true);
            Console.WriteLine("Beacon proxy address: " + beaconProxyFactory.Address);

            var weth = await DeployBehindProxy(deployer, "AeWETH", proxyAdmin, isClassic: true);
            Console.WriteLine("WETH address: " + weth.Address);

            var multicall = await LoadContractUtils.DeployAbiContract(provider, deployer, "ArbMulticall2", isClassic: true);
            Console.WriteLine("Multicall address: " + multicall.Address);

            return new ERC20DeploymentResult
            {
                ProxyAdmin = proxyAdmin,
                Router = router,
                StandardGateway = standardGateway,
                CustomGateway = customGateway,
                WethGateway = wethGateway,
                Beacon = beacon,
                BeaconProxyFactory = beaconProxyFactory,
                Weth = weth,
                Multicall = multicall
            };
        }
        public static async Task<TransactionReceipt> SendTransactionWrapper(SignerOrProvider signer, Function contractFunction, params object[] functionInput)
        {
            var txHash = await contractFunction.SendTransactionAsync(signer.Account.Address);
            var txReceipt = await signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            return txReceipt;
        }

        public static async Task<Tuple<ERC20DeploymentResult, ERC20DeploymentResult>> DeployERC20AndInit(SignerOrProvider l1Signer, SignerOrProvider l2Signer, string inboxAddress)
        {
            Console.WriteLine("Deploying L1 contracts...");
            var l1Contracts = await DeployERC20L1(l1Signer);

            Console.WriteLine("Deploying L2 contracts...");
            var l2Contracts = await DeployERC20L2(l2Signer);

            Console.WriteLine("Initializing L2 contracts...");
            await SendTransactionWrapper(
                l2Signer,
                l2Contracts.Router.GetFunction("initialize"),
                new object[] { l1Contracts.Router.Address, l2Contracts.StandardGateway.Address });

            await SendTransactionWrapper(
                l2Signer,
                l2Contracts.StandardGateway.GetFunction("initialize"),
                new object[] { l1Contracts.StandardGateway.Address, l2Contracts.Router.Address, l2Contracts.BeaconProxyFactory.Address });

            await SendTransactionWrapper(
                l2Signer,
                l2Contracts.CustomGateway.GetFunction("initialize"),
                new object[] { l1Contracts.CustomGateway.Address, l2Contracts.Router.Address });

            await SendTransactionWrapper(
                l2Signer,
                l2Contracts.Weth.GetFunction("initialize"),
                new object[] { "WETH", "WETH", 18, l2Contracts.WethGateway.Address, l1Contracts.Weth.Address });

            await SendTransactionWrapper(
                l2Signer,
                l2Contracts.WethGateway.GetFunction("initialize"),
                new object[] { l1Contracts.WethGateway.Address, l2Contracts.Router.Address, l1Contracts.Weth.Address, l2Contracts.Weth.Address });


            Console.WriteLine("Initializing L1 contracts...");

            await SendTransactionWrapper(
                l1Signer,
                l1Contracts.Router.GetFunction("initialize"),
                new object[] { l1Signer.Account.Address, l1Contracts.StandardGateway.Address, Constants.ADDRESS_ZERO, l2Contracts.Router.Address, inboxAddress });

            var cloneableProxyHash = await l2Signer.Provider.Eth.GetContract(l2Contracts.BeaconProxyFactory.Address, (await LogParser.LoadAbi("BeaconProxyFactory")).Item1)
                .GetFunction("cloneableProxyHash").CallAsync<string>();

            await SendTransactionWrapper(
                l1Signer,
                l1Contracts.StandardGateway.GetFunction("initialize"),
                new object[] { l2Contracts.StandardGateway.Address, l1Contracts.Router.Address, inboxAddress, cloneableProxyHash, l2Contracts.BeaconProxyFactory.Address });

            await SendTransactionWrapper(
                l1Signer,
                l1Contracts.CustomGateway.GetFunction("initialize"),
                new object[] { l2Contracts.CustomGateway.Address, l1Contracts.Router.Address, inboxAddress, l1Signer.Account.Address });

            await SendTransactionWrapper(
                l1Signer,
                l1Contracts.WethGateway.GetFunction("initialize"),
                new object[] { l2Contracts.WethGateway.Address, l1Contracts.Router.Address, inboxAddress, l1Contracts.Weth.Address, l2Contracts.Weth.Address });


            return new Tuple<ERC20DeploymentResult, ERC20DeploymentResult>(l1Contracts, l2Contracts);
        }
    }
}