using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Arbitrum.Utils;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;
using Arbitrum.DataEntities;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Eth.Services;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;


namespace Arbitrum.Utils
{
    public class FetchedEvent<TEventArgs>
    {
        public TEventArgs Event { get; set; }
        public string Topic { get; set; }
        public string Name { get; set; }
        public int BlockNumber { get; set; }
        public string BlockHash { get; set; }
        public string TransactionHash { get; set; }
        public string Address { get; set; }
        public List<string> Topics { get; set; }
        public string Data { get; set; }

        public FetchedEvent(
            TEventArgs eventArgs,
            string topic,
            string name,
            int blockNumber,
            string blockHash,
            string transactionHash,
            string address,
            List<string> topics,
            string data)
        {
            Event = eventArgs;
            Topic = topic;
            Name = name;
            BlockNumber = blockNumber;
            BlockHash = blockHash;
            TransactionHash = transactionHash;
            Address = address;
            Topics = topics;
            Data = data;
        }
    }

    public class EventFetcher
    {
        public readonly Web3 _provider;

        public EventFetcher(object provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (provider is Web3 web3Provider)
            {
                _provider = web3Provider;
            }
            else if (provider is SignerOrProvider signerOrProvider)
            {
                _provider = signerOrProvider.Provider!;
            }
            else if (provider is ArbitrumProvider arbitrumProvider)
            {
                _provider = arbitrumProvider.Provider;
            }
            else
            {
                throw new ArgumentException("Invalid provider type", nameof(provider));
            }
        }
        //generic FetchedEvent type(to be tested)
        public async Task<List<FetchedEvent<TEventArgs>>> GetEventsAsync<TEventArgs>(
            dynamic contractFactory,
            string eventName,
            Dictionary<string, object>? argumentFilters = null,
            NewFilterInput? filter = null,
            bool isClassic = false)
        {

            // Initialize filter and argumentFilters if they are null
            if (filter == null)
                filter = new NewFilterInput();

            if (argumentFilters == null)
                argumentFilters = new Dictionary<string, object>();

            // Get the contract instance
            Contract contract;

            // If the contract factory is a string (indicating the contract address or name),
            // get the contract address from the filter dictionary.
            // If the filter contains an "address" key, use its value as the contract address.
            // Otherwise, use a default address.
            if (contractFactory is string)
            {
                string contractAddress = LoadContractUtils.GetAddress(!string.IsNullOrEmpty(filter?.Address?.FirstOrDefault())
                    ? filter?.Address?.FirstOrDefault()
                    : "0x0000000000000000000000000000000000000000");


                contract = await LoadContractUtils.LoadContract(
                    provider: _provider,
                    contractName: contractFactory,
                    address: contractAddress,
                    isClassic: isClassic
                );
            }
            else if (contractFactory is Contract contractInstance)
            {
                contract = contractInstance;
            }
            else
            {
                throw new ArbSdkError("Invalid contract factory type");
            }

            // Get the event instance
            var eventInstance = contract.GetEvent(eventName);

            if (eventInstance == null)
                throw new ArgumentException($"Event {eventName} not found in contract");

            // Create event filter
            BlockParameter fromBlock = new BlockParameter(new HexBigInteger(BigInteger.Zero));
            BlockParameter toBlock = new BlockParameter(new HexBigInteger(BigInteger.Zero));

            if (filter is NewFilterInput filterDict)
            {
                if (filterDict.FromBlock!=null)
                {
                    fromBlock = filterDict.FromBlock;
                }

                if (filterDict.ToBlock !=null)
                {
                    toBlock = filterDict.ToBlock;
                }
            }

            // Create event filter
            var eventFilter = new NewFilterInput
            {
                FromBlock = fromBlock,
                ToBlock = toBlock,
                Address = new[] { contract.Address },
                // Merge filter and argumentFilters
                Topics = MergeTopics(filter!, argumentFilters)
            };

            var logs = await _provider.Eth.Filters.GetLogs.SendRequestAsync(eventFilter);

            var fetchedEvents = new List<FetchedEvent<TEventArgs>>();
            int logCount = 0;

            foreach (var log in logs)
            {
                fetchedEvents.Add(new FetchedEvent<TEventArgs>(
                    //Since the generic type parameter TEventArgs does not have a new() constraint,
                    //you cannot create an instance of it using the new keyword.
                    //We need to ensure that TEventArgs has a parameterless constructor. 
                    //Activator.CreateInstance<TEventArgs>() is used to create an instance of TEventArgs
                    eventArgs: Activator.CreateInstance<TEventArgs>(),
                    topic: log.GetTopic(logCount),   ///////
                    name: eventName,
                    blockNumber: (int)log.BlockNumber.Value,
                    blockHash: log.BlockHash,
                    transactionHash: log.TransactionHash,
                    address: log.Address,
                    topics: ConvertToStringList(log.Topics),
                    data: log.Data));
                logCount++;
            }

            return fetchedEvents;
        }

        // Helper method to merge topics from filter and argumentFilters
        private string[] MergeTopics(NewFilterInput filter, Dictionary<string, object> argumentFilters)
        {
            List<string> topics = new List<string>();

            // Add topics from filter
            if (filter.Topics != null  && filter.Topics is string[] filterTopicsArray)
            {
                topics.AddRange(filterTopicsArray);
            }

            // Add topics from argumentFilters
            if (argumentFilters != null && argumentFilters.TryGetValue("topics", out var argumentFilterTopics) && argumentFilterTopics is string[] argumentFilterTopicsArray)
            {
                topics.AddRange(argumentFilterTopicsArray);
            }

            return topics.ToArray();
        }

        private List<string> ConvertToStringList(object[] topics)
        {
            if (topics == null)
                return new List<string>();

            var stringList = new List<string>();
            foreach (var topic in topics)
            {
                if (topic is string str)
                {
                    stringList.Add(str);
                }
            }
            return stringList;
        }
    }
}
