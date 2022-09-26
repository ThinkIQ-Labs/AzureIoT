using System;

namespace ThinkIQ.Azure.Connector.Utils
{
    public sealed class AppService
    {
        private static readonly AppService _instance = new AppService();
        private IServiceProvider _provider;

        private AppService()
        {
        }

        public static AppService Instance => _instance;

        public void Initialize(IServiceProvider provider)
        {
            _provider ??= provider;
        }

        public IServiceProvider Provider => _provider;
    }
}
