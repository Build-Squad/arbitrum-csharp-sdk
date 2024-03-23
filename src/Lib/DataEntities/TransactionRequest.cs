using System.Threading.Tasks;
using System.Numerics;

namespace Arbitrum.DataEntities
{
    public class L1ToL2MessageGasParams
    {
        public BigInteger? MaxSubmissionCost { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }
        public BigInteger? GasLimit { get; set; }
        public BigInteger? Deposit { get; set; }
    }

    public class L1ToL2MessageNoGasParams
    {
        public string? From { get; set; }
        public string? To { get; set; }
        public BigInteger? L2CallValue { get; set; }
        public string? ExcessFeeRefundAddress { get; set; }
        public string? CallValueRefundAddress { get; set; }
        public byte[] Data { get; set; }
    }

    public class L1ToL2MessageParams : L1ToL2MessageNoGasParams
    {
        public new string? ExcessFeeRefundAddress { get; set; }
        public new string? CallValueRefundAddress { get; set; }
    }
    public class TransactionRequest
    {
        public string To { get; set; }
        public byte[] Data { get; set; }
        public BigInteger Value { get; set; }
        public string From { get; set; }
    }

    public class L1ToL2TransactionRequest
    {
        public TransactionRequest TxRequest { get; set; }
        public RetryableData RetryableData { get; set; }
        public Func<Task<bool>>? IsValid { get; set; }


        public L1ToL2TransactionRequest(TransactionRequest txRequest, RetryableData retryableData)
        {
            TxRequest = txRequest;
            RetryableData = retryableData;
        }
    }

    public class L2ToL1TransactionRequest
    {
        public TransactionRequest TxRequest { get; }
        public object EstimateL1GasLimit { get; }

        public L2ToL1TransactionRequest(TransactionRequest txRequest, object estimateL1GasLimit)
        {
            TxRequest = txRequest;
            EstimateL1GasLimit = estimateL1GasLimit;
        }
    }

    public static class Utils
    {
        public static bool IsL1ToL2TransactionRequest<T>(T possibleRequest)
        {
            if (possibleRequest is L1ToL2TransactionRequest)
            {
                var l1ToL2Request = (L1ToL2TransactionRequest)(object)possibleRequest;
                return l1ToL2Request.TxRequest != null;
            }
            return false;
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
