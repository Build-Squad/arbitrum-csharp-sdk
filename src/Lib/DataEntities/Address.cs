using System;
using System.Numerics;
using Nethereum.Util;
using static Arbitrum.DataEntities.Constants;

namespace Arbitrum.DataEntities
{
    public class Address
    {
        private readonly BigInteger ADDRESS_ALIAS_OFFSET_BIG_INT = BigInteger.Parse(ADDRESS_ALIAS_OFFSET, System.Globalization.NumberStyles.HexNumber);
        private readonly int ADDRESS_BIT_LENGTH = 160;
        private readonly int ADDRESS_NIBBLE_LENGTH = 160 / 4; // 40 characters

        public string Value { get; }

        public Address(string value)
        {
            // Remove "0x" prefix if present
            value = value.StartsWith("0x") ? value.Substring(2) : value;

            if (!AddressUtil.Current.IsValidEthereumAddressHexFormat(value))
            {
                throw new ArbSdkError($"'{value}' is not a valid address");
            }

            Value = value;
        }

        public string Alias(string address, bool forward)
        {
            BigInteger addressBigInt = BigInteger.Parse("0x" + address, System.Globalization.NumberStyles.HexNumber);

            // Use BigInteger to allow for proper under/overflow behavior
            BigInteger resultBigInt = forward
                ? addressBigInt + ADDRESS_ALIAS_OFFSET_BIG_INT
                : addressBigInt - ADDRESS_ALIAS_OFFSET_BIG_INT;

            // Ensure positive modulus with ADDRESS_BIT_LENGTH
            resultBigInt = BigInteger.Remainder(resultBigInt, BigInteger.Pow(2, ADDRESS_BIT_LENGTH));

            // Convert the resulting BigInteger back to a hexadecimal string and pad to nibble length
            string resultHex = resultBigInt.ToString("X");
            resultHex = resultHex.PadLeft(ADDRESS_NIBBLE_LENGTH, '0');

            // Return the final alias as a hexadecimal string with '0x' prefix
            return "0x" + resultHex;
        }

        /**
         * Find the L2 alias of an L1 address.
         * @returns The L2 alias for the provided L1 address.
         */
        public Address ApplyAlias()
        {
            return new Address(Alias(Value, true));
        }

        /**
         * Find the L1 alias of an L2 address
         * @returns
         */
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
