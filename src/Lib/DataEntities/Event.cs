using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Util;
using System.Text.Json;
using Arbitrum.Utils;
using System.Text;
using Nethereum.ABI.Model;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Newtonsoft.Json;
using static Arbitrum.Utils.LoadContractUtils;
using Nethereum.ABI.CompilationMetadata;

namespace Arbitrum.DataEntities
{
    public class GlobalStateStructOutput
    {
        public string? Item1 { get; set; }
        public string? Item2 { get; set; }
        public string[]? Bytes32Vals { get; set; }
        public string[]? U64Vals { get; set; }
    }

    public class ExecutionStateStructOutput
    {
        public GlobalStateStructOutput? GlobalState { get; set; }
        public int MachineStatus { get; set; }
    }

    public class AssertionStructOutput
    {
        public ExecutionStateStructOutput? BeforeState { get; set; }
        public ExecutionStateStructOutput? AfterState { get; set; }
        public int NumBlocks { get; set; }
    }

    public class NodeCreatedEvent : L2ToL1TransactionEvent
    {
        public int? NodeNum { get; set; }
        public string? ParentNodeHash { get; set; }
        public string? NodeHash { get; set; }
        public string? ExecutionHash { get; set; }
        public AssertionStructOutput? Assertion { get; set; }
        public string? AfterInboxBatchAcc { get; set; }
        public string? WasmModuleRoot { get; set; }
        public int? InboxMaxCount { get; set; }

        public FilterLog Log => throw new System.NotImplementedException();

        //public NodeCreatedEvent(
        //    L2ToL1TransactionEvent eventArgs, // Update the argument type here
        //    string topic,
        //    string name,
        //    int blockNumber,
        //    string blockHash,
        //    string transactionHash,
        //    string address,
        //    List<string> topics,
        //    string data,

        //    int nodeNum,
        //    string parentNodeHash,
        //    string nodeHash,
        //    string executionHash,
        //    AssertionStructOutput assertion,
        //    string afterInboxBatchAcc,
        //    string wasmModuleRoot,
        //    int inboxMaxCount
        //    ) : base(eventArgs, topic, name, blockNumber, blockHash, transactionHash, address, topics, data) // Explicit cast to object to resolve dynamic dispatch issue
        //{
        //    NodeNum = nodeNum;
        //    ParentNodeHash = parentNodeHash;
        //    NodeHash = nodeHash;
        //    ExecutionHash = executionHash;
        //    Assertion = assertion;
        //    AfterInboxBatchAcc = afterInboxBatchAcc;
        //    WasmModuleRoot = wasmModuleRoot;
        //    InboxMaxCount = inboxMaxCount;
        //}
    }

    public class Input
    {
        public string internalType { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool? indexed { get; set; } // Nullable boolean to handle absence of the key
    }
    public class Output
    {
        public string internalType { get; set; }
        public string name { get; set; }
        public string type { get; set; }
    }

    public class AbiItem
    {
        public List<Input> inputs { get; set; }
        public List<Output> outputs { get; set; } // Add outputs property
        public string name { get; set; }
        public string type { get; set; }
        public string stateMutability { get; set; } // Add stateMutability property
        public bool? anonymous { get; set; } // Nullable boolean to handle absence of the key
    }

    public class RootObject
    {
        public string _format { get; set; }
        public string contractName { get; set; }
        public string sourceName { get; set; }
        public List<AbiItem> abi { get; set; }
        public string bytecode { get; set; }
        public string deployedBytecode { get; set; }
        public object linkReferences { get; set; }
        public object deployedLinkReferences { get; set; }
    }



    public static class LogParser
    {
        public static string Keccak(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = Sha3Keccack.Current.CalculateHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        public static async Task<IEnumerable<EventLog<T>>> ParseTypedLogs<T, TContract>(
        Web3 web3,
        string contractName,
        JArray logs,
        string eventName,
        bool isClassic = false)
        where TContract : Contract
        where T : new()
        {
            try
            {
                var (abi, contractAddress) = await LoadAbi(contractName, isClassic);

                var contracta = new ContractBuilder(abi, contractAddress);

                var contract = web3.Eth.GetContract(abi, contractAddress);

                var _event = contract.GetEvent(eventName);
                var eventABI = _event.EventABI;

                //var eventABI = contract.ContractBuilder.GetEventAbi(contractName);

                FilterLog[] parsedLogs = EventExtensions.GetLogsForEvent(eventABI, logs);

                //var parsedLogs = logs.Select(l => new FilterLog
                //{
                //    Address = l["address"].ToString(),
                //    Topics = l["topics"].Select(t => t.ToString()).ToArray(),
                //    Data = l["data"].ToString()
                //}).ToArray();

                var decodedLogs = new List<EventLog<T>>();

                foreach (var log in parsedLogs)
                {
                    var logTopic = log.Topics[0];
                    if (logTopic.ToString() == eventABI.Sha3Signature)
                    {
                        var decodedLog = contract.GetEvent(eventName).DecodeAllEventsForEvent<T>(parsedLogs);
                        decodedLogs.AddRange(decodedLog);
                    }
                }
                return decodedLogs;
            }
            catch(Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public static async Task<(string, string)> LoadAbi(string contractName, bool isClassic = false)
        {
            string abi;
            string bytecode;
            string filePath = isClassic ? $"src/abi/classic/{contractName}.json" : $"src/abi/{contractName}.json";

            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string json = await reader.ReadToEndAsync();

                    string json2 = File.ReadAllText(filePath);

                    RootObject contractData = JsonConvert.DeserializeObject<RootObject>(json);

                    if (contractData == null || string.IsNullOrEmpty(contractData.abi.ToString()))
                        throw new Exception($"No ABI found for contract: {contractName}");

                    abi = JsonConvert.SerializeObject(contractData.abi);
                    bytecode = contractData.bytecode;
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
            public string[]? Abi { get; set; }
            public string? Bytecode { get; set; }
        }
    }
}
