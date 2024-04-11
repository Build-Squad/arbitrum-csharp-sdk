using System.Threading.Tasks;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;

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
        public byte[]? Data { get; set; }
    }

    public class L1ToL2MessageParams : L1ToL2MessageNoGasParams
    {
        public new string? ExcessFeeRefundAddress { get; set; }
        public new string? CallValueRefundAddress { get; set; }
    }
    public class TransactionRequest : TransactionInput            //////////
    {
        public new string? To { get; set; }
        public new string? Data { get; set; }
        public new BigInteger? Value { get; set; }
        public new string? From { get; set; }
    }

    public class L1ToL2TransactionRequest
    {
        public TransactionRequest? TxRequest { get; set; }
        public RetryableData? RetryableData { get; set; }
        public Func<Task<bool>>? IsValid { get; set; }

    }

    public class L2ToL1TransactionRequest
    {
        public TransactionRequest? TxRequest { get; }
        public Func<Task<BigInteger>>? EstimateL1GasLimit { get; set; }

    }

    public static class TransactionUtils
    {
        public static bool IsL1ToL2TransactionRequest<T>(T possibleRequest)
        {
            return possibleRequest is L1ToL2TransactionRequest;
        }

        public static bool IsL2ToL1TransactionRequest<T>(T possibleRequest)
        {
            return possibleRequest is L2ToL1TransactionRequest;
        }

        public static bool IsDefined<T>(T val)
        {
            return val != null;
        }
    }
}
