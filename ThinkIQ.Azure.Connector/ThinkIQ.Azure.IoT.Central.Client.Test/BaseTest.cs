using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ThinkIQ.Azure.IoT.Central.Client.Test
{
    public class BaseTest
    {
        public BaseTest()
        {
            Host = CreateHostBuilder().Build();
            Task.Run(() => Host.RunAsync());
        }

        public IHost Host { get; }

        public static IHostBuilder CreateHostBuilder(string[] args = null) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args);
    }
}
