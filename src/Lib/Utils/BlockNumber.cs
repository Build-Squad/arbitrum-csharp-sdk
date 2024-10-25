using System;
using System.Numerics;
using System.Runtime.Serialization;
using System.Threading.Tasks;

public class BlockNumberManager
{
    private TaskCompletionSource<BlockResult> _internalBlockNumber;
    private int _maxInternalBlockNumber = 0;

    // Simulated network and block functions
    private async Task<int> PerformGetBlockNumber()
    {
        // Simulating an async call to get the block number
        await Task.Delay(100); // Simulating network delay
        return 10000; // Simulated block number
    }

    private async Task<Exception> GetNetwork()
    {
        await Task.Delay(50); // Simulating network check
        return null; // No network error
    }

    private int GetTime()
    {
        return (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
    }

    public async Task<int> GetInternalBlockNumber(int maxAge)
    {
        await Ready();

        if (maxAge > 0)
        {
            // Check for pending internal block requests
            while (_internalBlockNumber != null)
            {
                var internalBlockNumber = _internalBlockNumber.Task;

                try
                {
                    var result = await internalBlockNumber;

                    if ((GetTime() - result.RespTime) <= maxAge)
                    {
                        return result.BlockNumber;
                    }

                    // If too old, fetch a new one
                    break;
                }
                catch (Exception)
                {
                    if (_internalBlockNumber.Task == internalBlockNumber)
                    {
                        break;
                    }
                }
            }
        }

        var reqTime = GetTime();

        var checkInternalBlockNumber = FetchInternalBlockNumber(reqTime);
        _internalBlockNumber = checkInternalBlockNumber;

        checkInternalBlockNumber.Task.ContinueWith(t =>
        {
            if (t.IsFaulted && _internalBlockNumber.Task == t)
            {
                _internalBlockNumber = null;
            }
        });

        var resultBlock = await checkInternalBlockNumber.Task;
        return resultBlock.BlockNumber;
    }

    private TaskCompletionSource<BlockResult> FetchInternalBlockNumber(int reqTime)
    {
        var tcs = new TaskCompletionSource<BlockResult>();

        PerformGetBlockNumber().ContinueWith(async blockTask =>
        {
            try
            {
                var networkError = await GetNetwork();

                if (networkError != null)
                {
                    if (_internalBlockNumber.Task == tcs.Task)
                    {
                        _internalBlockNumber = null;
                    }
                    tcs.SetException(networkError);
                }
                else
                {
                    var blockNumber = blockTask.Result;

                    var respTime = GetTime();
                    if (blockNumber < _maxInternalBlockNumber)
                    {
                        blockNumber = _maxInternalBlockNumber;
                    }

                    _maxInternalBlockNumber = blockNumber;
                    tcs.SetResult(new BlockResult
                    {
                        BlockNumber = blockNumber,
                        ReqTime = reqTime,
                        RespTime = respTime
                    });
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs;
    }

    private int? _fastBlockNumber = null;
    private DateTime _fastQueryDate;
    private TaskCompletionSource<int> _fastBlockNumberPromise = new TaskCompletionSource<int>();

    // Method to update the fast block number
    public void SetFastBlockNumber(int blockNumber)
    {
        // Older block, maybe a stale request
        if (_fastBlockNumber != null && blockNumber < _fastBlockNumber) return;

        // Update the time we updated the block number
        _fastQueryDate = DateTime.UtcNow;

        // Newer block number, use it
        if (_fastBlockNumber == null || blockNumber > _fastBlockNumber)
        {
            _fastBlockNumber = blockNumber;
            _fastBlockNumberPromise.SetResult(blockNumber);
        }
    }

    //public async Task<object> GetBlockTag(object blockTag)
    //{
    //    // Await if blockTag is a Task
    //    if (blockTag is Task<object> blockTagTask)
    //    {
    //        blockTag = await blockTagTask;
    //    }

    //    // Check if blockTag is a number and is negative
    //    if (blockTag is int numberBlockTag && numberBlockTag < 0)
    //    {
    //        if (numberBlockTag % 1 != 0)
    //        {
    //            throw new ArgumentException("Invalid BlockTag", nameof(blockTag));
    //        }

    //        int blockNumber = await GetInternalBlockNumber(100 + 2 * pollingInterval);
    //        blockNumber += numberBlockTag;
    //        if (blockNumber < 0) blockNumber = 0;

    //        return blockNumber;
    //    }

    //    return blockTag;
    //}

    private async Task Ready()
    {
        // Simulating readiness (this could be an async setup operation)
        await Task.Delay(10);
    }

    public class BlockResult
    {
        public int BlockNumber { get; set; }
        public int ReqTime { get; set; }
        public int RespTime { get; set; }
    }
}
