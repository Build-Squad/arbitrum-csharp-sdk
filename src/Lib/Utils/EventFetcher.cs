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

namespace Arbitrum.Utils
{
    public class FetchedEvent : CaseDict
    {
        public string Event { get; }
        public string Topic { get; }
        public string Name { get; }
        public BigInteger BlockNumber { get; }
        public string BlockHash { get; }
        public string TransactionHash { get; }
        public string Address { get; }
        public List<string> Topics { get; }
        public string Data { get; }

        public FetchedEvent(
            string _event,
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
        public readonly object _provider;

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

        public async Task<List<FetchedEvent>> GetEventsAsync(
            string contractFactory,
            string eventName,
            object argumentFilters = null,
            object filter = null,
            bool isClassic = false)
        {
            if (filter == null)
                filter = new { };

            if (argumentFilters == null)
                argumentFilters = new { };

            var contractAddress = LoadContractUtils.GetChecksumAddress("0x0000000000000000000000000000000000000000");
            var contract = LoadContractUtils.LoadContract(provider: _provider, contractName: contractFactory, address: contractAddress, isClassic: isClassic);

            Event eventHandler = contract.GetEvent(eventName);
            if (eventHandler == null)
                throw new ArgumentException($"Event {eventName} not found in contract");

            var ethGetLogs = new EthGetLogs((IClient)_provider);

            var filterInput = new NewFilterInput
            {
                FromBlock = BlockParameter.CreateEarliest(),
                ToBlock = BlockParameter.CreateLatest(),
                Address = new List<string> { contract.Address }.ToArray()
            };

            var logs = await ethGetLogs.SendRequestAsync(filterInput);
            var fetchedEvents = new List<FetchedEvent>();

            foreach (var log in logs)
            {
                fetchedEvents.Add(new FetchedEvent(
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
