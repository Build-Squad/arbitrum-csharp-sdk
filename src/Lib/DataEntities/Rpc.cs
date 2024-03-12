using Nethereum.RPC.Eth.DTOs;
using System.Numerics;

namespace Arbitrum.DataEntities
{
    public interface IArbBlock 
    {
        /**
         * The merkle root of the withdrawals tree
         */
        string SendRoot { get; set; }
        /**
         * Cumulative number of withdrawals since genesis
         */
        BigInteger SendCount { get; set; }
        /**
         * The l1 block number as seen from within this l2 block
         */
        int L1BlockNumber { get; set; }
    }
    public class ArbBlock : Block, IArbBlock
    {
        public string SendRoot { get; set; }
        public BigInteger SendCount { get; set; }
        public int L1BlockNumber { get; set; }
    }

    public interface IArbBlockWithTransactions
    {
        string SendRoot { get; set; }
        BigInteger SendCount { get; set; }
        int L1BlockNumber { get; set; }
    }
    public class ArbBlockWithTransactions : BlockWithTransactions, IArbBlockWithTransactions
    {
        public string SendRoot { get; set; }
        public BigInteger SendCount { get; set; }
        public int L1BlockNumber { get; set; }
    }
    /**
     * Eth transaction receipt with additional arbitrum specific fields
     */
    public class ArbTransactionReceipt : TransactionReceipt
    {
        /**
         * The l1 block number that would be used for block.number calls
         * that occur within this transaction.
         * See https://developer.offchainlabs.com/docs/time_in_arbitrum
         */
        public int L1BlockNumber { get; set; }
        /**
         * Amount of gas spent on l1 computation in units of l2 gas
         */
        public BigInteger GasUsedForL1 { get; set; }
    }
}
