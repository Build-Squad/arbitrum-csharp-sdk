using System.Threading.Tasks;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.JsonRpc.Client;
using System.Reflection;

namespace Arbitrum.DataEntities
{
    public class TransactionRequest : TransactionInput
    {

    }
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
        public byte[]? Data { get; set; }
    }

    public class L1ToL2MessageParams : L1ToL2MessageNoGasParams
    {
        public new string? ExcessFeeRefundAddress { get; set; }
        public new string? CallValueRefundAddress { get; set; }
    }

    public class L1ToL2TransactionRequest 
    {
        public TransactionRequest? TxRequest { get; set; }
        public RetryableData? RetryableData { get; set; }
        public Func<Task<bool>>? IsValid { get; set; }
        public L1ToL2TransactionRequest()
        { }
        public L1ToL2TransactionRequest(TransactionRequest? txRequest, RetryableData? retryableData)
        {
            TxRequest = txRequest;
            RetryableData = retryableData;
        }

    }

    public class L2ToL1TransactionRequest
    {
        public TransactionRequest? TxRequest { get; set; }
        public Func<IClient, Task<BigInteger>>? EstimateL1GasLimit { get; set; }

    }

    public static class TransactionUtils
    {
        public static bool IsL1ToL2TransactionRequest(dynamic possibleRequest)
        {
            // Get the type of the possibleRequest object
            Type type = possibleRequest.GetType();

            // Get the PropertyInfo object for the TxRequest property
            PropertyInfo property = type.GetProperty("TxRequest");

            // Check if the property exists
            return (property != null);
        }

        public static bool IsL2ToL1TransactionRequest(dynamic possibleRequest)
        {
            return possibleRequest?.TxRequest != null;
        }

        public static bool IsDefined<T>(T val)
        {
            return val != null;
        }
    }
}
