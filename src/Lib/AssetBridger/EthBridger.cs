using Arbitrum.DataEntities;
using Arbitrum.Message;
using Arbitrum.src.Lib.DataEntities;
using Arbitrum.Utils;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using System.Numerics;

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

    public class WithL1Signer<T>
    {
        public T? Item { get; set; }
        public SignerOrProvider? L1Signer { get; set; }
    }

    public class EthDepositParams
    {
        public SignerOrProvider? L1Signer { get; set; }
        public BigInteger? Amount { get; set; }
        public string? From { get; set; }
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
        public SignerOrProvider? L1Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
        public Web3? L2Provider { get; set; }
    }

    public class EthWithdrawParams
    {
        public SignerOrProvider? L2Signer { get; set; }
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

        public async Task<L1ToL2TransactionRequest> GetDepositRequest(EthDepositRequestParams parameters)
        {
            var sha3 = new Nethereum.Util.Sha3Keccack();
            var functionSignatureHash = sha3.CalculateHash("depositEth()");
            var functionData = string.Concat("0x", functionSignatureHash.AsSpan(0, 8));

            return new L1ToL2TransactionRequest()
            {
                TxRequest = new TransactionRequest
                {
                    To = _l2Network?.EthBridge?.Inbox,
                    Value = parameters?.Amount.Value.ToHexBigInteger(),
                    Data = functionData,
                    From = parameters?.From
                },
                IsValid = new Func<Task<bool>>(() => Task.FromResult(true))
            };
        }

        public override async Task<L1EthDepositTransactionReceipt> Deposit(dynamic parameters)
        {
            dynamic ethDeposit;
            var l1Signer = parameters.L1Signer;
            var provider = l1Signer.Provider;

            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                ethDeposit = parameters;
            }
            else if (parameters is EthDepositParams or L1ToL2TxReqAndSigner)
            {
                ethDeposit = await GetDepositRequest(new EthDepositRequestParams()
                {
                    From = parameters.L1Signer.Account.Address,
                    Amount = parameters.Amount,
                    L1Signer = parameters.L1Signer,
                    Overrides = parameters.Overrides
                });
            }
            else
            {
                throw new ArgumentException("Invalid parameter type. Expected EthDepositParams or L1ToL2TxReqAndSigner.");
            }

            var tx = new TransactionRequest
            {
                To = ethDeposit?.TxRequest?.To,
                Value = ethDeposit?.TxRequest?.Value ?? BigInteger.Zero.ToHexBigInteger(),
                Data = ethDeposit?.TxRequest?.Data,
                From = ethDeposit?.TxRequest?.From
            };

            tx.From ??= l1Signer?.Account.Address;
            tx.Gas ??= await provider.Eth.TransactionManager.EstimateGasAsync(tx);

            var txnSign = await provider.TransactionManager.SignTransactionAsync(tx);
            var txnHash = await provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(txnSign);
            var receipt = await provider.Eth.TransactionManager.TransactionReceiptService.PollForReceiptAsync(txnHash);

            return L1TransactionReceipt.MonkeyPatchEthDepositWait(receipt);
        }

        public async Task<L1ToL2TransactionRequest> GetDepositToRequest(EthDepositToRequestParams parameters)
        {
            var requestParams = new L1ToL2MessageParams()
            {
                Value = parameters.Amount.Value.ToHexBigInteger(),
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
                gasOverrides);
        }

        public async Task<L1TransactionReceipt> DepositTo(dynamic parameters)
        {
            await CheckL1Network(parameters.L1Signer);
            await CheckL2Network(parameters.L2Provider);

            dynamic retryableTicketRequest;

            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                retryableTicketRequest = parameters;
            }
            else if (parameters is EthDepositToParams)
            {
                retryableTicketRequest = await GetDepositToRequest(new EthDepositToRequestParams
                {
                    From = parameters?.L1Signer?.Account.Address,
                    L1Provider = parameters?.L1Signer?.Provider,
                    L1Signer = parameters?.L1Signer,
                    Amount = parameters?.Amount,
                    L2Provider = parameters?.L2Provider,
                    DestinationAddress = parameters?.DestinationAddress
                });
            }
            else
            {
                throw new ArgumentException("Invalid parameter type. Expected EthDepositParams or L1ToL2TxReqAndSigner.");
            }

            var tx = new TransactionRequest
            {
                To = retryableTicketRequest?.TxRequest?.To,
                Value = retryableTicketRequest?.TxRequest?.Value,
                Data = retryableTicketRequest?.TxRequest?.Data,
                From = retryableTicketRequest?.TxRequest?.From
            };

            tx.From ??= parameters?.L1Signer?.Address;
            tx.Gas ??= await parameters.L1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(tx);

            var signedTx = await parameters.L1Signer.Provider.Eth.TransactionManager.SignTransactionAsync(tx);
            var txnHash = await parameters.L1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);
            await Task.Delay(TimeSpan.FromSeconds(2));
            var receipt = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

            return L1TransactionReceipt.MonkeyPatchContractCallWait(receipt);
        }

        public async Task<L2ToL1TransactionRequest> GetWithdrawalRequest(EthWithdrawParams parameters)
        {
            var arbSysContract = await LoadContractUtils.LoadContract(
                                                            provider: parameters.L2Signer.Provider,
                                                            contractName: "ArbSys",
                                                            address: Constants.ARB_SYS_ADDRESS,
                                                            isClassic: false
                                                            );

            var functionData = arbSysContract.GetFunction("withdrawEth").GetData(new object[] { parameters.DestinationAddress });

            return new L2ToL1TransactionRequest()
            {
                TxRequest = new TransactionRequest
                {
                    To = Constants.ARB_SYS_ADDRESS.EnsureHexPrefix(),
                    Value = (parameters?.Amount.Value.ToHexBigInteger()),
                    Data = functionData,
                    From = parameters?.From
                },
                EstimateL1GasLimit = async (Web3 l1provider) =>
                {
                    if (await Lib.IsArbitrumChain(l1provider))
                    {
                        return new BigInteger(4_000_000);
                    }

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

            await CheckL2Network(ethParams?.L2Signer);

            if (TransactionUtils.IsL2ToL1TransactionRequest(ethParams))
            {
                request = ethParams;
            }
            else 
            {
                request = await GetWithdrawalRequest(ethParams);
            }

            var tx = new TransactionRequest
            {
                To = request?.TxRequest?.To,
                Value = request?.TxRequest?.Value,
                Data = request?.TxRequest?.Data,
                From = request?.TxRequest?.From,
                MaxFeePerGas = ethParams?.Overrides?.MaxFeePerGas.Value.ToHexBigInteger(),
                MaxPriorityFeePerGas = ethParams?.Overrides?.MaxPriorityFeePerGas.Value.ToHexBigInteger()
            };

            tx.From ??= ethParams?.L2Signer?.Account.Address;
            tx.Gas ??= await ethParams.L2Signer.Provider.Eth.TransactionManager.EstimateGasAsync(tx);

            var signedTx = await ethParams.L2Signer.Account.TransactionManager.SignTransactionAsync(tx);
            var txnHash = await ethParams.L2Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);
            var receipt = await ethParams.L2Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

            return L2TransactionReceipt.MonkeyPatchWait(receipt);
        }
    }
}
