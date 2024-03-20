using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using NUnit.Framework.Internal.Execution;

namespace YourNamespace
{
    public class FetchedEvent<TEvent>
    {
        public EventArgs<TEvent> Event { get; set; }
        public string Topic { get; set; }
        public string Name { get; set; }
        public BigInteger BlockNumber { get; set; }
        public string BlockHash { get; set; }
        public string TransactionHash { get; set; }
        public string Address { get; set; }
        public List<string> Topics { get; set; }
        public string Data { get; set; }
    }

    public class EventFetcher
    {
        private readonly Web3 _web3;

        public EventFetcher(Web3 web3)
        {
            _web3 = web3;
        }

        public async Task<List<FetchedEvent<TEvent>>> GetEvents<TContract, TEventFilter>(
            TypeChainContractFactory<TContract> contractFactory,
            Func<TContract, TEventFilter> topicGenerator,
            BlockFilter filter)
            where TContract : Contract
            where TEventFilter : TypedEventFilter<TEvent>
        {
            var contract = contractFactory.Connect(filter.Address ?? Constants.AddressZero, _web3);
            var eventFilter = topicGenerator(contract);
            var fullFilter = new Filter
            {
                Address = filter.Address,
                FromBlock = filter.FromBlock,
                ToBlock = filter.ToBlock,
                Topics = eventFilter.GetTopics()
            };
            var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(fullFilter);
            var fetchedEvents = new List<FetchedEvent<TEvent>>();
            foreach (var log in logs)
            {
                if (!log.Removed)
                {
                    var pLog = contract.Interface.ParseLog(log);
                    fetchedEvents.Add(new FetchedEvent<TEvent>
                    {
                        Event = pLog.Event,
                        Topic = pLog.Topic,
                        Name = pLog.Name,
                        BlockNumber = log.BlockNumber,
                        BlockHash = log.BlockHash,
                        TransactionHash = log.TransactionHash,
                        Address = log.Address,
                        Topics = log.Topics,
                        Data = log.Data
                    });
                }
            }
            return fetchedEvents;
        }
    }
}
