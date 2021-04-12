using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Skrill.Models
{
    /// <summary>
    /// Represents configuration model
    /// </summary>
    public record ConfigurationModel : BaseNopModel
    {
        #region Properties

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.MerchantEmail")]
        public string MerchantEmail { get; set; }
        public bool MerchantEmail_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.SecretWord")]
        [DataType(DataType.Password)]
        public string SecretWord { get; set; }
        public bool SecretWord_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.Password")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        public bool Password_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Skrill.Fields.PaymentFlowType")]
        public int PaymentFlowTypeId { get; set; }
        public bool PaymentFlowTypeId_OverrideForStore { get; set; }
        public SelectList PaymentFlowTypes { get; set; }

        #endregion
    }
}