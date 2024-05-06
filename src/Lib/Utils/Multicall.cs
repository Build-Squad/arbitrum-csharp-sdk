//using Arbitrum.DataEntities;
//using Nethereum.Contracts;
//using Nethereum.ABI.FunctionEncoding;
//using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
//using Nethereum.Contracts.QueryHandlers.MultiCall;
//using System.Numerics;
//using Nethereum.Web3;
//using Nethereum.RLP;
//using Nethereum.Contracts.Standards.ENS.ETHRegistrarController.ContractDefinition;
//using Nethereum.ABI.FunctionEncoding.Attributes;
//using System.Text.Unicode;
//using Nethereum.ABI;
//using System.Text;
//using static System.Runtime.InteropServices.JavaScript.JSType;

//namespace Arbitrum.Utils
//{
//    public class NewCallInput<T>
//    {
//        /// <summary>
//        /// Address of the target contract to be called.
//        /// </summary>
//        public string? TargetAddr { get; set; }

//        /// <summary>
//        /// Function to produce encoded call data.
//        /// </summary>
//        public Func<byte[]>? Encoder { get; set; }

//        /// <summary>
//        /// Function to decode the result of the call.
//        /// </summary>
//        public Func<string, T>? Decoder { get; set; }
//    }
//    public class DecoderReturnType<T, TRequireSuccess>
//    {
//        public Dictionary<string, Type> Types { get; private set; }

//        public DecoderReturnType()
//        {
//            Types = new Dictionary<string, Type>();
//        }

//        public void AddDecoderType<K>(string key)
//        {
//            var type = typeof(K);
//            Types[key] = type;
//        }
//    }
//    public class AllowanceInputOutput<T>
//    {
//        public BigInteger Allowance { get; set; }
//    }

//    public class BalanceInputOutput<T>
//    {
//        public BigInteger Balance { get; set; }
//    }

//    public class DecimalsInputOutput<T>
//    {
//        public int Decimals { get; set; }
//    }

//    public class NameInputOutput<T>
//    {
//        public string? Name { get; set; }
//    }

//    public class SymbolInputOutput<T>
//    {
//        public string? Symbol { get; set; }
//    }

//    public class AllowanceType
//    {
//        public string? Owner { get; set; }
//        public string? Spender { get; set; }
//    }

//    public class BalanceOfType
//    {
//        public string? Account { get; set; }
//    }
//    public class TokenMultiInput
//    {
//        //All types here taken from Nethereum.Contracts.Standards.ERC20.ContractDefinition
//        public BalanceOfType? BalanceOf { get; set; }
         
//        public AllowanceType? Allowance { get; set; }
//        public bool? Symbol { get; set; } = true;
//        public bool? Decimals { get; set; } = true;
//        public bool? Name { get; set; } = true;
//    }

//    class MultiCaller
//    {
//        public readonly Web3 provider;
//        public string address;
//        public MultiCaller(Web3 provider, string address)
//        {
//            if (provider != null)
//            {
//                this.provider = provider;
//            }
//            else
//            {
//                throw new ArbSdkError("Invalid provider type");
//            }

//            this.address = address;
//        }

//        public static async Task<MultiCaller> FromProvider(Web3 provider)
//        {
//            int chainId = (int)(await provider.Eth.ChainId.SendRequestAsync()).Value;

//            var l2Network = NetworkUtils.l2Networks.ContainsKey(chainId) ? NetworkUtils.l2Networks[chainId] : null;
//            var l1Network = NetworkUtils.l1Networks.ContainsKey(chainId) ? NetworkUtils.l1Networks[chainId] : null;

//            dynamic network;
//            if(l2Network != null)
//            {
//                network = l2Network;
//            }
//            else
//            {
//                network = l1Network;
//            }

//            if (network == null)
//            {
//                throw new ArbSdkError($"Unexpected network id: {chainId}. Ensure that chain {chainId} has been added as a network.");
//            }

//            string multiCallAddr;

//            if (NetworkUtils.IsL1Network(network))
//            {
//                var firstL2 = NetworkUtils.l2Networks[network?.PartnerChainIDs[0]];
//                if (firstL2 == null)
//                {
//                    throw new Exception($"No partner chain found for L1 network: {network?.ChainID}. Partner chain IDs: {l1Network?.PartnerChainIDs}");
//                }

//                multiCallAddr = firstL2?.TokenBridge?.L1MultiCall;
//            }
//            else
//            {
//                multiCallAddr = network?.TokenBridge?.L2MultiCall;
//            }

//            return new MultiCaller(provider, multiCallAddr);
//        }

//        public async Task<Function> GetFunction(string contractName, string functionName)
//        {
//            var contract = await LoadContractUtils.LoadContract(contractName, provider, address);
//            return contract.GetFunction(functionName);
//        }

