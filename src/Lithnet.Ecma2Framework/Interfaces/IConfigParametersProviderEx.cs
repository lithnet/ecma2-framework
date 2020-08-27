using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConfigParametersProviderEx
    {
        void GetConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, IList<ConfigParameterDefinition> newDefinitions, ConfigParameterPage page, int pageNumber);

        ParameterValidationResult ValidateConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber);
    }
}
