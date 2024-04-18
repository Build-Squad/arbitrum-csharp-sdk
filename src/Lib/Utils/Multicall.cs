//using Arbitrum.DataEntities;
//using Nethereum.Contracts;
//using Nethereum.ABI.FunctionEncoding;
//using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
//using Nethereum.Contracts.QueryHandlers.MultiCall;
//using System.Numerics;
//using Nethereum.Web3;
//using Nethereum.RLP;

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
//        public Func<byte[], T>? Decoder { get; set; }
//    }


//    public class TokenMultiInput
//    {
//        //All types here taken from Nethereum.Contracts.Standards.ERC20.ContractDefinition
//        public BalanceOfFunction? BalanceOf { get; set; }
//        public AllowanceFunction? Allowance { get; set; }
//        public SymbolFunction? Symbol { get; set; }
//        public DecimalsFunction? Decimals { get; set; }
//        public NameFunction? Name { get; set; }
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

//            var network = l2Network != null ? l2Network : l1Network;

//            if (network == null)
//            {
//                throw new ArbSdkError($"Unexpected network id: {chainId}. Ensure that chain {chainId} has been added as a network.");
//            }

//            string multiCallAddr;

//            if (NetworkUtils.IsL1Network(network))
//            {
//                var firstL2 = NetworkUtils.l2Networks[network.PartnerChainIDs![0]];
//                if (firstL2 == null)
//                {
//                    throw new Exception($"No partner chain found for L1 network: {network.ChainID}. Partner chain IDs: {l1Network?.PartnerChainIDs}");
//                }

//                multiCallAddr = firstL2.TokenBridge.L1MultiCall;
//            }
//            else
//            {
//                multiCallAddr = network.TokenBridge.L2MultiCall;
//            }

//            return new MultiCaller(provider, multiCallAddr);
//        }

//        public async Task<Function> GetFunction(string contractName, string functionName)
//        {
//            var contract = await LoadContractUtils.LoadContract(contractName, provider, address);
//            return contract.GetFunction(functionName);
//        }

//        public async Task<NewCallInput<ParameterOutput>> getBlockNumberInput()
//        {
//            var iFace = await LoadContractUtils.LoadContract(
//                                            provider: provider,
//                                            contractName: "Multicall2",
//                                            address: address,
//                                            isClassic: true
//                                            );

//            return new NewCallInput<ParameterOutput>
//            {
//                TargetAddr = address,

//                Encoder = () =>
//                {
//                    var functionAbi = iFace.ContractBuilder.GetFunctionAbi("getBlockNumber");

//                    var function = iFace.GetFunction(functionAbi.Name);

//                    return function.GetData();
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

//            return new NewCallInput<ParameterOutput>
//            {
//                TargetAddr = address,

//                Encoder = () =>
//                {
//                    var functionAbi = iFace.ContractBuilder.GetFunctionAbi("getCurrentBlockTimestamp");
//                    var function = iFace.GetFunction(functionAbi.Name);
//                    return function.GetData();
//                },

//                Decoder = (returnData) =>
//                {

//                    // Use the interface (iFace) to decode the return data of 'getCurrentBlockTimestamp'

//                    var decodedData = iFace.GetFunction("getCurrentBlockTimestamp").DecodeInput(returnData);

//                    return decodedData.FirstOrDefault()!; // Return the first result
//                }
//            };
//        }

//        public async Task<List<ParameterOutput>> MultiCall(
//            List<NewCallInput<ParameterOutput>> parameters,
//            bool requireSuccess = default
//        )
//        {
//            // Load the Multicall2 contract
//            var multiCallContract = await LoadContractUtils.LoadContract(
//                                                                provider: provider,
//                                                                contractName: "Multicall2",
//                                                                address: address,
//                                                                isClassic: true
//                                                                );

//            // Prepare the arguments for the tryAggregate function
//            var args = parameters.Select(p => new Call3
//            {
//                Target = p.TargetAddr,
//                CallData = p.Encoder()
//            }).ToList();

//            var multiCallContractFunction = multiCallContract.GetFunction("tryAggregate");

//            var outputs = await multiCallContractFunction.CallAsync<List<Result>>(requireSuccess, args);

//            var a = outputs
//            // Create a list to store the results
//            var resultsList = new List<ParameterOutput>();

//            // Process the outputs
//            for (int i = 0; i < outputs.Count(); i++)
//            {
//                var output = outputs[i];
//                var parameter = parameters[i];

//                // Check if the output is successful
//                if (output.Success)
//                {
//                    // Decode the return data using the decoder from the parameter
//                    var decodedResult = parameter.Decoder?.Invoke(output.ReturnData);
//                    resultsList.Add(decodedResult!);
//                }
//                else
//                {
//                    // If the output is not successful, add null to the list
//                    resultsList.Add(null!);
//                }
//            }

//            return resultsList;
//        }

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
//                    inputs.Add(CreateCallInput(address, erc20Iface, "balanceOf", new object[] { account! }));
//                }

//                if (defaultedOptions?.Allowance != null)
//                {
//                    var owner = defaultedOptions.Allowance.Owner;
//                    var spender = defaultedOptions.Allowance.Spender;
//                    inputs.Add(CreateCallInput(address, erc20Iface, "allowance", new object[] { owner!, spender! }));
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
//                var results = await MultiCall(inputs);

//                var tokens = new List<TokenMultiInput>();
//                int i = 0;

//                while (i < results.Count())
//                {

//                    var tokenInfo = new TokenMultiInput();

//                    if (defaultedOptions?.BalanceOf != null)
//                    {
//                        tokenInfo.BalanceOf = (BalanceOfFunction)results[i++];
//                    }

//                    if (defaultedOptions?.Allowance != null)
//                    {
//                        tokenInfo.Allowance = (AllowanceFunction)results[i++];
//                    }

//                    if (defaultedOptions?.Symbol != null)
//                    {
//                        tokenInfo.Symbol = (SymbolFunction)results[i++];
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
