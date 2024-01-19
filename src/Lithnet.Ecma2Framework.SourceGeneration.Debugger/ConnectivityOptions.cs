using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Lithnet.Ecma2Framework.SourceGeneration.Debugger
{
    [ConnectivityConfiguration]
    internal class ConnectivityOptions
    {
        [LabelParameter("API details")]
        [Required]
        [EncryptedStringParameter("API Key")]
        [DefaultValue("ABC123")]
        public string ApiKey { get; set; }

        [Required]
        [Url]
        [DefaultValue("https://lithnet.io")]
        [StringParameter("Tenant URL")]
        public string TenantUrl { get; set; }

        [DividerParameter]
        [FileParameter("Log file name")]
        public string File { get; set; }

        [DefaultValue(true)]
        [CheckboxParameter("Enable logging")]
        public bool EnableLogging { get; set; }

        [DefaultValue("Info")]
        [DropdownParameter("Log level", false, new string[] { "Info", "Warning", "Error" })]
        public string LogLevel { get; set; }

        [LabelParameter("Advanced options")]
        public string AdvancedOptions { get; set; }

        [MultilineTextboxParameter("Tell a story")]
        public string StoryTime { get; set; }
    }
}
