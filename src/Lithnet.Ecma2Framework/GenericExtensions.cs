using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.CodeAnalysis;

namespace Lithnet.Ecma2Framework
{
    internal static class GenericExtensions
    {
        /// <summary>Indicates whether or not the class has a specific interface.</summary>
        /// <returns>Whether or not the SyntaxList contains the attribute.</returns>
        public static bool HasInterface(this INamedTypeSymbol declaredTypeSymbol, GeneratorSyntaxContext context, string interfaceName)
        {
            var namedTypeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(interfaceName);

            foreach (var interfaceTypeSymbol in declaredTypeSymbol.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(interfaceTypeSymbol, namedTypeSymbol))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ConvertToUnsecureString(this SecureString securePassword)
        {
            if (securePassword == null)
                throw new ArgumentNullException(nameof(securePassword));

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}