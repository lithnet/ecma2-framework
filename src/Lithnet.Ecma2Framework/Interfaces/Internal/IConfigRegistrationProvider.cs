using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework.Internal
{
    public interface IConfigRegistrationProvider
    {
        void RegisterOptions(IServiceCollection services, IConfiguration configuration);

        string GetKeyFromParameterName(string name);

        string GetTypeNameFromParameterName(string name);
    }
}
