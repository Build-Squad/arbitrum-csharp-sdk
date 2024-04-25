using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Nethereum.Contracts;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.Util;
using System.Collections.Generic;
using System.Collections;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.RPC.Eth.DTOs;
using System.Reflection.Metadata.Ecma335;
using Nethereum.ABI.Model;
using System.Reflection;

namespace Arbitrum.Utils
{
    public static class HelperMethods
    {
        //generic method to get bytecode from an abi
        public static string GetBytecodeFromABI(string filePath)
        {
            try
            {
                // Read the JSON file as a string
                string jsonString = File.ReadAllText(filePath);

                // Deserialize the JSON string to a dynamic object
                var jsonData = JsonSerializer.Deserialize<dynamic>(jsonString);

                // Check if the jsonData contains an ABI object and bytecode
                if (jsonData is not null && jsonData.ContainsKey("abi"))
                {
                    // Extract the ABI array
                    var abiArray = jsonData["abi"];

                    // Iterate through the ABI array to find the bytecode
                    foreach (var item in abiArray)
                    {
                        if (item is not null && item.ContainsKey("bytecode"))
                        {
                            // Return the bytecode
                            return item["bytecode"];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            // Return null if bytecode is not found
            return null;
        }

        //Generic method to copy properties of one type into other
        public static TDestination CopyMatchingProperties<TDestination, TSource>(TSource source)
        where TDestination : new()
        {
            // Create a new instance of the destination type
            TDestination destination = new TDestination();

            // Get the properties of the source and destination types
            PropertyInfo[] sourceProperties = typeof(TSource).GetProperties();
            PropertyInfo[] destinationProperties = typeof(TDestination).GetProperties();

            // Create a dictionary to map property names to PropertyInfo objects for quick lookup
            var destinationPropertyDictionary = new Dictionary<string, PropertyInfo>();
            foreach (var destProp in destinationProperties)
            {
                destinationPropertyDictionary[destProp.Name] = destProp;
            }

            // Iterate through each property in the source object
            foreach (var sourceProp in sourceProperties)
            {
                // Check if the property exists in the destination object
                if (destinationPropertyDictionary.TryGetValue(sourceProp.Name, out PropertyInfo? matchingDestProp))
                {
                    // Check if the property types match and the destination property is writable
                    if (matchingDestProp.PropertyType == sourceProp.PropertyType && matchingDestProp.CanWrite)
                    {
                        // Copy the value from the source to the destination
                        object value = sourceProp.GetValue(source)!;
                        matchingDestProp.SetValue(destination, value);
                    }
                }
            }

            // Return the new instance of the destination type with copied properties
            return destination;
        }
    }
    public class LoadContractException : Exception
    {
        public LoadContractException(string message) : base(message)
        {
        }
    }
    public class L2ToL1TransactionEvent
    {
        public string? Caller { get; set; }
        public string? Destination { get; set; }
        public BigInteger ArbBlockNum { get; set; }
        public BigInteger EthBlockNum { get; set; }
        public BigInteger Timestamp { get; set; }
        public BigInteger CallValue { get; set; }
        public string? Data { get; set; }
        public BigInteger UniqueId { get; set; }
        public BigInteger BatchNumber { get; set; }
        public BigInteger IndexInBatch { get; set; }
        public BigInteger Hash { get; set; }
        public BigInteger Position { get; set; }
        public string? TransactionHash { get; set; }

    }
    public class ClassicL2ToL1TransactionEvent : L2ToL1TransactionEvent
    {
        public new BigInteger UniqueId { get; set; }
        public new BigInteger BatchNumber { get; set; }
        public new BigInteger IndexInBatch { get; set; }
    }
    public class NitroL2ToL1TransactionEvent : L2ToL1TransactionEvent
    {
        public new BigInteger Hash { get; set; }
        public new BigInteger Position { get; set; }
    }
    public class BlockTag
    {
        public string? StringValue { get; set; }
        public int? NumberValue { get; set; }

        public BlockTag(string stringValue)
        {
            StringValue = stringValue;
            NumberValue = null;
        }

        public BlockTag(int numberValue)
        {
            StringValue = null;
            NumberValue = numberValue;
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
                return defaultValue!;
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
        public static dynamic FormatContractOutput(Contract contract, string functionName, object output)
        {
            // Get the FunctionABI object for the specified function name
            var funcAbi = contract.ContractBuilder.GetFunctionAbi(functionName);

            // Check if the FunctionABI object is null
            if (funcAbi == null)
            {
                // Throw an exception if the function ABI is not found
                throw new ArgumentException($"Function {functionName} not found in contract ABI");
            }

            return FormatOutput(funcAbi.OutputParameters, output);
        }

        private static object FormatOutput(Parameter[] outputParameters, object output)
        {
            if (outputParameters == null || !outputParameters.Any())
            {
                return output;
            }

            var formattedOutput = new Dictionary<string, object>();

            for (int i = 0; i < outputParameters.Length; i++)
            {
                var parameter = outputParameters[i];
                var parameterName = parameter.Name ?? $"output_{i}";

                if (parameter.Type.StartsWith("tuple"))
                {
                    if (output is object[] outputArray && outputArray.Length > i)
                    {
                        formattedOutput[parameterName] = FormatOutput(outputParameters, outputArray[i]);
                    }
                    else
                    {
                        formattedOutput[parameterName] = FormatOutput(outputParameters, output);
                    }
                }
                else
                {
                    if (output is object[] outputArray && outputArray.Length > i)
                    {
                        formattedOutput[parameterName] = outputArray[i];
                    }
                    else
                    {
                        formattedOutput[parameterName] = output;
                    }
                }
            }

            return formattedOutput;
        }
        public static async Task<bool> IsContractDeployed(Web3 web3, string address)
        {

            var bytecode = await web3.Eth.GetCode.SendRequestAsync(Web3.ToChecksumAddress(address));
            return bytecode != "0x" && bytecode.Length > 2;
        }
        public class ContractData
        {
            public string[]? Abi { get; set; }
            public string? Bytecode { get; set; }
        }

        public static async Task<Contract> LoadContract(string contractName, object provider, string? address = null, bool isClassic = false)
        {
            var web3Provider = GetWeb3Provider(provider);

            var (abi, contractAddress) =await LogParser.LoadAbi(contractName, isClassic);

            var contract = web3Provider.Eth.GetContract(abi, contractAddress);

            return contract;
        }

        public static Web3 GetWeb3Provider(object provider)
        {
            if (provider is SignerOrProvider signerOrProvider)
            {
                return signerOrProvider.Provider;
            }
            else if (provider is ArbitrumProvider arbitrumProvider)
            {
                return arbitrumProvider;
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