//        public async Task<NewCallInput<ParameterOutput>> GetBlockNumberInput()
//        {
//            var iFace = await LoadContractUtils.LoadContract(
//                                            provider: provider,
//                                            contractName: "Multicall2",
//                                            address: address,
//                                            isClassic: true
//                                            );

//            ABIEncode encoder = new ABIEncode();

//            return new NewCallInput<ParameterOutput>
//            {
//                TargetAddr = address,

//                Encoder = () =>
//                {
//                    var functionAbi = iFace.ContractBuilder.GetFunctionAbi("getBlockNumber");
//                    //var function = iFace.GetFunction(functionAbi.Name);
//                    return encoder.GetABIEncoded(functionAbi);
//                },
//                Decoder = (returnData) =>
//                {
//                    // Use the interface (iFace) to decode the return data of 'getBlockNumber'
//                    var decodedData = iFace.GetFunction("getBlockNumber").DecodeInput(returnData);

//                    return decodedData.FirstOrDefault()!; // Return the first result
//                }
//            };
//        }

//        public async Task<NewCallInput<ParameterOutput>> GetCurrentBlockTimestampInput()
//        {
//            var iFace = await LoadContractUtils.LoadContract(
//                                provider: provider,
//                                contractName: "Multicall2",
//                                address: address,
//                                isClassic: true
//                                );
//            ABIEncode encoder = new ABIEncode();

//            return new NewCallInput<ParameterOutput>
//            {
//                TargetAddr = address,

//                Encoder = () =>
//                {
//                    var functionAbi = iFace.ContractBuilder.GetFunctionAbi("getCurrentBlockTimestamp");
//                    //var function = iFace.GetFunction(functionAbi.Name);
//                    return encoder.GetABIEncoded(functionAbi);
//                },

//                Decoder = (returnData) =>
//                {
//                    // Use the interface (iFace) to decode the return data of 'getCurrentBlockTimestamp'
//                    var decodedData = iFace.GetFunction("getCurrentBlockTimestamp").DecodeInput(returnData);

//                    return decodedData.FirstOrDefault(); // Return the first result
//                }
//            };
//        }



//        public async Task<List<T>> MultiCall<T, TRequireSuccess>
//            (List<T> parameters, TRequireSuccess requireSuccess = default)
//            where T : NewCallInput<ParameterOutput>
//            where TRequireSuccess : struct, IConvertible
//        {
//            var multiCallContract = await LoadContractUtils.LoadContract(
//                provider: provider,
//                contractName: "Multicall2",
//                address: address,
//                isClassic: true
//            );

//            //await new MultiQueryHandler(provider.Client).MultiCallAsync();

//            var callInput = multiCallContract.GetFunction("tryAggregate").(parameters, requireSuccess);


//            var args = parameters.Select(p => new
//            {
//                target = p.TargetAddr,
//                callData = p.Encoder()
//            }).ToList();

//            var outputs = await multiCallContract.GetFunction("tryAggregate").CallAsync<List<Result>>(requireSuccess, args);

//            return outputs.Select((output, index) =>
//            {
//                if (output.Success && output.ReturnData != null && output.ReturnData.ToString() != "0x")
//                {
//                    return parameters[index].Decoder(output.ReturnData);
//                }
//                return default;
//            }).ToList() as DecoderReturnType<NewCallInput<ParameterOutput>, TRequireSuccess>;
//        }

//        //public async Task<DecoderReturnType<NewCallInput<dynamic>, TRequireSuccess>> MultiCall<T, TRequireSuccess>(dynamic[] parameters, bool requireSuccess = false)
//        //{
//        //    var defaultedRequireSuccess = requireSuccess;
//        //    var multiCall = await LoadContractUtils.LoadContract(
//        //                                                        provider: provider,
//        //                                                        contractName: "Multicall2",
//        //                                                        address: address,
//        //                                                        isClassic: true
//        //                                                        );
//        //    var args = parameters.Select(p => new
//        //    {
//        //        target = p.targetAddr,
//        //        callData = p.encoder()
//        //    }).ToList();

//        //    var multiCallContractFunction = multiCall.GetFunction("tryAggregate");

//        //    var outputs = await multiCallContractFunction.CallAsync<List<Result>>(requireSuccess, args);


//        //    return outputs.Select((output, index) =>
//        //    {
//        //        if (output.Success && output.ReturnData != null && output.ReturnData.ToString() != "0x")
//        //        {
//        //            return parameters[index].decoder(output.ReturnData);
//        //        }
//        //        return default;
//        //    }).ToList() as DecoderReturnType<NewCallInput<dynamic>, TRequireSuccess>;
//        //}

////    public async Task<List<ParameterOutput>> MultiCall(
////            List<NewCallInput<ParameterOutput>> parameters,
////            bool requireSuccess = default
////        )
////        {
////            // Load the Multicall2 contract
////            var multiCallContract = await LoadContractUtils.LoadContract(
////                                                                provider: provider,
////                                                                contractName: "Multicall2",
////                                                                address: address,
////                                                                isClassic: true
////                                                                );

