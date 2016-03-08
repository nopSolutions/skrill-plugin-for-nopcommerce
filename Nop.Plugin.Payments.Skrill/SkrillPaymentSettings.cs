using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Skrill
{
    public class SkrillPaymentSettings : ISettings
    {
        public string PayToEmail { get; set; }

        public string SecretWord { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }

        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
    }
}