using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IConfigurationService
    {
        Task<T> GetAsync<T>(string key) where T : new();
        Task SetAsync<T>(string key, T value);
    }
}