using System;
using System.IO;
using System.Text.Json;
using Nethereum.Contracts;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.Util;
using System.Collections.Generic;
using System.Collections;

namespace Arbitrum.Utils
{
    public class LoadContractException : Exception
    {
        public LoadContractException(string message) : base(message)
        {
        }
    }

    public class CaseDict : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
        public string RetryTxHash
        {
            get { return Get<string>("retryTxHash"); }
            set { this["retryTxHash"] = value; }
        }

        public CaseDict(object data)
        {
            if (data is Dictionary<string, object> dict)
            {
                _data = new Dictionary<string, object>(dict, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                throw new ArgumentException("Input data must be a dictionary");
            }
        }

        public object this[string key]
        {
            get
            {
                if (_data.TryGetValue(key, out var value))
                {
                    return value;
                }
                else
                {
                    throw new KeyNotFoundException(key);
                }
            }
            set
            {
                _data[key] = value; 
            }
        }

        public T Get<T>(string key, T? defaultValue = default)
        {
            if (_data.TryGetValue(key, out var value))
            {
                return (T)value;
            }
            else
            {
                return defaultValue;
            }
        }

        // Implementing IEnumerable<KeyValuePair<string, object>>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        // Implementing IEnumerable
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Method to add key-value pairs
        public void Add(string key, object value)
        {
            _data.Add(key, value);
        }

        public bool ContainsKey(string key)
        {
            return _data.ContainsKey(key);
        }

        public Dictionary<string, object> ToDictionary()
        {
            return _data.ToDictionary(kvp => kvp.Key, kvp => ConvertValue(kvp.Key, kvp.Value), StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            var items = string.Join(", ", ToDictionary().Select(kvp => $"{kvp.Key}: {ConvertValue(kvp.Key, kvp.Value)}"));
            return $"CaseDict({items})";
        }

        private object ConvertValue(string key, object value)
        {
            if (value is Dictionary<string, object> dict)
            {
                return new CaseDict(dict);
            }
            else if (value is List<object> list)
            {
                return list.Select(item => ConvertValue(key, item)).ToList();
            }
            // Handle Contract class conversion if applicable in your context
            // else if (value is Contract contract)
            // {
            //     return contract.Address;
            // }
            else
            {
                return value;
            }
        }
    }


    public class LoadContractUtils
    {
        public class ContractData
        {
            public string[]? Abi { get; set; }
            public string? Bytecode { get; set; }
        }

        public static Contract LoadContract(string contractName, object provider, string? address = null, bool isClassic = false)
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
                    var contractAddress = GetAddress(address);

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

        public static Web3 GetWeb3Provider(object provider)
        {
            if (provider is SignerOrProvider signerOrProvider)
            {
                return signerOrProvider.Provider;
            }
            else if (provider is ArbitrumProvider arbitrumProvider)
            {
                return arbitrumProvider.Provider;
            }
            else
            {
                return (Web3)provider;
            }
        }

        public static string GetAddress(string address)
        {
            if (AddressUtil.Current.IsChecksumAddress(address))
            {
                return AddressUtil.Current.ConvertToChecksumAddress(address);
            }
            else
            {
                throw new ArgumentException($"Invalid Ethereum address: {address}");
            }
        }
    }
}
