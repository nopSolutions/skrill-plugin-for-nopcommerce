using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Skrill.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.PayToEmail")]
        public string PayToEmail { get; set; }
        public bool PayToEmail_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.SecretWord")]
        public string SecretWord { get; set; }
        public bool SecretWord_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }
    }
}