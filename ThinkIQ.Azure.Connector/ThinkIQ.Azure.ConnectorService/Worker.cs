using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using ThinkIQ.Azure.IoT.Connector;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Security;
using ThinkIQ.DataAccess;
using ThinkIQ.Azure.Connector.Utils;

namespace ThinkIQ.Azure.ConnectorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, App> _apps = new ConcurrentDictionary<string, App>();

        private DataApi _dataAccess;
        private SecureString _pgPwd;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Application starts at {time}", DateTimeOffset.Now);

            // Build data access
            var systemConfig = _configuration.GetSection("ThinkIQ");

            var dataAccessSection = systemConfig.GetSection("DataAccess");
            var dataAccessConfig = dataAccessSection.Get<DataAccessConfig>();
            _dataAccess = new DataApi(dataAccessConfig.ConnectionString);

            // Get Azure configuration
            var section = _configuration.GetSection("Azure");


            if (!string.IsNullOrWhiteSpace(section["PSWPath"]))
            {
                var pgPwd = File.ReadAllText(section["PSWPath"]);
                _pgPwd = pgPwd.ConvertToSecureString();
            }

            if (_pgPwd == null)
            {
                // Local dev using appsettings.json to specify keys
                var appsSection = section.GetSection("Apps");
                var azureAppConfigs = appsSection.Get<IList<AzureAppConfig>>();
                StartAndStopApps(azureAppConfigs, cancellationToken);
            }

            return base.StartAsync(cancellationToken);
        }

        private void StartAndStopApps(IEnumerable<AzureAppConfig> azureAppConfigs, CancellationToken cancellationToken)
        {
            var appIds = new List<string>(_apps.Keys);
            foreach (var azureAppConfig in azureAppConfigs)
            {
                // Check if we have already processed this application
                if (appIds.Contains(azureAppConfig.IoTCentralApplicationId))
                {
                    // Remove this app from check list to track apps that no longer exist in the store
                    appIds.Remove(azureAppConfig.IoTCentralApplicationId);
                    continue;
                }

                try
                {
                    var appConfig = new AppConfig
                    {
                        AzureConfig = azureAppConfig,
                        DataAccess = _dataAccess
                    };

                    var app = new App(appConfig);
                    var task = app.StartAsync();
                    task.Wait(cancellationToken);
                    app.ExecuteOnTimer();
                    _apps.AddOrUpdate(azureAppConfig.IoTCentralApplicationId, app, (key, oldValue) => app);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, $"Failed to start application {azureAppConfig.IoTCentralApplicationId}.");
                }
            }

            // Clean up old apps
            foreach (var oldAppId in appIds)
            {
                if (_apps.TryGetValue(oldAppId, out var app))
                {
                    var task = app.StopAsync();
                    task.Wait(cancellationToken);
                    _apps.TryRemove(oldAppId, out _);
                }
            }
        }

        private void SetupAppsConfiguredInStore(CancellationToken cancellationToken)
        {
            // In production, use encrypted keys in database
            var pwd = _pgPwd.ConvertToString();
            var task = _dataAccess.GetAppConfigs(pwd);
            var azureAppConfigs = task.Result;
            StartAndStopApps(azureAppConfigs, cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var app in _apps.Values)
            {
                var task = app.StopAsync();
                task.Wait(cancellationToken);
            }

            _logger.LogInformation("Application stops at: {time}", DateTimeOffset.Now);
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
                SetupAppsConfiguredInStore(stoppingToken);

                // Every 10 seconds, check for configuration changes in database
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}