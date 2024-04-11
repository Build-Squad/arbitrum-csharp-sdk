using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Arbitrum.Utils;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using Nethereum.Web3;

// Define the PrimativeType type
public class PrimativeType
{
    // Define properties to represent string, number, boolean, and BigNumber types
    public string StringValue { get; set; }
    public int IntValue { get; set; }
    public bool BoolValue { get; set; }
    public BigInteger BigNumberValue { get; set; }

    // Add constructors to initialize the properties
    public PrimativeType(string value)
    {
        StringValue = value;
    }

    public PrimativeType(int value)
    {
        IntValue = value;
    }

    public PrimativeType(bool value)
    {
        BoolValue = value;
    }

    public PrimativeType(BigInteger value)
    {
        BigNumberValue = value;
    }
}

// Define the PrimativeOrPrimativeArray type
public class PrimativeOrPrimativeArray
{
    // Property to represent either a single PrimativeType or an array of PrimativeType
    public List<PrimativeType> Values { get; set; }

    // Constructor to initialize the property
    public PrimativeOrPrimativeArray(List<PrimativeType> values)
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

    public static Func<IEnumerable<PrimativeOrPrimativeArray>, Task<byte[]>> ArgSerializerConstructor(Web3 provider)
    {
        async Task<byte[]> SerializeParamsWithIndex(IEnumerable<PrimativeOrPrimativeArray> parameters)
        {
            async Task<int> AddressToIndex(string address) => await GetAddressIndex(address, provider);

            return await SerializeParams(parameters, AddressToIndex);
        }

        return SerializeParamsWithIndex;
    }

    public static bool IsAddress(dynamic input)
    {
        return input is string && Web3.IsChecksumAddress(input);            //////////////
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
