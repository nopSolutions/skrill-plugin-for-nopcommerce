using FluentValidation;
using Nop.Plugin.Payments.Skrill.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.Skrill.Validators
{
    /// <summary>
    /// Represents configuration model validator
    /// </summary>
    public class ConfigurationValidator : BaseNopValidator<ConfigurationModel>
    {
        #region Ctor

        public ConfigurationValidator(ILocalizationService localizationService)
        {
            RuleFor(model => model.MerchantEmail)
                .NotEmpty()
                .WithMessage(localizationService.GetResource("Plugins.Payments.Skrill.Fields.MerchantEmail.Required"));

            RuleFor(model => model.SecretWord)
                .NotEmpty()
                .WithMessage(localizationService.GetResource("Plugins.Payments.Skrill.Fields.SecretWord.Required"));
        }

        #endregion
    }
}