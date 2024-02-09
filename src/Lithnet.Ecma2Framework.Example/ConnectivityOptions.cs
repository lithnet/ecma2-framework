using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Lithnet.Ecma2Framework.Example
{
    /// <summary>
    /// Represents the configuration options for the connectivity page of the management agent UI
    /// </summary>
    [ConnectivityConfiguration]
    internal class ConnectivityOptions
    {
        /// <summary>
        /// Defines a configuration parameter the requests a URL for the API, and applies validation annotations to ensure the Url is well formed, and that it is required
        /// </summary>
        [StringParameter("API URL")]
        [Required]
        [Url]
        [DefaultValue("https://jsonplaceholder.typicode.com")]
        public string ApiUrl { get; set; }
    }
}
