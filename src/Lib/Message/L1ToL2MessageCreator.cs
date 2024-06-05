using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using Arbitrum.Message;
using Arbitrum.Utils;
using Arbitrum.DataEntities;
using Nethereum.RPC.Eth.DTOs;
using Org.BouncyCastle.Asn1.X509;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq.Expressions;
using Nethereum.JsonRpc.Client;

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

            var excessFeeRefundAddress = parameters?.ExcessFeeRefundAddress ?? parameters.From;
            var callValueRefundAddress = parameters?.CallValueRefundAddress ?? parameters.From;

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

            //var net = NetworkUtils.AddDefaultLocalNetwork();
            //var l2Network = net.l2Network;

            bool nativeTokenIsEth = l2Network.NativeToken == null;

            var inboxContract = await LoadContractUtils.LoadContract(
                                                    contractName: "Inbox",
                                                    provider: l1Provider,
                                                    address: l2Network?.EthBridge?.Inbox,
                                                    isClassic: false
                                                    );

            // Encode ABI
            var functionData = inboxContract.ContractBuilder.GetFunctionAbi("createRetryableTicket");


            var txRequest = new TransactionRequest()
            {
                To = l2Network?.EthBridge?.Inbox,
                Data = functionData.Sha3Signature,
                Value = nativeTokenIsEth ? (new HexBigInteger(estimates.Deposit.ToString()) ?? new HexBigInteger(BigInteger.Zero)) : new HexBigInteger(BigInteger.Zero),
                From = parameters.From
            };

            var retryableData = new RetryableData()
            {
                Data = parameters.Data,
                From = parameters.From,
                To = parameters.To,
                ExcessFeeRefundAddress = excessFeeRefundAddress,
                CallValueRefundAddress = callValueRefundAddress,
                L2CallValue = parameters.L2CallValue,
                MaxSubmissionCost = estimates.MaxSubmissionCost,
                MaxFeePerGas = estimates.MaxFeePerGas,
                GasLimit = estimates.GasLimit,
                Deposit = estimates.Deposit
            };

            async Task<bool> IsValid()
            {
                var reEstimates = await GetTicketEstimate(parsedParams, l1Provider, l2Provider, options);
                return await L1ToL2MessageGasEstimator.IsValid(estimates, reEstimates);
            }

            return new L1ToL2TransactionRequest(txRequest, retryableData)
            {
                TxRequest = txRequest,
                RetryableData = retryableData,
                IsValid = IsValid,
            };
        }

        public async Task<L1TransactionReceipt> CreateRetryableTicket(dynamic parameters, Web3 l2Provider, GasOverrides? options = null)    /////////
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

            var txReceipt = await l1Signer.Provider.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(new TransactionRequest
            {
                From = createRequest.TxRequest.From,
                To = createRequest.TxRequest.To,
                Gas = createRequest.TxRequest.Gas,
                GasPrice = createRequest.TxRequest.GasPrice,
                Value = createRequest.TxRequest.Value,
                Data = createRequest.TxRequest.Data,
                Nonce = createRequest.TxRequest.Nonce,
                ChainId = createRequest.TxRequest.ChainId
            });


            return L1TransactionReceipt.MonkeyPatchWait(txReceipt);
        }
    }
}