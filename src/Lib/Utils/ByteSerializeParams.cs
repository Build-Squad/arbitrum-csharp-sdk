using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.Utils;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using Nethereum.Web3;

// Define the PrimativeType type
namespace Arbitrum.Utils
{
    public delegate Task<int> AddressToIndexDelegate(string address);
    public class PrimativeType
    {
        // Define the Type enum to represent the type of data held by PrimativeType
        public enum TypeEnum
        {
            String,
            Int,
            Bool,
            BigInteger
        }

        // Property to hold the data type
        public TypeEnum DataType { get; set; }

        // Union type to represent different types of data
        public object Value { get; set; }

        // Constructors for different data types
        public PrimativeType(string value)
        {
            DataType = TypeEnum.String;
            Value = value;
        }

        public PrimativeType(int value)
        {
            DataType = TypeEnum.Int;
            Value = value;
        }

        public PrimativeType(bool value)
        {
            DataType = TypeEnum.Bool;
            Value = value;
        }

        public PrimativeType(BigInteger value)
        {
            DataType = TypeEnum.BigInteger;
            Value = value;
        }
    }
    // Define the PrimativeArray class to hold a list of PrimativeType objects
public class PrimativeArray
{
    // Property to hold a list of PrimativeType objects
    public List<PrimativeType> Values { get; set; }

    // Constructor to initialize the property
    public PrimativeArray(List<PrimativeType> values)
    {
        Values = values;
    }
}

    // Define the BytesNumber type as an enum
    public enum BytesNumber
    {
        One = 1,
        Four = 4,
        Eight = 8,
        Sixteen = 16,
        ThirtyTwo = 32
    }

    public static class AddressSerializer
    {
        private static readonly Dictionary<string, int> AddressToIndexMemo = new Dictionary<string, int>();

        public static async Task<int> GetAddressIndex(string address, Web3 provider)
        {
            if (AddressToIndexMemo.ContainsKey(address))
                return AddressToIndexMemo[address];

            var arbAddressTable = await LoadContractUtils.LoadContract(
                                                            provider: provider,
                                                            contractName: "ArbAddressTable",
                                                            address: Constants.ARB_ADDRESS_TABLE_ADDRESS,
                                                            isClassic: false
                                                            );

            var isRegistered = await arbAddressTable.GetFunction("addressExists").CallAsync<bool>(address);

            if (isRegistered)
            {
                var index = (int)await arbAddressTable.GetFunction("lookup").CallAsync<BigInteger>(address);
                AddressToIndexMemo[address] = index;
                return index;
            }
            else
            {
                return -1;
            }
        }