////            // Prepare the arguments for the tryAggregate function
////            var args = parameters.Select(p => new Call3
////            {
////                Target = p.TargetAddr,
////                CallData = p.Encoder()
////=            }).ToList();

////            var multiCallContractFunction = multiCallContract.GetFunction("tryAggregate");

////            var outputs = await multiCallContractFunction.CallAsync<List<Result>>(requireSuccess, args);

////            // Create a list to store the results
////            var resultsList = new List<ParameterOutput>();

////            // Process the outputs
////            for (int i = 0; i < outputs.Count(); i++)
////            {
////                var output = outputs[i];
////                var parameter = parameters[i];

////                // Check if the output is successful
////                if (output.Success)
////                {
////                    // Decode the return data using the decoder from the parameter
////                    var decodedResult = parameter.Decoder?.Invoke(output.ReturnData);
////                    resultsList.Add(decodedResult);
////                }
////                else
////                {
////                    // If the output is not successful, add null to the list
////                    resultsList.Add(null);
////                }
////            }

////            return resultsList;
////        }

//        public async Task<List<TokenMultiInput>> GetTokenData<T>(string[] erc20Addresses, T? options = default)
//            where T : TokenMultiInput
//        {
//            // If no options are supplied, default to fetching the names
//            // Use options, or create new instance if null
//            var defaultedOptions = options;   ////
//            var erc20Iface = await LoadContractUtils.LoadContract(
//                                                    provider: provider,
//                                                    contractName: "ERC20",
//                                                    isClassic: true
//                                                    );

//            // Prepare input list for multicall
//            var inputs = new List<NewCallInput<ParameterOutput>>();

//            // Populate the input list based on options
//            foreach (var address in erc20Addresses)
//            {
//                if (defaultedOptions?.BalanceOf != null)
//                {
//                    var account = defaultedOptions?.BalanceOf?.Account;
//                    inputs.Add(CreateCallInput(address, erc20Iface, "balanceOf", new object[] { account }));
//                }

//                if (defaultedOptions?.Allowance != null)
//                {
//                    var owner = defaultedOptions.Allowance.Owner;
//                    var spender = defaultedOptions.Allowance.Spender;
//                    inputs.Add(CreateCallInput(address, erc20Iface, "allowance", new object[] { owner, spender }));
//                }

//                if (defaultedOptions?.Symbol != null)
//                {
//                    inputs.Add(CreateCallInput(address, erc20Iface, "symbol", null));
//                }

//                if (defaultedOptions?.Decimals != null)
//                {
//                    inputs.Add(CreateCallInput(address, erc20Iface, "decimals", null));
//                }

//                if (defaultedOptions?.Name != null)
//                {
//                    inputs.Add(CreateCallInput(address, erc20Iface, "name", null));
//                }

//                // Perform multi-call and process results
//                var results = await MultiCall(inputs, true);

//                var tokens = new List<TokenMultiInput>();
//                int i = 0;

//                while (i < results.Count())
//                {

//                    var tokenInfo = new TokenMultiInput();

//                    if (defaultedOptions?.BalanceOf != null)
//                    {
//                        tokenInfo.BalanceOf = results[i++];
//                    }

//                    if (defaultedOptions?.Allowance != null)
//                    {
//                        tokenInfo.Allowance = results[i++];
//                    }

//                    if (defaultedOptions?.Symbol != null)
//                    {
//                        tokenInfo.Symbol = results[i++];
//                    }

//                    var token = new TokenMultiInput
//                    {
//                        Allowance = defaultedOptions?.Allowance,
//                        BalanceOf = defaultedOptions?.BalanceOf,
//                        Decimals = defaultedOptions?.Decimals,
//                        Name = defaultedOptions?.Name,
//                        Symbol = defaultedOptions?.Symbol
//                    };
//                    tokens.Add(token);
//                }

//                return tokens;
//            }
//        }

//        public NewCallInput<ParameterOutput> CreateCallInput(string address, Contract contract, string methodName, object[]? args = null)
//        {
//            return new NewCallInput<ParameterOutput>
//            {
//                TargetAddr = address,
//                Encoder = () =>
//                {
//                    var functionAbi = contract.ContractBuilder.GetFunctionAbi(methodName);
//                    var function = contract.GetFunction(functionAbi.Name);
//                    return function.GetData(args).ToBytesForRLPEncoding();
//                },

//                Decoder = (returnData) =>
//                {

//                    // Use the contract to decode the return data of the method

//                    var decodedData = contract.GetFunction("getCurrentBlockTimestamp").DecodeInput(returnData.ToString());

//                    return decodedData.FirstOrDefault()!; // Return the first result
//                }
//            };
//        }
//    }
//}
