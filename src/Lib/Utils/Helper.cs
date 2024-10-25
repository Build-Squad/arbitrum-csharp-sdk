using Arbitrum.DataEntities;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;
using System.Reflection;
using Contract = Nethereum.Contracts.Contract;

namespace Arbitrum.Utils
{
    public static class HelperMethods
    {
        private static Random random = new Random();

        public static string GenerateRandomHex(int length)
        {
            byte[] randomBytes = new byte[length];
            random.NextBytes(randomBytes);
            return "0x" + BitConverter.ToString(randomBytes).Replace("-", "").ToLower();
        }
        public static BigInteger HexDataLength(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return BigInteger.Zero;
            }

            string hexString = BitConverter.ToString(data).Replace("-", "").ToLower();
            if (!IsHexString(hexString) || (hexString.Length % 2 != 0))
            {
                return BigInteger.Zero;
            }

            return hexString.Length / 2;

            bool IsHexString(string str)
            {
                if (str.Length < 2)
                {
                    return false;
                }

                for (int i = 0; i < str.Length; i++)
                {
                    char c = str[i];
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    {
                        return false;
                    }
                }

                return true;
            }
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
    public class L2ToL1TransactionEvent : IEventDTO
    {
        public string? Caller { get; set; }
        public string? Destination { get; set; }
        public BigInteger? ArbBlockNum { get; set; }
        public BigInteger? EthBlockNum { get; set; }
        public BigInteger? Timestamp { get; set; }
        public BigInteger? CallValue { get; set; }
        public string? Data { get; set; }
        public BigInteger? UniqueId { get; set; }
        public BigInteger BatchNumber { get; set; }
        public BigInteger IndexInBatch { get; set; }
        public BigInteger? Hash { get; set; }
        public BigInteger? Position { get; set; }
        public string? TransactionHash { get; set; }

    }
    public class ClassicL2ToL1TransactionEvent : L2ToL1TransactionEvent
    {
    }
    public class NitroL2ToL1TransactionEvent : L2ToL1TransactionEvent
    {
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
            Contract contract;
            try
            {
                string contractAddress = string.Empty;
                var web3Provider = GetWeb3Provider(provider);

                var (abi, bytecode) = await LogParser.LoadAbi(contractName, isClassic);

                contractAddress = address;
                contract = web3Provider.Eth.GetContract(abi, contractAddress);                
            }
            catch (Exception ex) 
            {
                throw new Exception(ex.Message, ex.InnerException);
            }

            return contract;
        }

        public static async Task<Contract> DeployAbiContract(
            Web3 provider,
            SignerOrProvider deployer,
            string contractName,
            object[] constructorArgs = null,
            bool isClassic = false)
        {
            var deployerAddress = deployer.Account.Address;

            var (contractAbi, contractByteCode) = await LogParser.LoadAbi(contractName, isClassic);

            // Check if constructorArgs contains null values
            if (constructorArgs != null)
            {
                for (int i = 0; i < constructorArgs.Length; i++)
                {
                    if (constructorArgs[i] == null)
                    {
                        throw new ArgumentException($"Constructor argument at index {i} is null");
                    }
                }
            }

            var byteCode = string.Empty;
            if (!contractByteCode.StartsWith("0x")) byteCode = string.Concat("0x", contractByteCode);

            var gas = await provider.Eth.DeployContract.EstimateGasAsync(abi: contractAbi, contractByteCode: byteCode, from: deployerAddress, values: constructorArgs);

            var txn = await provider.Eth.DeployContract.SendRequestAsync(
                abi: contractAbi,
                contractByteCode: byteCode,
                from: deployerAddress,
                gas: gas,
                values: constructorArgs);

            var txReceipt = await provider.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txn);

            return provider.Eth.GetContract(contractAbi, txReceipt.ContractAddress);
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

        public static string GetContractAddress(string senderAddress, BigInteger nonce)
        {
            var encodedData = RLP.EncodeList(RLP.EncodeElement(senderAddress.Substring(2).HexToByteArray()), RLP.EncodeElement(nonce.ToByteArray()));
            var hashedData = new Sha3Keccack().CalculateHash(encodedData);
            var contractAddressBytes = new byte[20];
            Array.Copy(hashedData, hashedData.Length - 20, contractAddressBytes, 0, 20);
            return contractAddressBytes.ToHex();
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
