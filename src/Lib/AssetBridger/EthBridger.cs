using Arbitrum.DataEntities;
using Arbitrum.Message;
using System.Numerics;
using Nethereum.Web3;
using Arbitrum.Utils;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using NUnit.Framework;
using Nethereum.HdWallet;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using System.Reflection;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.TransactionReceipts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nethereum.RPC.Eth.DTOs;
using Arbitrum.AssetBridger;
using Arbitrum.src.Lib.DataEntities;

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

    public class L2ToL1TxReqAndSigner : L2ToL1TransactionRequest
    {
        public SignerOrProvider? L2Signer { get; set; }
        public PayableOverrides? Overrides { get; set; }
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

        public static async Task<EthBridger> FromProvider(Web3 l2Provider)
        {
            return new EthBridger(await NetworkUtils.GetL2Network(l2Provider));
        }

        private static async Task<L2Network> GetL2Network(Web3 l2Provider)
        {
            return await NetworkUtils.GetL2Network(l2Provider);
        }

        private bool IsApproveGasTokenParams(dynamic parameters)
        {
            return parameters is WithL1Signer<ApproveGasTokenParams> && ((ApproveGasTokenTxRequest)parameters).TxRequest == null;
        }

        public async Task<L1ToL2TransactionRequest> GetDepositRequest(EthDepositRequestParams parameters)
        {
            var inbox = await LoadContractUtils.LoadContract(
                                            provider: parameters.L1Signer.Provider,
                                            contractName: "TestDepositEth",
                                            address: _l2Network?.EthBridge?.Inbox,
                                            isClassic: false
                                            );

            var functionData = inbox.GetFunction("test_depositEth_FromContract").GetData();

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
            // Initialize ethDeposit variable
            dynamic ethDeposit;
            SignerOrProvider l1Signer = parameters.L1Signer;
            Web3 provider = l1Signer.Provider;

            // Check the type of parameters and set ethDeposit accordingly
            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                ethDeposit = parameters;
            }
            else if (parameters is EthDepositParams)
            {
                // Get deposit request for EthDepositParams
                ethDeposit = await GetDepositRequest(new EthDepositRequestParams()
                {
                    From = parameters.L1Signer.Account.Address,
                    Amount = parameters.Amount,
                    L1Signer = parameters.L1Signer,
                    Overrides = parameters.Overrides
                });
            }
            else if (parameters is L1ToL2TxReqAndSigner)
            {
                // Get deposit request for L1ToL2TxReqAndSigner
                ethDeposit = await GetDepositRequest(new EthDepositRequestParams()
                {
                    From = parameters.L1Signer.Account.Address,
                    L1Signer = parameters.L1Signer,
                    Overrides = parameters.Overrides,
                    Amount = parameters.Amount
                });
            }
            else
            {
                // Invalid parameter type
                throw new ArgumentException("Invalid parameter type. Expected EthDepositParams or L1ToL2TxReqAndSigner.");
            }

            // Create transaction input
            var tx = new TransactionRequest
            {
                To = ethDeposit?.TxRequest?.To,
                Value = ethDeposit?.TxRequest?.Value ?? BigInteger.Zero.ToHexBigInteger(),
                Data = ethDeposit?.TxRequest?.Data,
                From = ethDeposit?.TxRequest?.From,
                AccessList = ethDeposit?.TxRequest?.AccessList,
                ChainId = ethDeposit?.TxRequest?.ChainId,
                Gas = ethDeposit?.TxRequest?.Gas,
                GasPrice = ethDeposit?.TxRequest?.GasPrice,
                MaxFeePerGas = parameters?.Overrides?.MaxFeePerGas.Value.ToHexBigInteger(),
                MaxPriorityFeePerGas = parameters?.Overrides?.MaxPriorityFeePerGas.Value.ToHexBigInteger(),
                Nonce = ethDeposit?.TxRequest?.Nonce,
                Type = ethDeposit?.TxRequest?.Type
            };

            // If 'From' field is null, set it to L1Signer's address
            if (tx.From == null)
            {
                tx.From = l1Signer?.Account.Address;
            }

            // Retrieve the current nonce if not done automatically
            if (tx.Nonce == null)
            {
                var nonce = await provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(tx.From);

                tx.Nonce = 1.ToHexBigInteger();
            }

            //estimate gas for the transaction if not done automatically
            tx.Gas ??= await provider.Eth.TransactionManager.EstimateGasAsync(tx);

            var (contractAbi, contractByteCode) = await LogParser.LoadAbi("TestDepositEth", false);

            var gas = await provider.Eth.DeployContract.EstimateGasAsync(abi: contractAbi, contractByteCode: contractByteCode, from: l1Signer.Account.Address);

            // Send transaction
            var transactionHash = await provider.Eth.DeployContract.SendRequestAsync(contractByteCode, l1Signer.Account.Address, gas, null);

            // Wait for transaction receipt
            TransactionReceipt receipt = await provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

            var contract = provider.Eth.GetContract(contractAbi, receipt.ContractAddress);

            var function = contract.GetFunction("test_depositEth_FromContract");

            var sendMessageReceipt = await function.SendTransactionAndWaitForReceiptAsync(tx.From, new HexBigInteger(300000), null);

            // Return transaction receipt
            return L1TransactionReceipt.MonkeyPatchEthDepositWait(sendMessageReceipt);
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
            //await CheckL1Network(parameters.L1Signer);
            //await CheckL2Network(parameters.L2Provider);

            // Assuming we have an interface and helper methods for type checking
            dynamic retryableTicketRequest;

            if (TransactionUtils.IsL1ToL2TransactionRequest(parameters))
            {
                retryableTicketRequest = parameters;
            }
            else if (parameters is EthDepositToParams)
            {
                retryableTicketRequest = await GetDepositToRequest(new EthDepositToRequestParams
                {
                    From = parameters?.L1Signer?.Account.Address ?? null,
                    L1Provider = parameters?.L1Signer?.Provider ?? null,
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
                    From = parameters?.L1Signer?.Account.Address ?? null,
                    L1Provider = parameters?.L1Signer?.Provider ?? null,
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
                MaxFeePerGas = parameters?.Overrides?.MaxFeePerGas.ToHexBigInteger(),
                MaxPriorityFeePerGas = parameters?.Overrides?.MaxPriorityFeePerGas.ToHexBigInteger(),
                Nonce = retryableTicketRequest?.TxRequest?.Nonce,
                Type = retryableTicketRequest?.TxRequest?.Type,
            };

            if (tx.From == null)
            {
                tx.From = parameters?.L1Signer?.Address;
            }

            // Retrieve the current nonce
            if (tx.Nonce == null)
            {
                var nonce = await parameters.L1Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(tx.From);
                tx.Nonce = nonce;
            }

            if (tx.Gas == null)
            {
                var gas = await parameters.L1Signer.Provider.Eth.TransactionManager.EstimateGasAsync(tx);

                tx.Gas = gas;
            }

            //sign transaction
            var signedTx = await parameters.L1Signer.Account.TransactionManager.SignTransactionAsync(tx);

            //send transaction
            var txnHash = await parameters.L1Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

            // Get transaction receipt
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
            //await CheckL2Network(ethParams?.L2Signer);

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
                ChainId = new HexBigInteger(31337),//request?.TxRequest?.ChainId,
                Gas = request?.TxRequest?.Gas,
                GasPrice = request?.TxRequest?.GasPrice,
                MaxFeePerGas = ethParams?.Overrides?.MaxFeePerGas.Value.ToHexBigInteger(),
                MaxPriorityFeePerGas = ethParams?.Overrides?.MaxPriorityFeePerGas.Value.ToHexBigInteger(),
                Nonce = request?.TxRequest?.Nonce,
                Type = request?.TxRequest?.Type,
            };

            //check if from is null
            if (tx.From == null)
            {
                tx.From = ethParams?.L2Signer?.Account.Address;
            }

            // Retrieve the current nonce
            if (tx.Nonce == null)
            {
                var nonce = await ethParams.L2Signer.Provider.Eth.Transactions.GetTransactionCount.SendRequestAsync(tx.From);
                tx.Nonce = nonce;
            }

            //estimate gas for the transaction
            if (tx.Gas == null)
            {
                var gas = await ethParams.L2Signer.Provider.Eth.TransactionManager.EstimateGasAsync(tx);

                tx.Gas = gas;
            }

            //sign transaction
            var signedTx = await ethParams.L2Signer.Account.TransactionManager.SignTransactionAsync(tx);

            //send transaction
            var txnHash = await ethParams.L2Signer.Provider.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTx);

            // Get transaction receipt
            var receipt = await ethParams.L2Signer.Provider.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);

            return L2TransactionReceipt.MonkeyPatchWait(receipt);
        }
    }
}
