using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.Web3.Accounts;
using Nethereum.Web3;

namespace Arbitrum.Lib.Utils
{
    public class SignerOrProvider
    {
        public Account Account { get; }
        public IWeb3 Provider { get; }

        public SignerOrProvider(Account account, IWeb3 provider)
        {
            Account = account;
            Provider = provider;
        }
    }

    public static class SignerProviderUtils
    {
        public static bool IsSigner(object signerOrProvider)
        {
            if (signerOrProvider is Account || signerOrProvider is SignerOrProvider || signerOrProvider is Web3)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static IWeb3 GetProvider(object signerOrProvider)
        {
            if (signerOrProvider is Web3)
            {
                return signerOrProvider as Web3;
            }
            else if (signerOrProvider is SignerOrProvider)
            {
                return (signerOrProvider as SignerOrProvider).Provider;
            }
            else
            {
                return null;
            }
        }

        public static object GetSigner(object signerOrProvider)
        {
            if (signerOrProvider is Account)
            {
                return signerOrProvider as Account;
            }
            else if (signerOrProvider is SignerOrProvider)
            {
                return (signerOrProvider as SignerOrProvider).Account;
            }
            else
            {
                return null;
            }
        }

        public static IWeb3 GetProviderOrThrow(object signerOrProvider)
        {
            var provider = GetProvider(signerOrProvider);
            if (provider != null)
            {
                return provider;
            }
            else
            {
                throw new MissingProviderArbSdkError((string)signerOrProvider);
            }
        }

        public static bool SignerHasProvider(object signer)
        {
            return signer is SignerOrProvider;
        }

        public static async Task<bool> CheckNetworkMatches(object signerOrProvider, int chainId)
        {
            IWeb3 provider;

            if (signerOrProvider is SignerOrProvider)
            {
                provider = (signerOrProvider as SignerOrProvider).Provider;
            }
            else if (signerOrProvider is Web3)
            {
                provider = signerOrProvider as Web3;
            }
            else
            {
                provider = null;
            }

            if (provider == null)
            {
                throw new MissingProviderArbSdkError((string)signerOrProvider);
            }

            int providerChainId = (int)(await provider.Eth.ChainId.SendRequestAsync()).Value;
            if (providerChainId != chainId)
            {
                throw new ArbSdkError($"Signer/provider chain id: {providerChainId} does not match provided chain id: {chainId}.");
            }
            else
            {
                return true;
            }
        }
    }
}
