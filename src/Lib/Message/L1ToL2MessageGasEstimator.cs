using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Arbitrum.DataEntities;

namespace Arbitrum.Message
{

    public class GasOverrides
    {
        public PercentIncreaseWithMin? GasLimit { get; set; }
        public PercentIncrease? MaxSubmissionFee { get; set; }
        public PercentIncrease? MaxFeePerGas { get; set; }
        public PercentIncreaseWithOnlyBase? Deposit { get; set; }
    }
    public class  PercentIncrease
    {
        public BigInteger? Base {  get; set; }
        public BigInteger? PercentInc {  get; set; }
    }
    public class PercentIncreaseWithOnlyBase
    {
        public BigInteger? Base { get; set; }
    }
    public class PercentIncreaseWithMin
    {
        public PercentIncrease? Increase { get; set; }
        public BigInteger? Min { get; set; }
    }

    public class L1ToL2MessageGasEstimator
    {
        public readonly Web3 _l2Provider;
        public L1ToL2MessageGasEstimator(Web3 l2provider)
        {
            _l2Provider = l2provider;
        }

        public async Task<L1ToL2MessageGasParams> EstimateAll(
            L1ToL2MessageNoGasParams retryableEstimateData,
            BigInteger l1BaseFee,
            Web3 l1Provider,
            GasOverrides? options
            )
        {
            return await 
        }

        public static async Task<bool> IsValid(L1ToL2MessageGasParams estimates, L1ToL2MessageGasParams reEstimates)
        {
            return await Task.Run(() =>
            {
                return estimates.MaxFeePerGas >= reEstimates.MaxFeePerGas
                    && estimates.MaxSubmissionCost >= reEstimates.MaxSubmissionCost;
            });
        }


    }
}
