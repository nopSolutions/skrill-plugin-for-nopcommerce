using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Skrill
{
    /// <summary>
    /// Represents plugin settings
    /// </summary>
    public class SkrillSettings : ISettings
    {
        /// <summary>
        /// Gets or sets the email address of merchant account
        /// </summary>
        public string MerchantEmail { get; set; }

        /// <summary>
        /// Gets or sets the secret word submitted in the settings section of merchant account
        /// </summary>
        public string SecretWord { get; set; }

        /// <summary>
        /// Gets or sets the password required to request services
        /// </summary>
        public string Password { get; set; }
    }
}