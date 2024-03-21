using System;
using System.Globalization;
using System.Numerics;
using Nethereum.Util;
using Nethereum.Web3;

namespace Arbitrum.DataEntities
{
    public class Address
    {
        private readonly BigInteger ADDRESS_ALIAS_OFFSET_BIG_INT = BigInteger.Parse(Constants.ADDRESS_ALIAS_OFFSET, NumberStyles.HexNumber);
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
            BigInteger addressInt = BigInteger.Parse("0x" + address, System.Globalization.NumberStyles.HexNumber);
            BigInteger offset = forward ? addressInt + ADDRESS_ALIAS_OFFSET_BIG_INT : addressInt - ADDRESS_ALIAS_OFFSET_BIG_INT;
            string aliasedAddress = (offset & ((BigInteger.One << ADDRESS_BIT_LENGTH) - BigInteger.One)).ToString("X");
            aliasedAddress = aliasedAddress.PadLeft(ADDRESS_NIBBLE_LENGTH, '0');
            return "0x" + aliasedAddress;
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
