using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Skrill.Models;
using Nop.Plugin.Payments.Skrill.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Security;

namespace Nop.Plugin.Payments.Skrill.Controllers
{
    [Area(AreaNames.Admin)]
    [ValidateIpAddress]
    [AuthorizeAdmin]
    [ValidateVendor]
    [AutoValidateAntiforgeryToken]
    public class SkrillController : BasePluginController
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

        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var settings = _settingService.LoadSetting<SkrillSettings>(storeScope);

            //prepare model
            var model = new ConfigurationModel
            {
                MerchantEmail = settings.MerchantEmail,
                SecretWord = settings.SecretWord,
                Password = settings.Password,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.MerchantEmail_OverrideForStore = _settingService.SettingExists(settings, setting => setting.MerchantEmail, storeScope);
                model.SecretWord_OverrideForStore = _settingService.SettingExists(settings, setting => setting.SecretWord, storeScope);
                model.Password_OverrideForStore = _settingService.SettingExists(settings, setting => setting.Password, storeScope);
            }

            return View("~/Plugins/Payments.Skrill/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var settings = _settingService.LoadSetting<SkrillSettings>(storeScope);

            settings.MerchantEmail = model.MerchantEmail;
            settings.SecretWord = model.SecretWord;
            settings.Password = model.Password;
            _settingService.SaveSetting(settings);

            //save settings
            _settingService.SaveSettingOverridablePerStore(settings, setting => setting.MerchantEmail, model.MerchantEmail_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, setting => setting.SecretWord, model.SecretWord_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(settings, setting => setting.Password, model.Password_OverrideForStore, storeScope, false);
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //validate credentials
            if (!string.IsNullOrEmpty(settings.Password))
            {
                var serviceManager = EngineContext.Current.Resolve<ServiceManager>();
                if (serviceManager.ValidateCredentials())
                    _notificationService.SuccessNotification(_localizationService.GetResource("Plugins.Payments.Skrill.Credentials.Valid"));
                else
                {
                    var error = string.Format(_localizationService.GetResource("Plugins.Payments.Skrill.Credentials.Invalid"), Url.Action("List", "Log"));
                    _notificationService.ErrorNotification(error, false);
                }
            }

            return Configure();
        }

        #endregion
    }
}