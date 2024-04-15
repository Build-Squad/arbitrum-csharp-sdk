using Arbitrum.DataEntities;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CodeTranslationAssistant
{
    /**
     * Input to multicall aggregator
     */
    public class CallInput<T>
    {
        /**
         * Address of the target contract to be called
         */
        public string targetAddr { get; set; }
        /**
         * Function to produce encoded call data
         */
        public Func<string> encoder { get; set; }
        /**
         * Function to decode the result of the call
         */
        public Func<string, T> decoder { get; set; }
    }

    /**
     * For each item in T this DecoderReturnType<T> yields the return
     * type of the decoder property.
     * If we require success then the result cannot be undefined
     */
    public class DecoderReturnType<T, TRequireSuccess>
    {
        public List<T> result { get; set; }
    }

    ///////////////////////////////////////
    /////// TOKEN CONDITIONAL TYPES ///////
    ///////////////////////////////////////
    // these conditional types return check T, and if it matches
    // the input type then they return a known output type
    public class AllowanceInputOutput<T>
    {
        public BigInteger? allowance { get; set; }
    }
    public class BalanceInputOutput<T>
    {
        public BigInteger? balance { get; set; }
    }
    public class DecimalsInputOutput<T>
    {
        public int? decimals { get; set; }
    }
    public class NameInputOutput<T>
    {
        public string? name { get; set; }
    }
    public class SymbolInputOutput<T>
    {
        public string? symbol { get; set; }
    }
    public class TokenMultiInput
    {
        public BalanceOf? balanceOf { get; set; }
        public Allowance? allowance { get; set; }
        public bool? symbol { get; set; }
        public bool? decimals { get; set; }
        public bool? name { get; set; }
    }
    // if we were given options at all then we convert
    // those options to outputs
    public class TokenInputOutput<T>
    {
        public AllowanceInputOutput<T> allowance { get; set; }
        public BalanceInputOutput<T> balance { get; set; }
        public DecimalsInputOutput<T> decimals { get; set; }
        public NameInputOutput<T> name { get; set; }
        public SymbolInputOutput<T> symbol { get; set; }
    }
    //\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
    //\\\\\ TOKEN CONDITIONAL TYPES \\\\\\\
    //\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

    /**
     * Util for executing multi calls against the MultiCallV2 contract
     */
    public class MultiCaller
    {
        private readonly Web3 provider;
        /**
         * Address of multicall contract
         */
        public string address { get; set; }

        public MultiCaller(Web3 provider, string address)
        {
            this.provider = provider;
            this.address = address;
        }

        /**
         * Finds the correct multicall address for the given provider and instantiates a multicaller
         * @param provider
         * @returns
         */
        public static async Task<MultiCaller> fromProvider(Web3 provider)
        {
            var chainId = (await provider.getNetwork()).chainId;
            var l2Network = l2Networks[chainId] as L2Network;
            var l1Network = l1Networks[chainId] as L1Network;

            var network = l2Network ?? l1Network;
            if (network == null)
            {
                throw new ArbSdkError(
                    $"Unexpected network id: {chainId}. Ensure that chain {chainId} has been added as a network."
                );
            }

            string multiCallAddr;
            if (isL1Network(network))
            {
                var firstL2 = l2Networks[network.partnerChainIDs[0]];
                if (firstL2 == null)
                    throw new ArbSdkError(
                        $"No partner chain found l1 network: {network.chainID} : partner chain ids {network.partnerChainIDs}"
                    );
                multiCallAddr = firstL2.tokenBridge.l1MultiCall;
            }
            else
            {
                multiCallAddr = network.tokenBridge.l2Multicall;
            }

            return new MultiCaller(provider, multiCallAddr);
        }

        /**
         * Get the call input for the current block number
         * @returns
         */
        public CallInput<Task<BigInteger>> getBlockNumberInput()
        {
            var iFace = Multicall2__factory.createInterface();
            return new CallInput<Task<BigInteger>>()
            {
                targetAddr = this.address,
                encoder = () => iFace.encodeFunctionData("getBlockNumber"),
                decoder = (returnData) => iFace.decodeFunctionResult("getBlockNumber", returnData)[0]
            };
        }

        /**
         * Get the call input for the current block timestamp
         * @returns
         */
        public CallInput<Task<BigInteger>> getCurrentBlockTimestampInput()
        {
            var iFace = Multicall2__factory.createInterface();
            return new CallInput<Task<BigInteger>>()
            {
                targetAddr = this.address,
                encoder = () => iFace.encodeFunctionData("getCurrentBlockTimestamp"),
                decoder = (returnData) => iFace.decodeFunctionResult("getCurrentBlockTimestamp", returnData)[0]
            };
        }

        /**
         * Executes a multicall for the given parameters
         * Return values are order the same as the inputs.
         * If a call failed undefined is returned instead of the value.
         *
         * To get better type inference when the individual calls are of different types
         * create your inputs as a tuple and pass the tuple in. The return type will be
         * a tuple of the decoded return types. eg.
         *
         *
         *typescript
         *   const inputs: [
         *     CallInput<Awaited<ReturnType<ERC20['functions']['balanceOf']>>[0]>,
         *     CallInput<Awaited<ReturnType<ERC20['functions']['name']>>[0]>
         *   ] = [
         *     {
         *       targetAddr: token.address,
         *       encoder: () => token.interface.encodeFunctionData('balanceOf', ['']),
         *       decoder: (returnData: string) =>
         *         token.interface.decodeFunctionResult('balanceOf', returnData)[0],
         *     },
         *     {
         *       targetAddr: token.address,
         *       encoder: () => token.interface.encodeFunctionData('name'),
         *       decoder: (returnData: string) =>
         *         token.interface.decodeFunctionResult('name', returnData)[0],
         *     },
         *   ]
         *
         *   const res = await multiCaller.call(inputs)
         *         * @param provider
         * @param params
         * @param requireSuccess Fail the whole call if any internal call fails
         * @returns
         */
        public async Task<DecoderReturnType<T, TRequireSuccess>> multiCall<T, TRequireSuccess>(
            T[] parameters,
            TRequireSuccess requireSuccess = default(TRequireSuccess)
        )
        {
            var defaultedRequireSuccess = requireSuccess ?? false;
            var multiCall = Multicall2__factory.connect(this.address, this.provider);
            var args = parameters.Select(p => new
            {
                target = p.targetAddr,
                callData = p.encoder()
            }).ToList();

            var outputs = await multiCall.callStatic.tryAggregate(
                defaultedRequireSuccess,
                args
            );

            return new DecoderReturnType<T, TRequireSuccess>()
            {
                result = outputs.Select((output, index) =>
                {
                    if (output.success && output.returnData != null && output.returnData != "0x")
                    {
                        return parameters[index].decoder(output.returnData);
                    }
                    return parameters(T);
                }).ToList()
            };
        }

        /**
         * Multicall for token properties. Will collect all the requested properies for each of the
         * supplied token addresses.
         * @param erc20Addresses
         * @param options Defaults to just 'name'
         * @returns
         */
        public async Task<List<TokenInputOutput<T>>> getTokenData<T>(
            string[] erc20Addresses,
            T options = default(T)
        )
        {
            // if no options are supplied, then we just multicall for the names
            var defaultedOptions = options ?? new TokenMultiInput() { name = true };
            var erc20Iface = ERC20__factory.createInterface();

            bool isBytes32(string data) =>
                utils.isHexString(data) && utils.hexDataLength(data) == 32;

            var input = new List<CallInput<T>>();
            foreach (var t in erc20Addresses)
            {
                if (defaultedOptions.allowance != null)
                {
                    input.Add(new CallInput<T>()
                    {
                        targetAddr = t,
                        encoder = () =>
                            erc20Iface.encodeFunctionData("allowance", new object[]
                            {
                                defaultedOptions.allowance.owner,
                                defaultedOptions.allowance.spender
                            }),
                        decoder = (returnData) =>
                            erc20Iface.decodeFunctionResult(
                                "allowance",
                                returnData
                            )[0] as BigInteger
                    });
                }

                if (defaultedOptions.balanceOf != null)
                {
                    input.Add(new CallInput<T>()
                    {
                        targetAddr = t,
                        encoder = () =>
                            erc20Iface.encodeFunctionData("balanceOf", new object[]
                            {
                                defaultedOptions.balanceOf.account
                            }),
                        decoder = (returnData) =>
                            erc20Iface.decodeFunctionResult(
                                "balanceOf",
                                returnData
                            )[0] as BigInteger
                    });
                }

                if (defaultedOptions.decimals != null)
                {
                    input.Add(new CallInput<T>()
                    {
                        targetAddr = t,
                        encoder = () => erc20Iface.encodeFunctionData("decimals"),
                        decoder = (returnData) =>
                            erc20Iface.decodeFunctionResult(
                                "decimals",
                                returnData
                            )[0] as int
                    });
                }

                if (defaultedOptions.name != null)
                {
                    input.Add(new CallInput<T>()
                    {
                        targetAddr = t,
                        encoder = () => erc20Iface.encodeFunctionData("name"),
                        decoder = (returnData) =>
                        {
                            // Maker doesn't follow the erc20 spec and returns bytes32 data.
                            // https://etherscan.io/token/0x9f8F72aA9304c8B593d555F12eF6589cC3A579A2#readContract
                            if (isBytes32(returnData))
                            {
                                return utils.parseBytes32String(returnData) as string;
                            }
                            else
                            {
                                return erc20Iface.decodeFunctionResult(
                                    "name",
                                    returnData
                                )[0] as string;
                            }
                        }
                    });
                }

                if (defaultedOptions.symbol != null)
                {
                    input.Add(new CallInput<T>()
                    {
                        targetAddr = t,
                        encoder = () => erc20Iface.encodeFunctionData("symbol"),
                        decoder = (returnData) =>
                        {
                            // Maker doesn't follow the erc20 spec and returns bytes32 data.
                            // https://etherscan.io/token/0x9f8F72aA9304c8B593d555F12eF6589cC3A579A2#readContract
                            if (isBytes32(returnData))
                            {
                                return utils.parseBytes32String(returnData) as string;
                            }
                            else
                            {
                                return erc20Iface.decodeFunctionResult(
                                    "symbol",
                                    returnData
                                )[0] as string;
                            }
                        }
                    });
                }
            }

            var res = await this.multiCall(input);

            var i = 0;
            var tokens = new List<TokenInputOutput<T>>();
            while (i < res.result.Count)
            {
                tokens.Add(new TokenInputOutput<T>()
                {
                    allowance = defaultedOptions.allowance != null ? res.result[i++] as BigInteger : null,
                    balance = defaultedOptions.balanceOf != null ? res.result[i++] as BigInteger : null,
                    decimals = defaultedOptions.decimals != null ? res.result[i++] as int : null,
                    name = defaultedOptions.name != null ? res.result[i++] as string : null,
                    symbol = defaultedOptions.symbol != null ? res.result[i++] as string : null
                });
            }
            return tokens;
        }
    }
}


