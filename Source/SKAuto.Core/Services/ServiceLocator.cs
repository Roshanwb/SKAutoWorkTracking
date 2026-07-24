using Microsoft.Extensions.DependencyInjection;

namespace SKAuto.Core.Services
{
    public static class ServiceLocator
    {
        private static IServiceProvider? _provider;

        public static void SetProvider(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public static T GetService<T>() where T : notnull
        {
            if (_provider is null)
                throw new InvalidOperationException("ServiceLocator not initialized. Call SetProvider first.");
            return _provider.GetRequiredService<T>();
        }

        public static object GetService(Type serviceType)
        {
            if (_provider is null)
                throw new InvalidOperationException("ServiceLocator not initialized. Call SetProvider first.");
            return _provider.GetRequiredService(serviceType);
        }
    }
}