using Arbitrum.DataEntities;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;


namespace Arbitrum.Utils
{
    public class FetchedEvent<IEventDTO>
    {
        public IEventDTO Event { get; set; }
        public string Topic { get; set; }
        public string Name { get; set; }
        public int BlockNumber { get; set; }
        public string BlockHash { get; set; }
        public string TransactionHash { get; set; }
        public string Address { get; set; }
        public List<string> Topics { get; set; }
        public string Data { get; set; }

        public FetchedEvent(
            IEventDTO eventArgs,
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

        public async Task<List<FetchedEvent<T>>> GetEventsAsync<T>(
        dynamic contractFactory,
        string eventName,
        Dictionary<string, object>? argumentFilters = null,
        NewFilterInput? filter = null,
        bool isClassic = false) where T : IEventDTO, new()
        {
            filter ??= new NewFilterInput();
            argumentFilters ??= new Dictionary<string, object>();
            Contract contract;

            if (contractFactory is string)
            {
                var util = new AddressUtil();
                var contractAddress = LoadContractUtils.GetAddress(
                    !string.IsNullOrEmpty(filter?.Address?.FirstOrDefault())
                        ? filter?.Address?.FirstOrDefault()!
                        : util.ConvertToChecksumAddress("0x0000000000000000000000000000000000000000")
                );

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

            var eventInstance = contract.GetEvent(eventName)
                ?? throw new ArgumentException($"Event {eventName} not found in contract");

            var eventFilter = new NewFilterInput
            {
                FromBlock = filter?.FromBlock ?? new BlockParameter(new HexBigInteger(BigInteger.Zero)),
                ToBlock = filter?.ToBlock ?? BlockParameter.CreateLatest(),
                Address = new[] { contract.Address },
                Topics = MergeTopics(filter!, argumentFilters, eventInstance.EventABI)
            };

            var logs = await _provider.Eth.Filters.GetLogs.SendRequestAsync(eventFilter);

            var decodedEvents = eventInstance.DecodeAllEventsForEvent<T>(logs.ToArray());

            var fetchedEvents = decodedEvents.Select(log => new FetchedEvent<T>(
                eventArgs: log.Event,
                topic: log.Log.Topics.FirstOrDefault()?.ToString(),
                name: eventName,
                blockNumber: (int)log.Log.BlockNumber.Value,
                blockHash: log.Log.BlockHash,
                transactionHash: log.Log.TransactionHash,
                address: log.Log.Address,
                topics: ConvertToStringList(log.Log.Topics),
                data: log.Log.Data
            )).ToList();

            return fetchedEvents;
        }

        private static string[] MergeTopics(NewFilterInput filter, Dictionary<string, object> argumentFilters, EventABI eventAbi)
        {
            var topics = new List<string>();

            if (filter.Topics != null)
            {
                foreach (var topic in filter.Topics)
                {
                    if (topic != null) topics.Add(topic?.ToString());
                }
            }

            if (argumentFilters != null)
            {
                foreach (var arg in argumentFilters)
                {
                    var param = eventAbi.InputParameters.FirstOrDefault(p => p.Name == arg.Key);
                    if (param != null)
                    {
                        var encodedBytes = param.ABIType.Encode(arg.Value);

                        if (encodedBytes.Length < 32)
                        {
                            var paddedBytes = new byte[32];
                            Buffer.BlockCopy(encodedBytes, 0, paddedBytes, 32 - encodedBytes.Length, encodedBytes.Length);
                            encodedBytes = paddedBytes;
                        }

                        topics.Add("0x" + BitConverter.ToString(encodedBytes).Replace("-", "").ToLower());
                    }
                }
            }

            return topics.ToArray();
        }

        private static List<string> ConvertToStringList(object[] topics)
        {
            return topics?.Select(t => t?.ToString()).ToList() ?? new List<string>();
        }
    }
}
