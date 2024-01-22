using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Lithnet.Ecma2Framework
{
    internal class DiscoveredConfigClass
    {
        public string ClassName { get; set; }

        public string SectionName { get; set; }

        public ConfigParameterPage Page { get; set; }

        public List<string> ServicesToRegister { get; set; } = new List<string>();

        public List<string> ValidatorsToAdd { get; set; } = new List<string>();

        public List<string> ParametersToAdd { get; set; } = new List<string>();

        public INamedTypeSymbol ClassSymbol { get; set; }

        public AttributeData ConfigAttribute { get; set; }

        public HashSet<string> PropertiesDecorated { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}