        public static async Task<byte[]> SerializeParams(
            dynamic parameters,
            AddressToIndexDelegate? addressToIndex = null
        )
        {
            // Initialize the list for formatted parameters
            List<byte> formattedParams = new List<byte>();

            // Iterate through the parameters
            foreach (var parameter in parameters.Values)
            {
                // Check if the parameter is a list
                if (parameter is PrimativeArray primArray)
                {
                    // Add the length of the array as a uint
                    formattedParams.AddRange(L1ToL2MessageUtils.HexToBytes(ToUint(primArray.Values.Count, 1)));
                    // Check if the array contains address types
                    if (IsAddress(primArray.Values[0].Value))
                    {
                        List<int> indices = new List<int>();
                        foreach (var item in primArray.Values)
                        {
                            if (addressToIndex != null)
                            {
                                indices.Add(await addressToIndex(item.Value.ToString()!));
                            }
                            else
                            {
                                indices.Add(-1);
                            }
                        }
                        // If all indices are valid, add them as uints
                        if (indices.All(index => index > -1))
                        {
                            formattedParams.AddRange(L1ToL2MessageUtils.HexToBytes(ToUint(1, 1)));
                            foreach (int index in indices)
                            {
                                formattedParams.AddRange(L1ToL2MessageUtils.HexToBytes(ToUint(index, 4)));
                            }
                        }
                        else
                        {
                            // Otherwise, add a 0 and the primitive values
                            formattedParams.AddRange(L1ToL2MessageUtils.HexToBytes(ToUint(0, 1)));
                            foreach (var item in primArray.Values)
                            {
                                formattedParams.AddRange(FormatPrimitive(item.Value.ToString()!));
                            }
                        }
                    }
                    else
                    {
                        // Add the primitive values
                        foreach (var item in primArray.Values)
                        {
                            formattedParams.AddRange(FormatPrimitive(item));
                        }
                    }
                }
                else
                {
                    // Check if the parameter is an address type
                    if (IsAddress(parameter.StringValue))
                    {
                        int index = await addressToIndex!.Invoke(parameter.StringValue);

                        if (index > -1)
                        {
                            // If index is valid, add 1 and the index
                            formattedParams.AddRange(L1ToL2MessageUtils.HexToBytes(ToUint(1, 1)));
                            formattedParams.AddRange(L1ToL2MessageUtils.HexToBytes(ToUint(index, 4)));
                        }
                        else
                        {
                            // Otherwise, add 0 and the primitive value
                            formattedParams.AddRange(L1ToL2MessageUtils.HexToBytes(ToUint(0, 1)));
                            formattedParams.AddRange(FormatPrimitive(parameter.StringValue));
                        }
                    }
                    else
                    {
                        // Add the primitive value directly
                        formattedParams.AddRange(FormatPrimitive(parameter));
                    }
                }
            }

            // Convert the list of formatted parameters to a byte array and return it
            return formattedParams.ToArray();
        }

        public static Func<PrimativeArray, Task<byte[]>> ArgSerializerConstructor(Web3 provider)
        {
            async Task<byte[]> SerializeParamsWithIndex(PrimativeArray parameters)
            {
                async Task<int> AddressToIndex(string address) => await GetAddressIndex(address, provider);

                return await SerializeParams(parameters, AddressToIndex);
            }

            return SerializeParamsWithIndex;
        }

        public static bool IsAddress(dynamic input)
        {
            return input is string && Web3.IsChecksumAddress(input);
        }

        public static string HexZeroPad(dynamic value, int length)
        {
            string hexValue = value is string ? value : ((byte[])value).ToHex();

            if (!hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hexValue = "0x" + hexValue;
            }

            if (hexValue.Length > 2 * length + 2)
            {
                throw new ArgumentException("Value out of range", nameof(value));
            }

            while (hexValue.Length < 2 * length + 2)
            {
                hexValue = "0x0" + hexValue.Substring(2);
            }

            return hexValue;
        }
        public static string ToUint(dynamic val, int bytes)
        {
            return HexZeroPad(BigInteger.Parse(val), bytes);
        }
        private static BigInteger ConvertToBigInteger(dynamic val)
        {
            if (val is BigInteger bigInt)
            {
                return bigInt;
            }
            else if (val is string stringValue)
            {
                if (BigInteger.TryParse(stringValue, out BigInteger result))
                {
                    return result;
                }
                else
                {
                    throw new ArgumentException("Invalid string format for BigInteger conversion", nameof(val));
                }
            }
            else
            {
                return Convert.ToInt64(val);
            }
        }


        private static byte[] FormatPrimitive(dynamic value)
        {
            if (Web3.IsChecksumAddress(value))
                return AddressUtil.Current.ConvertToChecksumAddress(value).HexToByteArray();

            if (value is bool boolValue)
                return BitConverter.GetBytes(boolValue ? 1 : 0);

            if (value is int intValue)
                return BitConverter.GetBytes(intValue);

            if (value is string stringValue)
                return stringValue.HexToByteArray();

            if (value is BigInteger bigIntegerValue)
                bigIntegerValue.ToByteArray();

            // Handle other primitive types here

            throw new ArgumentException("Unsupported type", nameof(value));
        }
    }
}
