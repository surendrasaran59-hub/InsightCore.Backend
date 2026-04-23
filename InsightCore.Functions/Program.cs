using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;

namespace InsightCore.Functions
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .Build();

            host.Run();
        }
    }
}
