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
using Org.BouncyCastle.Utilities;
using Nethereum.JsonRpc.Client;
using Org.BouncyCastle.Asn1.Ocsp;

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

    public class EthDepositParams
    {
        public Account? L1Signer { get; set; }
        public BigInteger? Amount { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }
    public class EthDepositRequestParams : EthDepositParams
    {
        public string? From { get; set; }
    }

    public class EthDepositToParams : EthDepositParams
    {
        public Web3? L2Provider { get; set; }
        public string? DestinationAddress { get; set; }
        public GasOverrides? RetryableGasOverrides { get; set; }
    }
    public class EthDepositToRequestParams : EthDepositToParams
    {
        public Web3? L1Provider { get; set; }
        public string? From { get; set; }
    }

    public class L1ToL2TxReqAndSigner : L1ToL2TransactionRequest
    {
        public Account? L1Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
        public Web3? L2Provider { get; set; }
    }

    public class L2ToL1TxReqAndSigner : L2ToL1TransactionRequest
    {
        public Account? L2Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }

    public class EthWithdrawParams
    {
        public Account? L2Signer { get; set; }
        public BigInteger? Amount { get; set; }
        public string? DestinationAddress { get; set; }
        public string? From { get; set; }
        public PayableOverrides? Overrides { get; set; }
    }


    public class EthBridger : AssetBridger<EthDepositParams, EthWithdrawParams, L1EthDepositTransactionReceipt>
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

        //public TransactionRequest GetApproveGasTokenRequest(ApproveGasTokenParams parameters)
        //{

        //}
        //public TransactionRequest ApproveGasToken(ApproveGasTokenParams parameters)
        //{

        //}
        //public string GetDepositRequestData(ApproveGasTokenParams parameters)
        //{

        //}


        public async Task<L1ToL2TransactionRequest> GetDepositRequest(EthDepositRequestParams parameters)
        {
            var inbox = await LoadContractUtils.LoadContract(
                                            provider: new Web3(parameters?.L1Signer, parameters?.L1Signer?.TransactionManager?.Client),
                                            contractName: "Inbox",
                                            address: _l2Network?.EthBridge?.Inbox,
                                            isClassic: false
                                            );

            var functionData = inbox?.ContractBuilder.GetFunctionAbi("depositEth");
            return new L1ToL2TransactionRequest()
            {
                TxRequest = new TransactionRequest
                {
                    To = _l2Network?.EthBridge?.Inbox,
                    Value = parameters?.Amount,
                    Data = functionData?.ToString(),
                    From = parameters?.From
                },
                IsValid = new Func<Task<bool>>(() => Task.FromResult(true))
            };
        }

        public override async Task<L1EthDepositTransactionReceipt> Deposit(dynamic parameters)
        {
            await CheckL1Network(new SignerOrProvider(parameters?.L1Signer));

            dynamic ethDeposit;
            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                ethDeposit = parameters;      ///////////
            }
            else if (parameters is EthDepositParams)
            {
                ethDeposit = await GetDepositRequest(new EthDepositRequestParams()
                {
                    From = parameters?.L1Signer?.Address,
                    Amount = parameters?.Amount,
                    L1Signer = parameters?.L1Signer,
                    Overrides = parameters?.Overrides
                });
            }
            else if (parameters is L1ToL2TxReqAndSigner)
            {
                ethDeposit = await GetDepositRequest(new EthDepositRequestParams()
                {
                    From = parameters?.L1Signer?.Address,
                    L1Signer = parameters?.L1Signer,
                    Overrides = parameters?.Overrides,
                    Amount = parameters?.Amount
                });
            }
            else
            {
                throw new ArgumentException("Invalid parameter type. Expected EthDepositParams or L1ToL2TxReqAndSigner.");
            }

            var tx = new TransactionRequest
            {
                To = ethDeposit?.TxRequest?.To,
                Value = ethDeposit?.TxRequest?.Value ?? BigInteger.Zero,
                Data = ethDeposit?.TxRequest?.Data,
                From = ethDeposit?.TxRequest?.From,
                AccessList = ethDeposit?.TxRequest?.AccessList,
                ChainId = ethDeposit?.TxRequest?.ChainId,
                Gas = ethDeposit?.TxRequest?.Gas,
                GasPrice = ethDeposit?.TxRequest?.GasPrice,
                MaxFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxFeePerGas.ToString()),
                MaxPriorityFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxPriorityFeePerGas.ToString()),
                Nonce = ethDeposit?.TxRequest?.Nonce,
                Type = ethDeposit?.TxRequest?.Type,
            };

            if (tx.From == null)
            {
                tx.From = parameters?.L1Signer?.Address;
            }

            var txReceipt = await parameters?.L1Signer?.TransactionManager.SendTransactionAndWaitForReceiptAsync(tx);

            return L1TransactionReceipt.MonkeyPatchEthDepositWait(txReceipt);
        }

        public async Task<L1ToL2TransactionRequest> GetDepositToRequest(EthDepositToRequestParams parameters)
        {
            var requestParams = new L1ToL2MessageParams()
            {
                To = parameters?.DestinationAddress,
                From = parameters?.From,
                L2CallValue = parameters?.Amount,
                CallValueRefundAddress = parameters?.DestinationAddress,
                Data = "0x".HexToByteArray()
            };

            var gasOverrides = parameters?.RetryableGasOverrides ?? null;

            return await L1ToL2MessageCreator.GetTicketCreationRequest(
                requestParams,
                parameters?.L1Provider,
                parameters?.L2Provider,
                gasOverrides
                );
        }

        public async Task<L1TransactionReceipt> DepositTo(dynamic parameters)
        {
            await CheckL1Network(new SignerOrProvider(parameters.L1Signer!));
            await CheckL2Network(new SignerOrProvider(parameters.L2Provider!));

            // Assuming we have an interface and helper methods for type checking
            dynamic retryableTicketRequest;

            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                retryableTicketRequest = parameters;   ////////
            }
            else if (parameters is EthDepositToParams)
            {
                retryableTicketRequest = await GetDepositToRequest(new EthDepositToRequestParams
                {
                    From = parameters?.L1Signer?.Address ?? null,
                    L1Provider = new Web3(parameters?.L1Signer?.TransactionManager.Client) ?? null,
                    L1Signer = parameters?.L1Signer ?? null,
                    Amount = parameters?.Amount ?? null,
                    Overrides = parameters?.Overrides ?? null,
                    L2Provider = parameters?.L2Provider ?? null,
                    DestinationAddress = parameters?.DestinationAddress ?? null,
                    RetryableGasOverrides = parameters?.RetryableGasOverrides ?? null
                });
            }
            else if (parameters is L1ToL2TxReqAndSigner)
            {
                retryableTicketRequest = await GetDepositToRequest(new EthDepositToRequestParams
                {
                    From = parameters?.L1Signer?.Address ?? null,
                    L1Provider = new Web3(parameters?.L1Signer?.TransactionManager.Client) ?? null,
                    L1Signer = parameters?.L1Signer ?? null,
                    Amount = parameters?.Amount ?? null,
                    Overrides = parameters?.Overrides ?? null,
                    L2Provider = parameters?.L2Provider ?? null,
                    DestinationAddress = parameters?.DestinationAddress ?? null,
                    RetryableGasOverrides = parameters?.RetryableGasOverrides ?? null,
                });
            }
            else
            {
                throw new ArgumentException("Invalid parameter type. Expected EthDepositParams or L1ToL2TxReqAndSigner.");
            }

            var tx = new TransactionRequest
            {
                To = retryableTicketRequest?.TxRequest?.To,
                Value = retryableTicketRequest?.TxRequest?.Value ?? BigInteger.Zero,
                Data = retryableTicketRequest?.TxRequest?.Data,
                From = retryableTicketRequest?.TxRequest?.From,
                AccessList = retryableTicketRequest?.TxRequest?.AccessList,
                ChainId = retryableTicketRequest?.TxRequest?.ChainId,
                Gas = retryableTicketRequest?.TxRequest?.Gas,
                GasPrice = retryableTicketRequest?.TxRequest?.GasPrice,
                MaxFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxFeePerGas.ToString()),
                MaxPriorityFeePerGas = new HexBigInteger(parameters?.Overrides?.MaxPriorityFeePerGas.ToString()),
                Nonce = retryableTicketRequest?.TxRequest?.Nonce,
                Type = retryableTicketRequest?.TxRequest?.Type,
            };

            if (tx.From == null)
            {
                tx.From = parameters?.L1Signer?.Address;
            }

            var txReceipt = await parameters?.L1Signer?.TransactionManager.SendTransactionAndWaitForReceiptAsync(tx);


            return L1TransactionReceipt.MonkeyPatchContractCallWait(txReceipt);
        }

        public async Task<L2ToL1TransactionRequest> GetWithdrawalRequest(EthWithdrawParams parameters)
        {
            var arbSysContract = await LoadContractUtils.LoadContract(
                                                            provider: new Web3(parameters?.L2Signer?.TransactionManager?.Client),
                                                            contractName: "ArbSys",
                                                            address: Constants.ARB_SYS_ADDRESS,
                                                            isClassic: false
                                                            );
            var functionData = arbSysContract?.ContractBuilder.GetFunctionAbi("withdrawEth");

            return new L2ToL1TransactionRequest()
            {
                TxRequest = new TransactionRequest
                {
                    To = Constants.ARB_SYS_ADDRESS,
                    Value = parameters?.Amount,
                    Data = functionData?.ToString(),
                    From = parameters?.From
                },
                EstimateL1GasLimit = async (IClient l1provider) =>
                {
                    if (await Lib.IsArbitrumChain(new Web3(l1provider)))
                    {
                        // values for L3 are dependent on the L1 base fee, so hardcoding can never be accurate
                        // however, this is only an estimate used for display, so should be good enough
                        //
                        // measured with withdrawals from Xai and Rari then added some padding
                        return new BigInteger(4_000_000);
                    }

                    // measured 126998 - add some padding
                    return new BigInteger(130000);
                }
            };
        }

        public override async Task<L2TransactionReceipt> Withdraw(dynamic ethParams)
        {

            dynamic request;
            if (!SignerProviderUtils.SignerHasProvider(ethParams?.L2Signer))
            {
                throw new MissingProviderArbSdkError("L2Signer");
            }
            await CheckL2Network(new SignerOrProvider(ethParams?.L2Signer));

            if (TransactionUtils.IsL2ToL1TransactionRequest(ethParams))
            {
                request = ethParams;
            }

            else if (ethParams is EthWithdrawParams)
            {
                request = await GetWithdrawalRequest(ethParams);
            }

            else if (ethParams is L2ToL1TxReqAndSigner)
            {
                request = await GetWithdrawalRequest(new EthWithdrawParams()
                {
                    L2Signer = ethParams?.L2Signer,
                    From = ethParams?.TxRequest?.From,
                    //DestinationAddress = 
                    //Amount = ethParams.Overrides,
                    Overrides = ethParams?.Overrides,
                });
            }

            else
            {
                throw new ArgumentException("Invalid parameter type. Expected EthDepositParams or L1ToL2TxReqAndSigner.");
            }

            var tx = new TransactionRequest
            {
                To = request?.TxRequest?.To,
                Value = request?.TxRequest?.Value ?? BigInteger.Zero,
                Data = request?.TxRequest?.Data,
                From = request?.TxRequest?.From,
                AccessList = request?.TxRequest?.AccessList,
                ChainId = request?.TxRequest?.ChainId,
                Gas = request?.TxRequest?.Gas,
                GasPrice = request?.TxRequest?.GasPrice,
                MaxFeePerGas = new HexBigInteger(ethParams?.Overrides?.MaxFeePerGas.ToString()),
                MaxPriorityFeePerGas = new HexBigInteger(ethParams?.Overrides?.MaxPriorityFeePerGas.ToString()),
                Nonce = request?.TxRequest?.Nonce,
                Type = request?.TxRequest?.Type,
            };

            if (tx.From == null)
            {
                tx.From = ethParams?.L2Signer?.Address;
            }

            var txReceipt = await ethParams?.L2Signer?.TransactionManager?.SendTransactionAndWaitForReceiptAsync(tx);

            return L2TransactionReceipt.MonkeyPatchWait(txReceipt);
        }
    }
}
