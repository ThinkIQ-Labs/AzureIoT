using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ThinkIQ.Azure.Connector.Utils;

namespace ThinkIQ.Azure.ConnectorService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var host = CreateHostBuilder(args).Build();
                AppService.Instance.Initialize(host.Services);
                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>

            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureServices((hostContext, services) => services.AddHostedService<Worker>())
                .UseSerilog((hostContext, loggerConfig) =>
                    loggerConfig.ReadFrom.Configuration(hostContext.Configuration));
    }
}