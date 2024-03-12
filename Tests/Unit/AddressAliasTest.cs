using Nethereum.Hex.HexConvertors.Extensions;
using NUnit.Framework;
using System;
using System.Numerics;
using static Arbitrum.DataEntities.Constants;

namespace Arbitrum.DataEntities.Tests.Unit
{
    [TestFixture]
    public class AddressAliasTests
    {
        private const string AddressAliasOffset = "0x10";
        private static readonly BigInteger AddressAliasOffsetInt = BigInteger.Parse(AddressAliasOffset.RemoveHexPrefix(), System.Globalization.NumberStyles.HexNumber);
        private static readonly BigInteger MaxAddrInt = BigInteger.Pow(2, 160) - 1;

        private static void ApplyUndoTest(string addr, string expectedApply, string expectedUndo)
        {
            var address = new Address(addr);

            var afterApply = address.ApplyAlias();
            Assert.That(afterApply.Value, Is.EqualTo(expectedApply));

            var afterUndo = afterApply.UndoAlias();
            Assert.That(afterUndo.Value, Is.EqualTo(expectedUndo));

            var afterApplyUndo = afterUndo.ApplyAlias();
            Assert.That(afterApplyUndo.Value, Is.EqualTo(expectedApply));

            var afterUndoApply = afterApply.UndoAlias();
            Assert.That(afterUndoApply.Value, Is.EqualTo(expectedUndo));
        }

        [Test]
        public void TestAliasBelowOffset()
        {
            var belowOffsetInt = (MaxAddrInt - AddressAliasOffsetInt - 10) & MaxAddrInt;
            var belowOffsetHex = belowOffsetInt.ToString("X").PadLeft(40, '0');

            ApplyUndoTest(
                belowOffsetHex,
                "0xfffffffffffffffffffffffffffffffffffffff5",
                "0xddddffffffffffffffffffffffffffffffffddd3"
            );
        }

        [Test]
        public void TestAliasOnOffset()
        {
            var onOffsetInt = (MaxAddrInt - AddressAliasOffsetInt) & MaxAddrInt;
            var onOffsetHex = onOffsetInt.ToString("X").PadLeft(40, '0');

            ApplyUndoTest(
                onOffsetHex,
                "0xffffffffffffffffffffffffffffffffffffffff",
                "0xddddffffffffffffffffffffffffffffffffdddd"
            );
        }

        [Test]
        public void TestAliasAboveOffset()
        {
            var aboveOffsetInt = (MaxAddrInt - AddressAliasOffsetInt + 10) & MaxAddrInt;
            var aboveOffsetHex = aboveOffsetInt.ToString("X").PadLeft(40, '0');

            ApplyUndoTest(
                aboveOffsetHex,
                "0x0000000000000000000000000000000000000009",
                "0xddddffffffffffffffffffffffffffffffffdde7"
            );
        }

        [Test]
        public void TestAliasSpecialCase()
        {
            var special = "0xFfC98231ef2fd1F77106E10581A1faC14E29d014";
            ApplyUndoTest(
                special,
                "0x10da8231ef2fd1f77106e10581a1fac14e29e125",
                "0xeeb88231ef2fd1f77106e10581a1fac14e29bf03"
            );
        }
    }
}
