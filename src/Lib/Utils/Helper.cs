using Arbitrum.DataEntities;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3;
using System.Reflection;

namespace Arbitrum.Utils
{
    public static class HelperMethods
    {
        private static Random random = new ();

        public static string GenerateRandomHex(int length)
        {
            byte[] randomBytes = new byte[length];
            random.NextBytes(randomBytes);
            return "0x" + BitConverter.ToString(randomBytes).Replace("-", "").ToLower();
        }

        public static TDestination CopyMatchingProperties<TDestination, TSource>(TSource source)
        where TDestination : new()
        {
            var destination = new TDestination();

            PropertyInfo[] sourceProperties = typeof(TSource).GetProperties();
            PropertyInfo[] destinationProperties = typeof(TDestination).GetProperties();

            var destinationPropertyDictionary = new Dictionary<string, PropertyInfo>();
            foreach (var destProp in destinationProperties)
            {
                destinationPropertyDictionary[destProp.Name] = destProp;
            }

            foreach (var sourceProp in sourceProperties)
            {
                if (destinationPropertyDictionary.TryGetValue(sourceProp.Name, out PropertyInfo? matchingDestProp))
                {
                    if (matchingDestProp.PropertyType == sourceProp.PropertyType && matchingDestProp.CanWrite)
                    {
                        object value = sourceProp.GetValue(source)!;
                        matchingDestProp.SetValue(destination, value);
                    }
                }
            }

            return destination;
        }
    }

    public class LoadContractException : Exception
    {
        public LoadContractException(string message) : base(message)
        {
        }
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
            var funcAbi = contract.ContractBuilder.GetFunctionAbi(functionName) 
                ?? throw new ArgumentException($"Function {functionName} not found in contract ABI");

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

        // For deploying generic contracts
        public static async Task<Contract> DeployAbiContract<T>(
            Web3 provider,
            string contractName,
            T constructorArgs = null,
            bool isClassic = false) where T : ContractDeploymentMessage, new()
        {
            var (contractAbi, contractByteCode) = await LogParser.LoadAbi(contractName, isClassic);

            var deploymentMessage = constructorArgs ?? new T();
            deploymentMessage.ByteCode = contractByteCode;

            var transactionReceiptDeployment = await provider.Eth.GetContractDeploymentHandler<T>()
                .SendRequestAndWaitForReceiptAsync(deploymentMessage);

            var contractAddress = transactionReceiptDeployment.ContractAddress;

            var contract = provider.Eth.GetContract(contractAbi, contractAddress);
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

            var gas = await provider.Eth.DeployContract.EstimateGasAsync(abi: contractAbi, contractByteCode: contractByteCode, from: deployerAddress, values: constructorArgs);

            var txn = await provider.Eth.DeployContract.SendRequestAsync(
                abi: contractAbi,
                contractByteCode: contractByteCode,
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
