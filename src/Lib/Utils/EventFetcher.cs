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
using Nethereum.Hex.HexTypes;


namespace Arbitrum.Utils
{
    public class FetchedEvent : CaseDict
    {
        public dynamic Event { get; }
        public string Topic { get; }
        public string Name { get; }
        public BigInteger BlockNumber { get; }
        public string BlockHash { get; }
        public string TransactionHash { get; }
        public string Address { get; }
        public List<string> Topics { get; }
        public string Data { get; }

        public FetchedEvent(
            dynamic _event,          //Used dynamic type here as not sure of exact type of Event
            string topic,
            string name,
            BigInteger blockNumber,
            string blockHash,
            string transactionHash,
            string address,
            List<string> topics,
                    string data) : base(data) // Passing 'data' to the base class constructor
        {
            Event = _event;
            Topic = topic?.ToString() ?? string.Empty; // Convert topic to string or default to empty string
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
                _provider = signerOrProvider.Provider;
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
        public async Task<List<FetchedEvent>> GetEventsAsync(
            dynamic contractFactory,
            string eventName,
            Dictionary<string, object> argumentFilters = null,
            Dictionary<string, object> filter = null,
            bool isClassic = false)
        {

            // Initialize filter and argumentFilters if they are null
            if (filter == null)
                filter = new Dictionary<string, object>();

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
                string contractAddress = LoadContractUtils.GetAddress(filter.ContainsKey("address")
                    ? (string)filter["address"]
                    : "0x0000000000000000000000000000000000000000");

                contract = LoadContractUtils.LoadContract(
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
            var fromBlock = new HexBigInteger(BigInteger.Zero);
            var toBlock = new HexBigInteger(BigInteger.Zero);

            if (filter is Dictionary<string, object> filterDict)
            {
                if (filterDict.TryGetValue("fromBlock", out var fromBlockObj))
                {
                    fromBlock = new HexBigInteger((long)fromBlockObj);
                }

                if (filterDict.TryGetValue("toBlock", out var toBlockObj))
                {
                    toBlock = new HexBigInteger((long)toBlockObj);
                }
            }

            // Create event filter
            var eventFilter = new NewFilterInput
            {
                FromBlock = new BlockParameter(fromBlock),
                ToBlock = new BlockParameter(toBlock),
                Address = new[] { contract.Address },
                // Merge filter and argumentFilters
                Topics = MergeTopics(filter, argumentFilters)
            };

            var logs = await _provider.Eth.Filters.GetLogs.SendRequestAsync(eventFilter);

            var fetchedEvents = new List<FetchedEvent>();

            foreach (var log in logs)
            {
                fetchedEvents.Add(new FetchedEvent (
                    _event: log.Address,
                    topic: log.GetTopic(0),   ///////
                    name: eventName,
                    blockNumber: log.BlockNumber.Value,
                    blockHash: log.BlockHash,
                    transactionHash: log.TransactionHash,
                    address: log.Address,
                    topics: ConvertToStringList(log.Topics),
                    data: log.Data));
            }

            return fetchedEvents;
        }

        // Helper method to merge topics from filter and argumentFilters
        private string[] MergeTopics(Dictionary<string, object> filter, Dictionary<string, object> argumentFilters)
        {
            List<string> topics = new List<string>();

            // Add topics from filter
            if (filter.TryGetValue("topics", out var filterTopics) && filterTopics is string[] filterTopicsArray)
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
