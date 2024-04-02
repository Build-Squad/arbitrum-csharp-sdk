using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Util;
using System.Text.Json;

using Arbitrum.Utils;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Arbitrum.Message;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Arbitrum.DataEntities
{
    public class GlobalStateStructOutput
    {
        public string Item1 { get; set; }
        public string Item2 { get; set; }
        public string[] Bytes32Vals { get; set; }
        public string[] U64Vals { get; set; }
    }

    public class ExecutionStateStructOutput
    {
        public GlobalStateStructOutput GlobalState { get; set; }
        public int MachineStatus { get; set; }
    }

    public class AssertionStructOutput
    {
        public ExecutionStateStructOutput BeforeState { get; set; }
        public ExecutionStateStructOutput AfterState { get; set; }
        public int NumBlocks { get; set; }
    }

    public class NodeCreatedEvent: FetchedEvent<NodeCreatedEvent>, IEventLog
    {
        public int NodeNum { get; set; }
        public string ParentNodeHash { get; set; }
        public string NodeHash { get; set; }
        public string ExecutionHash { get; set; }
        public AssertionStructOutput Assertion { get; set; }
        public string AfterInboxBatchAcc { get; set; }
        public string WasmModuleRoot { get; set; }
        public int InboxMaxCount { get; set; }

        public FilterLog Log => throw new System.NotImplementedException();

        public NodeCreatedEvent(
            NodeCreatedEvent eventArgs, // Update the argument type here
            string topic,
            string name,
            int blockNumber,
            string blockHash,
            string transactionHash,
            string address,
            List<string> topics,
            string data,

            int nodeNum,
            string parentNodeHash,
            string nodeHash,
            string executionHash,
            AssertionStructOutput assertion,
            string afterInboxBatchAcc,
            string wasmModuleRoot,
            int inboxMaxCount
            ) : base(eventArgs, topic, name, blockNumber, blockHash, transactionHash, address, topics, data) // Explicit cast to object to resolve dynamic dispatch issue
        {
            NodeNum = nodeNum;
            ParentNodeHash = parentNodeHash;
            NodeHash = nodeHash;
            ExecutionHash = executionHash;
            Assertion = assertion;
            AfterInboxBatchAcc = afterInboxBatchAcc;
            WasmModuleRoot = wasmModuleRoot;
            InboxMaxCount = inboxMaxCount;
        }
    }



    public static class LogParser
    {
        public static string Keccak(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = Sha3Keccack.Current.CalculateHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        public static async Task<IEnumerable<L2ToL1TransactionEvent>> ParseTypedLogs(   //////
            Web3 web3,
            string contractName,
            JArray logs,
            string eventName,
            bool isClassic = false)
        {
            var (abi, contractAddress) = await LoadAbi(contractName, isClassic);

            var contract = web3.Eth.GetContract(abi, contractAddress);

            var eventAbi = contract.Abi.FirstOrDefault(e =>
            {
                if (e is Dictionary<string, object> dictionary)
                {
                    if (dictionary.ContainsKey("name") && dictionary.ContainsKey("type") &&
                        dictionary["name"].ToString() == eventName && dictionary["type"].ToString() == "event")
                    {
                        return true;
                    }
                }
                return false;
            });

            if (eventAbi == null)
                return new List<MessageEvents>();

            var eventInputs = ((JsonElement)eventAbi.GetProperty("inputs")).EnumerateArray().Select(i => i.GetProperty("type").GetString());
            var eventSignature = Keccak($"{eventName}({string.Join(",", eventInputs)})").ToString();

            var parsedLogs = new List<MessageEvents>();
            foreach (var log in logs)
            {
                var logTopic = log.Topics[0];
                var logTopicHex = logTopic.ToHex();
                if (!string.IsNullOrEmpty(logTopicHex) && logTopicHex == eventSignature)
                {
                    try
                    {
                        var logReceipt = new FilterLog[] { log }; // Corrected instantiation
                        var decodedLog = contract.GetEvent<MessageEvents>(eventName).DecodeAllEvents(logReceipt);
                        parsedLogs.Add(decodedLog);
                    }

                    catch (Exception ex)
                    {
                        throw new Exception("Error decoding log: " + ex.Message);
                    }
                }
            }
            return parsedLogs;
        }

        public static async Task<(string, string)> LoadAbi(string contractName, bool isClassic = false)
        {
            string abi, bytecode;
            string filePath = isClassic ? $"src/abi/classic/{contractName}.json" : $"src/abi/{contractName}.json";

            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string json = await reader.ReadToEndAsync();
                    var contractData = JsonSerializer.Deserialize<ContractData>(json);

                    if (contractData == null || string.IsNullOrEmpty(contractData.Abi))
                        throw new Exception($"No ABI found for contract: {contractName}");

                    abi = contractData.Abi;
                    bytecode = contractData.Bytecode;
                }
            }
            catch (Exception ex)
            {
                // Handle file not found or JSON parsing errors
                throw new Exception($"Error loading ABI for contract {contractName}: {ex.Message}");
            }

            return (abi, bytecode);
        }

        private class ContractData
        {
            public string Abi { get; set; }
            public string Bytecode { get; set; }
        }
    }
}
