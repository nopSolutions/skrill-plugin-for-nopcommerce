using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Skrill.Domain;
using Nop.Plugin.Payments.Skrill.Models;
using Nop.Plugin.Payments.Skrill.Services;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Skrill.Controllers
{
    [Area(AreaNames.Admin)]
    [ValidateIpAddress]
    [AuthorizeAdmin]
    [ValidateVendor]
    [AutoValidateAntiforgeryToken]
    public class SkrillController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;

        #endregion

        #region Ctor

        public SkrillController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
        }

        #endregion

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<SkrillSettings>(storeScope);

            //prepare model
            var model = new ConfigurationModel
            {
                MerchantEmail = settings.MerchantEmail,
                SecretWord = settings.SecretWord,
                Password = settings.Password,
                ActiveStoreScopeConfiguration = storeScope,
                PaymentFlowTypeId = (int)settings.PaymentFlowType,
                PaymentFlowTypes = await settings.PaymentFlowType.ToSelectListAsync()
            };

            if (storeScope > 0)
            {
                model.MerchantEmail_OverrideForStore = await _settingService.SettingExistsAsync(settings, setting => setting.MerchantEmail, storeScope);
                model.SecretWord_OverrideForStore = await _settingService.SettingExistsAsync(settings, setting => setting.SecretWord, storeScope);
                model.Password_OverrideForStore = await _settingService.SettingExistsAsync(settings, setting => setting.Password, storeScope);
                model.PaymentFlowTypeId_OverrideForStore = await _settingService.SettingExistsAsync(settings, setting => setting.PaymentFlowType, storeScope);
            }

            return View("~/Plugins/Payments.Skrill/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<SkrillSettings>(storeScope);

            settings.MerchantEmail = model.MerchantEmail;
            settings.SecretWord = model.SecretWord.Trim();
            settings.Password = model.Password.Trim();
            settings.PaymentFlowType = (PaymentFlowType)model.PaymentFlowTypeId;

            //save settings
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.MerchantEmail, model.MerchantEmail_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.SecretWord, model.SecretWord_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.Password, model.Password_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.PaymentFlowType, model.PaymentFlowTypeId_OverrideForStore, storeScope, false);
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            //validate credentials
            if (!string.IsNullOrEmpty(settings.Password))
            {
                var serviceManager = EngineContext.Current.Resolve<ServiceManager>();
                if (await serviceManager.ValidateCredentialsAsync())
                    _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Payments.Skrill.Credentials.Valid"));
                else
                {
                    var error = string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Skrill.Credentials.Invalid"), Url.Action("List", "Log"));
                    _notificationService.ErrorNotification(error, false);
                }
            }

            return await Configure();
        }

        #endregion
    }
}