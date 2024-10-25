using System;
using System.Globalization;
using System.Numerics;
using Nethereum.Util;
using Nethereum.Web3;

namespace Arbitrum.DataEntities
{
    public class Address
    {
        private readonly BigInteger ADDRESS_ALIAS_OFFSET_BIG_INT = BigInteger.Parse(Constants.ADDRESS_ALIAS_OFFSET.Substring(2), NumberStyles.HexNumber);
        private readonly int ADDRESS_BIT_LENGTH = 160;
        private readonly int ADDRESS_NIBBLE_LENGTH = 160 / 4; // 40 characters

        public string Value { get; }

        public Address(string value)
        {
            if (!AddressExtensions.IsValidEthereumAddressHexFormat(value))
            {
                throw new ArbSdkError($"'{value}' is not a valid address");
            }
            Value = value;
        }

        public string Alias(string address, bool forward)
        {
            // Convert the hex address to a BigInteger
            BigInteger addressInt = BigInteger.Parse(address.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber);

            // Calculate the aliased address by adding or subtracting the offset
            BigInteger offset = forward ? addressInt + ADDRESS_ALIAS_OFFSET_BIG_INT : addressInt - ADDRESS_ALIAS_OFFSET_BIG_INT;

            // Convert the offset to a hexadecimal string and mask it to the correct bit length
            string aliasedAddress = (offset & ((BigInteger.One << ADDRESS_BIT_LENGTH) - 1)).ToString("X");

            // Ensure the address is padded to the correct nibble length
            string paddedAddress = aliasedAddress.PadLeft(ADDRESS_NIBBLE_LENGTH, '0');

            // Return the checksummed Ethereum address
            return Web3.ToChecksumAddress("0x" + paddedAddress);

        }

        public Address ApplyAlias()
        {
            return new Address(Alias(Value, true));
        }

        public Address UndoAlias()
        {
            return new Address(Alias(Value, false));
        }

        public bool Equals(Address other)
        {
            return Value.ToLower() == other.Value.ToLower();
        }
    }
}
