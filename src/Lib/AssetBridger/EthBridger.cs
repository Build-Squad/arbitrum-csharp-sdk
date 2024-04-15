using Arbitrum.DataEntities;
using Arbitrum.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading.Tasks;
using Arbitrum.AssetBridgerModule;
using Nethereum.Web3;
using Arbitrum.Utils;
using Nethereum.RPC.Eth.DTOs;
using System.Reflection.Metadata.Ecma335;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3.Accounts;

namespace Arbitrum.AssetBridgerModule
{

    public class ApproveGasTokenParams
    {
        public BigInteger? Amount { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }

    public class ApproveGasTokenTxRequest
    {
        public TransactionRequest? TxRequest { get; set; }
        public Overrides? Overrides { get; set; }
    }

    // Define the ApproveGasTokenParamsOrTxRequest class
    public class ApproveGasTokenParamsOrTxRequest
    {
        // Define properties to hold either ApproveGasTokenParams or ApproveGasTokenTxRequest
        public ApproveGasTokenParams? ApproveGasTokenParams { get; set; }
        public ApproveGasTokenTxRequest? ApproveGasTokenTxRequest { get; set; }
    }

    public class WithL1Signer<T>
    {
        public T? Item { get; set; }
        public SignerOrProvider? L1Signer { get; set; }
    }

    public class EthWithdrawParams
    {
        public BigInteger? Amount { get; set; }
        public string? DestinationAddress { get; set; }
        public string? From { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }

    public class EthDepositParams
    {
        public Account? L1Signer { get; set; }
        public BigInteger? Amount { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }

    public class EthDepositToParams : EthDepositParams
    {
        public Web3? L2Provider { get; set; }
        public string? DestinationAddress { get; set; }
        public GasOverrides? RetryableGasOverrides { get; set; }
    }

    public class L1ToL2TxReqAndSigner : L1ToL2TransactionRequest
    {
        public Account? L1Signer { get; set; }
        public Overrides? Overrides { get; set; }
    }

    public class L2ToL1TxReqAndSigner : L2ToL1TransactionRequest
    {
        public Account? L2Signer { get; set; }
        public Overrides? Overrides { get; set; }
    }

    public class EthDepositRequestParams : EthDepositParams
    {
        public new BigInteger? Amount { get; set; }
        public string? DestinationAddress { get; set; }
        public string? From { get; set; }
    }

    public class EthDepositToRequestParams : EthDepositToParams
    {
        public Web3? L1Provider { get; set; }
        public new BigInteger? Amount { get; set; }
        public new string? DestinationAddress { get; set; }
        public string? From { get; set; }
    }

    public class EthBridger : AssetBridger<EthDepositParams, EthWithdrawParams>
    {
        private readonly L2Network _l2Network;

        public EthBridger(L2Network l2Network) : base(l2Network)
        {
            _l2Network = l2Network;
        }

        public static async Task<EthBridger> FromProvider(Web3 l2Provider)
        {
            return new EthBridger(await NetworkUtils.GetL2NetworkAsync(l2Provider));
        }

        private static async Task<L2Network> GetL2Network(Web3 l2Provider)
        {
            return await NetworkUtils.GetL2NetworkAsync(l2Provider);
        }

        private bool IsApproveGasTokenParams(dynamic parameters)
        {
            return parameters is WithL1Signer<ApproveGasTokenParams> && ((ApproveGasTokenTxRequest)parameters).TxRequest == null;
        }

        public TransactionRequest GetApproveGasTokenRequest(ApproveGasTokenParams parameters)
        {

        }
        public TransactionRequest ApproveGasToken(ApproveGasTokenParams parameters)
        {

        }
        public string GetDepositRequestData(ApproveGasTokenParams parameters)
        {

        }


        public async Task<L1ToL2TransactionRequest> GetDepositRequest(EthDepositRequestParams parameters)
        {
            var inbox = await LoadContractUtils.LoadContract(
                                            provider: new Web3(parameters.L1Signer),
                                            contractName: "Inbox",
                                            address: _l2Network?.EthBridge?.Inbox,
                                            isClassic: false
                                            );

            var functionData = inbox.ContractBuilder.GetFunctionAbi("depositEth");

            return new L1ToL2TransactionRequest()
            {
                TxRequest = new TransactionRequest
                {
                    To = _l2Network?.EthBridge?.Inbox,
                    Value = parameters.Amount,
                    Data = functionData.ToString(),
                    // Assuming parameters.From contains the sender address
                    From = parameters.From
                },
                IsValid = new Func<Task<bool>>(() => Task.FromResult(true))
            };
        }

        public async Task<L1TransactionReceipt> Deposit(EthDepositParams parameters)
        {
            await CheckL1Network(parameters.L1Signer);

            dynamic ethDeposit;
            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                ethDeposit = parameters;
            }
            else
            {
                ethDeposit = await GetDepositRequest(parameters);   ////////
            }

            var tx = new TransactionRequest
            {
                To = ethDeposit?.TxRequest?.To,
                Value = ethDeposit?.TxRequest?.Value ?? BigInteger.Zero,
                Data = ethDeposit?.TxRequest?.Data
            };

            if (tx.From == null)
            {
                tx.From = parameters.L1Signer.Address;
            }

            var txReceipt = await parameters.L1Signer.Provider.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(tx);

            L1TransactionReceipt.MonkeyPatchEthDepositWait(txReceipt);
        }

        public async Task<L1TransactionReceipt> Deposit(L1ToL2TxReqAndSigner parameters)
        {
            await CheckL1Network(parameters.L1Signer);

            dynamic ethDeposit;
            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                ethDeposit = parameters;
            }
            else
            {
                ethDeposit = await GetDepositRequest(parameters);   ////////
            }

            var tx = new TransactionRequest
            {
                To = ethDeposit?.TxRequest?.To,
                Value = ethDeposit?.TxRequest?.Value ?? BigInteger.Zero,
                Data = ethDeposit?.TxRequest?.Data
            };

            if (tx.From == null)
            {
                tx.From = parameters.L1Signer.Account.Address;
            }

            var txReceipt = await parameters.L1Signer.Provider.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(tx);

            L1TransactionReceipt.MonkeyPatchEthDepositWait(txReceipt);
        }

        public async Task<L1ToL2TransactionRequest> GetDepositToRequest(EthDepositToRequestParams parameters)
        {
            var requestParams = new L1ToL2MessageParams()
            {
                To = parameters.DestinationAddress,
                From = parameters.From,
                L2CallValue = parameters.Amount,
                CallValueRefundAddress = parameters.DestinationAddress,
                Data = "0x".HexToByteArray()
            };

            var gasOverrides = parameters.RetryableGasOverrides?? null;

            return await L1ToL2MessageCreator.GetTicketCreationRequest(
                requestParams,
                parameters.L1Provider,
                parameters.L2Provider,
                gasOverrides
                );
        }

        public async Task<L1TransactionReceipt> DepositTo(EthDepositToParams parameters)
        {

        }

        public async Task<L2ToL1TransactionRequest> GetWithdrawalRequest(EthWithdrawParams parameters)
        {

        }

        public async Task<L1TransactionReceipt> Withdraw(EthWithdrawParams parameters)
        {

        }






    }
}
