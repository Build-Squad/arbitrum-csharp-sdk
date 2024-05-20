using System.Threading.Tasks;
using Arbitrum.DataEntities;
using Nethereum.Web3.Accounts;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;

namespace Arbitrum.DataEntities
{
    public class SignerOrProvider
    {
        public Account? Account { get; }
        public Web3? Provider { get; }

        public SignerOrProvider(Web3 provider)
        {
            if (provider == null)
            {
                throw new ArgumentException("Provider is not provided");
            }
            Provider = provider;
        }
        public SignerOrProvider(Account account)
        {
            if (account == null)
            {
                throw new ArgumentException("Signer is not provided");
            }
            Account = account;
        }

        public SignerOrProvider(Account account, Web3 provider)
        {
            if (account == null && provider == null)
            {
                throw new ArgumentException("Either account or provider should be set, but not both.");
            }
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

        public static Web3 GetProvider(dynamic signerOrProvider)
        {
            if (signerOrProvider is Web3)
            {
                return signerOrProvider as Web3;
            }
            else if (signerOrProvider is SignerOrProvider)
            {
                return (signerOrProvider as SignerOrProvider)?.Provider!;
            }
            else if(signerOrProvider is IClient)
            {
                return new Web3(signerOrProvider);
            }
            else if(signerOrProvider is Account)
            {
                return new Web3(signerOrProvider?.TransactionManager?.Client);
            }
            else
            {
                return null!;
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
                return (signerOrProvider as SignerOrProvider)?.Account!;
            }
            else
            {
                return null!;
            }
        }

        public static Web3 GetProviderOrThrow(object signerOrProvider)
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

        public static async Task<bool> CheckNetworkMatches(dynamic signerOrProvider, int chainId)
        {
            Web3 provider;
            Account account;

            if (signerOrProvider is SignerOrProvider)
            {
                provider = (signerOrProvider as SignerOrProvider)?.Provider;
                account = (signerOrProvider as SignerOrProvider)?.Account;
            }
            else if (signerOrProvider is Web3)
            {
                provider = signerOrProvider as Web3;
            }
            else
            {
                provider = null!;
            }

            if (provider == null)
            {
                throw new MissingProviderArbSdkError("signerOrProvider");
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
