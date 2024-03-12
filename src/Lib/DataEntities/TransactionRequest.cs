using System.Threading.Tasks;

namespace Arbitrum.DataEntities
{
    public class L1ToL2MessageParams
    {
        // Add any necessary members or methods
    }

    public class L1ToL2MessageGasParams
    {
        // Add any necessary members or methods
    }

    public class L1ToL2TransactionRequest
    {
        public object TxRequest { get; }
        public object RetryableData { get; }

        public L1ToL2TransactionRequest(object txRequest, object retryableData)
        {
            TxRequest = txRequest;
            RetryableData = retryableData;
        }

        public Task<bool> IsValid()
        {
            
            throw new NotImplementedException();
        }
    }

    public class L2ToL1TransactionRequest
    {
        public object TxRequest { get; }
        public object EstimateL1GasLimit { get; }

        public L2ToL1TransactionRequest(object txRequest, object estimateL1GasLimit)
        {
            TxRequest = txRequest;
            EstimateL1GasLimit = estimateL1GasLimit;
        }
    }

    public static class Utils
    {
        public static bool IsL1ToL2TransactionRequest(object possibleRequest)
        {
            return IsDefined(possibleRequest is IDictionary<string, object> dict ? dict["txRequest"] : null);
        }

        public static bool IsL2ToL1TransactionRequest(object possibleRequest)
        {
            return IsDefined(possibleRequest is IDictionary<string, object> dict ? dict["txRequest"] : null);
        }

        public static bool IsDefined(object val)
        {
            return val != null;
        }
    }
}
