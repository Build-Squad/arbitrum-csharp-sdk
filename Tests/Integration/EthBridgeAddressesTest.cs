using NUnit.Framework;
using System.Threading.Tasks;
using NUnit.Framework.Constraints;
using Nethereum.Web3;

namespace YourNamespace.Tests
{
    [TestFixture]
    public class EthBridgeTests
    {
        [Test]
        public async Task TestTransfersEtherOnL2()
        {
            var setupState = await TestHelpers.TestSetup();
            var l2Signer = setupState.L2Signer;

            TestHelpers.FundL2(l2Signer);

            var randomAddress = Account.Create().Address;
            var amountToSend = Web3.Convert.ToWei(0.000005, "ether");

            var balanceBefore = l2Signer.Provider.Eth.GetBalance(l2Signer.Account.Address);

            var txHash = l2Signer.Provider.Eth.SendTransaction(new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = l2Signer.Account.Address,
                To = randomAddress,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(amountToSend),
                MaxFeePerGas = new Nethereum.Hex.HexTypes.HexBigInteger(15000000000),
                MaxPriorityFeePerGas = new Nethereum.Hex.HexTypes.HexBigInteger(0)
            });

            var txReceipt = await l2Signer.Provider.Eth.TransactionManager.TransactionReceiptService.WaitForTransactionReceiptAsync(txHash);

            var balanceAfter = l2Signer.Provider.Eth.GetBalance(l2Signer.Account.Address);
            var randomBalanceAfter = l2Signer.Provider.Eth.GetBalance(randomAddress);

            Assert.That(Web3.Convert.FromWei(randomBalanceAfter, "ether"), Is.EqualTo(Web3.Convert.FromWei(amountToSend, "ether"))
                .Using(DecimalComparer.Equal), "Random address balance after should match the sent amount");

            var expectedBalanceAfter = balanceBefore - txReceipt.GasUsed.Value * txReceipt.EffectiveGasPrice.Value - amountToSend;

            Assert.That(balanceAfter, Is.EqualTo(expectedBalanceAfter).Using(DecimalComparer.Equal), "L2 signer balance after should be correctly reduced");
        }

        // Implement other test methods similarly
    }
}
