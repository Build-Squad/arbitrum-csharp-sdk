using Arbitrum.DataEntities;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Arbitrum.Tests.Unit
{
    [TestFixture]
    public class NetworksTests
    {
        private const int EthereumMainnetChainId = 1;
        private const int ArbitrumOneChainId = 42161;
        private const int MockL1ChainId = 111111;
        private const int MockL2ChainId = 222222;
        private const int MockL3ChainId = 99999999;

        [SetUp]
        public void Setup()
        {
            NetworkUtils.AddDefaultLocalNetwork();
        }

        //Positive Test Cases
        [Test]
        public async Task GetL1Network_WithValidChainId_ReturnsL1Network()
        {
            // Arrange
            int validChainId = 1;

            // Act
            var result = await NetworkUtils.GetL1Network(validChainId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("Mainnet"));
            Assert.That(result.ExplorerUrl, Is.EqualTo("https://etherscan.io"));
            Assert.That(result.BlockTime, Is.EqualTo(14));
        }

        [Test]
        public async Task GetL2Network_WithValidChainId_ReturnsL2Network()
        {
            // Arrange
            int validChainId = 42161;

            // Act
            var result = await NetworkUtils.GetL2Network(validChainId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("Arbitrum One"));
            Assert.That(result.ExplorerUrl, Is.EqualTo("https://arbiscan.io"));
            Assert.That(result.ConfirmPeriodBlocks, Is.EqualTo(45818));
        }

        [Test]
        public void AddCustomNetwork_WithValidCustomNetwork_AddsNetworkSuccessfully()
        {
            // Arrange
            var customL1Network = new L1Network
            {
                ChainID = 123456,
                Name = "CustomL1",
                ExplorerUrl = "https://customl1explorer.io",
                BlockTime = 15,
                IsCustom = true,
                PartnerChainIDs = new[] { 54321 },
                IsArbitrum = false
            };

            var customL2Network = new L2Network
            {
                ChainID = 654321,
                Name = "CustomL2",
                ExplorerUrl = "https://customl2explorer.io",
                ConfirmPeriodBlocks = 50000,
                IsCustom = true,
                IsArbitrum = true,
                PartnerChainID = 12345,
                PartnerChainIDs = Array.Empty<int>(),
                RetryableLifetimeSeconds = 86400,
                NitroGenesisBlock = 123456,
                NitroGenesisL1Block = 789012,
                DepositTimeout = 3600000,
                BlockTime = 10
            };

            // Act
            NetworkUtils.AddCustomNetwork(customL1Network, customL2Network);

            // Assert
            Assert.That(NetworkUtils.l1Networks.ContainsKey(customL1Network.ChainID), Is.True);
            Assert.That(NetworkUtils.l2Networks.ContainsKey(customL2Network.ChainID), Is.True);
        }

        [Test]
        public void AddDefaultLocalNetwork_ReturnsExpectedNetworks()
        {
            // Act
            var (l1Network, l2Network) = NetworkUtils.AddDefaultLocalNetwork();

            // Assert L1 network properties
            Assert.That(l1Network.BlockTime, Is.EqualTo(10));
            Assert.That(l1Network.ChainID, Is.EqualTo(1337));
            Assert.That(l1Network.ExplorerUrl, Is.Empty);
            Assert.That(l1Network.IsCustom, Is.True);
            Assert.That(l1Network.Name, Is.EqualTo("EthLocal"));
            Assert.That(l1Network.PartnerChainIDs, Is.EquivalentTo(new[] { 412346 }));
            Assert.That(l1Network.IsArbitrum, Is.False);

            // Assert L2 network properties
            Assert.That(l2Network.ChainID, Is.EqualTo(412346));
            Assert.That(l2Network.ConfirmPeriodBlocks, Is.EqualTo(20));
            Assert.That(l2Network.ExplorerUrl, Is.Empty);
            Assert.That(l2Network.IsArbitrum, Is.True);
            Assert.That(l2Network.IsCustom, Is.True);
            Assert.That(l2Network.Name, Is.EqualTo("ArbLocal"));
            Assert.That(l2Network.PartnerChainID, Is.EqualTo(1337));
            Assert.That(l2Network.PartnerChainIDs, Is.Empty);
            Assert.That(l2Network.RetryableLifetimeSeconds, Is.EqualTo(604800));
            Assert.That(l2Network.NitroGenesisBlock, Is.EqualTo(0));
            Assert.That(l2Network.NitroGenesisL1Block, Is.EqualTo(0));
            Assert.That(l2Network.DepositTimeout, Is.EqualTo(900000));
            Assert.That(l2Network.TokenBridge, Is.Not.Null);
            Assert.That(l2Network.EthBridge, Is.Not.Null);
            Assert.That(l2Network.BlockTime, Is.EqualTo(Constants.ARB_MINIMUM_BLOCK_TIME_IN_SECONDS));
        }

        [Test]
        public void IsL1Network_WithL1Network_ReturnsTrue()
        {
            // Arrange
            var l1Network = new L1Network
            {
                ChainID = 123,
                Name = "L1Network",
                ExplorerUrl = "https://l1explorer.io",
                BlockTime = 10,
                IsCustom = false,
                PartnerChainIDs = new[] { 456 },
                IsArbitrum = false
            };

            // Act
            bool result = NetworkUtils.IsL1Network(l1Network);

            // Assert
            Assert.That(result, Is.True);
        }

        //Negative Test Cases
        [Test]
        public void GetL1Network_WithInvalidChainId_ThrowsArbSdkError()
        {
            // Arrange
            int invalidChainId = 9999;

            // Act & Assert
            Assert.That(() => NetworkUtils.GetL1Network(invalidChainId),
                Throws.TypeOf<ArbSdkError>().With.Message.EqualTo($"Unrecognized network {invalidChainId}."));
        }

        [Test]
        public void GetL2Network_WithInvalidChainId_ThrowsArbSdkError()
        {
            // Arrange
            int invalidChainId = 9999;

            // Act & Assert
            Assert.That(() => NetworkUtils.GetL2Network(invalidChainId),
                Throws.TypeOf<ArbSdkError>().With.Message.EqualTo($"Unrecognized network {invalidChainId}."));
        }

        [Test]
        public void AddCustomNetwork_WithNullCustomNetwork_ThrowsArgumentNullException()
        {
            // Arrange
            L1Network? customL1Network = null;
            L2Network? customL2Network = null;

            // Act & Assert
            Assert.That(() => NetworkUtils.AddCustomNetwork(customL1Network, customL2Network), Throws.ArgumentNullException);
        }


        [Test]
        public void AddCustomNetwork_WithInvalidCustomNetwork_ThrowsArbSdkError()
        {
            // Arrange
            L1Network invalidCustomL1Network = null; // Missing required properties
            var invalidCustomL2Network = new L2Network
            {
                // Missing required properties
            };

            // Act & Assert
            Assert.That(() => NetworkUtils.AddCustomNetwork(invalidCustomL1Network, invalidCustomL2Network),
                        Throws.TypeOf<ArbSdkError>()
                              .With.Message.EqualTo($"Custom network {invalidCustomL2Network.ChainID} must have isCustom flag set to true"));
        }

        [Test]
        public void AddCustomNetwork_WithAlreadyIncludedL1Network_ThrowsArbSdkError()
        {
            // Arrange
            var customL1Network = new L1Network
            {
                ChainID = 12345,
                Name = "ExistingL1",
                ExplorerUrl = "https://existingl1explorer.io",
                BlockTime = 15,
                IsCustom = true,
                PartnerChainIDs = new[] { 54321 },
                IsArbitrum = false
            };
            var customL2Network = new L2Network
            {
                ChainID = 54321,
                Name = "CustomL2",
                ExplorerUrl = "https://customl2explorer.io",
                ConfirmPeriodBlocks = 50000,
                IsCustom = true,
                IsArbitrum = true,
                PartnerChainID = 12345,
                PartnerChainIDs = Array.Empty<int>(),
                RetryableLifetimeSeconds = 86400,
                NitroGenesisBlock = 123456,
                NitroGenesisL1Block = 789012,
                DepositTimeout = 3600000,
                BlockTime = 10
            };

            NetworkUtils.AddCustomNetwork(customL1Network, customL2Network);

            // Attempting to add the same L1 network again
            var duplicateL1Network = new L1Network
            {
                ChainID = 12345,
                Name = "DuplicateL1",
                ExplorerUrl = "https://duplicatel1explorer.io",
                BlockTime = 20,
                IsCustom = true,
                PartnerChainIDs = new[] { 98765 },
                IsArbitrum = false
            };

            // Act & Assert
            Assert.That(() => NetworkUtils.AddCustomNetwork(duplicateL1Network, new L2Network()),
                        Throws.TypeOf<ArbSdkError>()
                              .With.Message.EqualTo($"Network {customL1Network.ChainID} already included"));
        }

        [Test]
        public void AddDefaultLocalNetwork_WithExistingNetworks_OverwritesExistingNetworks()
        {
            // Arrange: Prepare existing networks

            // Act
            var (l1Network, l2Network) = NetworkUtils.AddDefaultLocalNetwork();

            // Act: Adding default networks again
            var (l1NetworkAgain, l2NetworkAgain) = NetworkUtils.AddDefaultLocalNetwork();

            // Assert: Ensure the networks are overwritten
            Assert.That(l1Network, Is.Not.EqualTo(l1NetworkAgain));
            Assert.That(l2Network, Is.Not.EqualTo(l2NetworkAgain));
        }

        [Test]
        public void IsL1Network_WithL2Network_ReturnsFalse()
        {
            // Arrange
            var l2Network = new L2Network
            {
                ChainID = 123,
                Name = "L2Network",
                // Other properties
            };

            // Act
            bool result = NetworkUtils.IsL1Network(l2Network);

            // Assert
            Assert.That(result, Is.False);
        }
    }
}
