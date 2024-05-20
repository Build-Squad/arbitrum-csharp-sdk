using Arbitrum.DataEntities;
using NUnit.Framework;

namespace Arbitrum.Tests.Unit
{
    [TestFixture]
    public class AddressTests
    {
        [Test]
        public void Constructor_ValidAddress_Success()
        {
            // Arrange
            string validAddress = "0x0000000000000000000000000000000000000000";

            // Act
            var address = new Address(validAddress);

            // Assert
            Assert.That(address.Value, Is.EqualTo(validAddress));
        }

        [Test]
        public void Constructor_InvalidAddress_ThrowsArbSdkError()
        {
            // Arrange
            string invalidAddress = "0xInvalidAddress";

            // Act & Assert
            Assert.Throws<ArbSdkError>(() => new Address(invalidAddress));
        }

        [Test]
        public void ApplyAlias_ValidAddress_Success()
        {
            // Arrange
            string l1Address = "0x1234567890123456789012345678901234567890";
            string expectedL2Alias = "0x00ff456789012345678901234567890123456789"; // Example expected L2 alias

            var address = new Address(l1Address);

            // Act
            var l2Alias = address.ApplyAlias();

            // Assert
            Assert.That(l2Alias.Value, Is.EqualTo(expectedL2Alias));
        }

        [Test]
        public void UndoAlias_ValidAddress_Success()
        {
            // Arrange
            string l2Address = "0x00ff456789012345678901234567890123456789"; // Example L2 alias
            string expectedL1Address = "0x1234567890123456789012345678901234567890";

            var address = new Address(l2Address);

            // Act
            var l1Address = address.UndoAlias();

            // Assert
            Assert.That(l1Address.Value, Is.EqualTo(expectedL1Address));
        }

        [Test]
        public void Equals_SameAddress_ReturnsTrue()
        {
            // Arrange
            string addressValue = "0x1234567890123456789012345678901234567890";

            var address1 = new Address(addressValue);
            var address2 = new Address(addressValue);

            // Act & Assert
            Assert.That(address1.Equals(address2), Is.True);
        }

        [Test]
        public void Equals_DifferentAddresses_ReturnsFalse()
        {
            // Arrange
            var address1 = new Address("0x1234567890123456789012345678901234567890");
            var address2 = new Address("0x0987654321098765432109876543210987654321");

            // Act & Assert
            Assert.That(address1.Equals(address2), Is.False);
        }
    }
}
