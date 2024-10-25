using Arbitrum.ContractFactory;
using Arbitrum.DataEntities;
using Arbitrum.src.Lib.DataEntities;
using Arbitrum.Utils;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;

namespace Arbitrum.Message
{
    public class L1ToL2MessageCreator
    {
        private readonly SignerOrProvider l1Signer;

        public L1ToL2MessageCreator(SignerOrProvider l1Signer)
        {
            this.l1Signer = l1Signer ?? throw new ArgumentNullException(nameof(l1Signer));
            if (!SignerProviderUtils.SignerHasProvider(l1Signer))
                throw new MissingProviderArbSdkError("l1Signer");
        }

        public static async Task<L1ToL2MessageGasParams> GetTicketEstimate(
        L1ToL2MessageNoGasParams parameters,
        Web3 l1Provider,
        Web3 l2Provider,
        GasOverrides? retryableGasOverrides = null)
        {
            var baseFee = await Lib.GetBaseFee(l1Provider);

            var gasEstimator = new L1ToL2MessageGasEstimator(l2Provider);
            return await gasEstimator.EstimateAll(parameters, baseFee, l1Provider, retryableGasOverrides);
        }

        public static async Task<L1ToL2TransactionRequest> GetTicketCreationRequest(L1ToL2MessageParams parameters, Web3 l1Provider, Web3 l2Provider, GasOverrides? options = null)
        {
            var excessFeeRefundAddress = parameters?.ExcessFeeRefundAddress ?? parameters?.From;
            var callValueRefundAddress = parameters?.CallValueRefundAddress ?? parameters?.From;

            var parsedParams = new L1ToL2MessageNoGasParams
            {
                From = parameters?.From,
                To = parameters?.To,
                L2CallValue = parameters?.L2CallValue,
                ExcessFeeRefundAddress = excessFeeRefundAddress,
                CallValueRefundAddress = callValueRefundAddress,
                Data = parameters?.Data
            };

            var estimates = await GetTicketEstimate(parsedParams, l1Provider, l2Provider, options);

            var l2Network = await NetworkUtils.GetL2Network(l2Provider);

            var funcParams = new CreateRetryableTicketFunction
            {
                To = parameters.To,
                L2CallValue = parameters.L2CallValue.Value,
                MaxSubmissionCost = estimates.MaxSubmissionCost.Value,
                ExcessFeeRefundAddress = excessFeeRefundAddress,
                CallValueRefundAddress = callValueRefundAddress,
                GasLimit = estimates.GasLimit.Value,
                MaxFeePerGas = estimates.MaxFeePerGas.Value,
                Data = parameters.Data
            };

            var contractHandler = l2Provider.Eth.GetContractHandler(l2Network?.EthBridge?.Inbox);
            var funcHandler = contractHandler.GetFunction<CreateRetryableTicketFunction>();
            var funcData = funcHandler.GetData(funcParams);

            var txRequest = new TransactionRequest
            {
                To = l2Network?.EthBridge?.Inbox,
                Data = funcData,
                Value = estimates?.Deposit?.ToHexBigInteger(),
                From = parameters.From
            };

            var retryableData = new RetryableData
            {
                Data = parameters.Data,
                From = parameters.From,
                To = parameters.To,
                ExcessFeeRefundAddress = excessFeeRefundAddress,
                CallValueRefundAddress = callValueRefundAddress,
                L2CallValue = parameters.L2CallValue,
                MaxSubmissionCost = estimates?.MaxSubmissionCost,
                MaxFeePerGas = estimates?.MaxFeePerGas,
                GasLimit = estimates?.GasLimit,
                Value = estimates?.Deposit
            };

            async Task<bool> IsValid()
            {
                var reEstimates = await GetTicketEstimate(parsedParams, l1Provider, l2Provider, options);
                return await L1ToL2MessageGasEstimator.IsValid(estimates!, reEstimates);
            }

            return new L1ToL2TransactionRequest(txRequest, retryableData)
            {
                TxRequest = txRequest,
                RetryableData = retryableData,
                IsValid = IsValid,
            };
        }

        public async Task<L1TransactionReceipt> CreateRetryableTicket(dynamic parameters, Web3 l2Provider, GasOverrides? options = null)
        {
            var l1Provider = SignerProviderUtils.GetProviderOrThrow(l1Signer);

            var createRequest = TransactionUtils.IsL1ToL2TransactionRequest(parameters)
                ? parameters
                : await GetTicketCreationRequest(
                    parameters,
                    l1Provider,
                    l2Provider,
                    options
                );

            var tx = createRequest.TxRequest;

            tx.From ??= l1Signer?.Account?.Address;

            tx.Gas ??= await l1Provider.Eth.TransactionManager.EstimateGasAsync(tx);

            var txnSign = await l1Provider.TransactionManager.SignTransactionAsync(tx);
            var txnHash = await l1Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(txnSign);
            var txReceipt = await l1Provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txnHash);

            return L1TransactionReceipt.MonkeyPatchWait(txReceipt);
        }
    }
}