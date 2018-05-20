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
    public interface ICapabilitiesProvider
    {
        MACapabilities GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters);
    }
} 
