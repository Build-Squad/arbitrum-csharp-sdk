using System;
using System.IO;
using System.Text.Json;
using Nethereum.Contracts;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;
using Arbitrum.Lib.Utils;

namespace Arbitrum.Utils
{
    public class LoadContractException : Exception
    {
        public LoadContractException(string message) : base(message)
        {
        }
    }

    public class LoadContractUtils
    {
        private class ContractData
        {
            public string[] Abi { get; set; }
            public string Bytecode { get; set; }
        }

        public static Contract LoadContract(string contractName, IWeb3 provider, string address = null, bool isClassic = false)
        {
            var web3Provider = GetWeb3Provider(provider);
            var web3 = new Web3(web3Provider);

            string filePath;
            if (isClassic)
            {
                filePath = $"src/abi/classic/{contractName}.json";
            }
            else
            {
                filePath = $"src/abi/{contractName}.json";
            }

            using (var abiFile = File.OpenText(filePath))
            {
                var contractData = JsonSerializer.Deserialize<ContractData>(abiFile.ReadToEnd());
                if (contractData == null || contractData.Abi == null)
                {
                    throw new Exception($"No ABI found for contract: {contractName}");
                }

                var abi = string.Join(",", contractData.Abi);
                var bytecode = contractData.Bytecode;

                if (address != null)
                {
                    var contractAddress = GetChecksumAddress(address);

                    if (string.IsNullOrEmpty(bytecode))
                    {
                        return web3.Eth.GetContract(abi, contractAddress);
                    }
                    //else
                    //{
                    //    return web3.Eth.GetContract(abi, bytecode, contractAddress);
                    //}
                    return null; ///////
                }
                else
                {
                    if (string.IsNullOrEmpty(bytecode))
                    {
                        return web3.Eth.GetContract<object>(abi);
                    }

                    else
                    {
                        return web3.Eth.GetContract(abi, bytecode);
                    }
                }
            }
        }

        private static IClient GetWeb3Provider(IWeb3 provider)
        {
            if (provider is SignerOrProvider signerOrProvider)
            {
                return (IClient)signerOrProvider.Provider;
            }
            //else if (provider is ArbitrumProvider arbitrumProvider)
            //{
            //    return (IClient)arbitrumProvider.Provider;
            //}
            else
            {
                return (IClient)provider;
            }
        }

        private static string GetChecksumAddress(string address)
        {
            return Web3.ToChecksumAddress(address);
        }
    }
}
