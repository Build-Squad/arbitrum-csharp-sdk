using Arbitrum.ContractFactory;
using Arbitrum.Utils;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Newtonsoft.Json.Linq;

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

    public static class LogParser
    {
        public static IEnumerable<EventLog<T>> ParseTypedLogs<T>(Web3 web3, JArray logs, string? address = null) where T: IEventDTO, new()
        {
            try
            {
                var _event = web3.Eth.GetEvent<T>(address);

                var parsedLogs = EventExtensions.GetLogsForEvent(_event.EventABI, logs);

                var decodedLogs = new List<EventLog<T>>();

                foreach (var log in parsedLogs)
                {
                    var logTopic = log.Topics[0];
                    if (logTopic.ToString() == _event.EventABI.Sha3Signature.EnsureHexPrefix())
                    {
                        var decodedLog = _event.DecodeAllEventsForEvent(parsedLogs);
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

        public static async Task<(string?, string?)> LoadAbi(string contractName, bool isClassic = false)
        {
            string? abi, bytecode;
            string filePath = isClassic ? $"src/abi/classic/{contractName}.json" : $"src/abi/{contractName}.json";

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    string json = await reader.ReadToEndAsync();

                    dynamic contractData = JObject.Parse(json);

                    if (contractData == null || string.IsNullOrEmpty(contractData?.abi.ToString()))
                        throw new Exception($"No ABI found for contract: {contractName}");

                    abi = contractData?.abi?.ToString();
                    bytecode = contractData?.bytecode;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading ABI for contract {contractName}: {ex.Message}");
            }

            return (abi, bytecode);
        }
    }
}
