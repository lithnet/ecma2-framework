using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Lithnet.Ecma2Framework.Example
{
    [ConnectivityConfiguration]
    internal class ConnectivityOptions
    {
        [StringParameter("Tenant URL")]
        [Required]
        [Url]
        [DefaultValue("https://jsonplaceholder.typicode.com")]
        public string TenantUrl { get; set; }
    }
}
