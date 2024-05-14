using Serilog;
using Serilog.Events;
using Serilog.Sinks; // Add this line

namespace Arbitrum
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            // Your code here

            Log.CloseAndFlush();
        }
    }

}