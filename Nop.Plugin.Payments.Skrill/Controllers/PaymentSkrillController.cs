using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Skrill.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Skrill.Controllers
{
    public class PaymentSkrillController : BasePaymentController
    {
        private readonly IStoreContext _storeContext;
        private readonly ISettingService _settingService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILogger _logger;
        private readonly SkrillPaymentSettings _skrillPaymentSettings;
        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;
        private readonly IPermissionService _permissionService;

        public PaymentSkrillController(
            IStoreContext storeContext,
            ISettingService settingService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            ILogger logger,
            SkrillPaymentSettings skrillPaymentSettings,
            ILocalizationService localizationService,
            IWebHelper webHelper,
            IPermissionService permissionService)
        {
            this._storeContext = storeContext;
            this._settingService = settingService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._logger = logger;
            this._skrillPaymentSettings = skrillPaymentSettings;
            this._localizationService = localizationService;
            this._webHelper = webHelper;
            this._permissionService = permissionService;
        }

        private string GetValue(string key, IFormCollection form)
        {
            return (form.Keys.Contains(key) ? form[key].ToString() : _webHelper.QueryString<string>(key)) ?? string.Empty;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var skrillPaymentSettings = _settingService.LoadSetting<SkrillPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                ActiveStoreScopeConfiguration = storeScope,
                PayToEmail = skrillPaymentSettings.PayToEmail,
                SecretWord = skrillPaymentSettings.SecretWord,
                AdditionalFee = skrillPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = skrillPaymentSettings.AdditionalFeePercentage
            };

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.PayToEmail_OverrideForStore = _settingService.SettingExists(skrillPaymentSettings, x => x.PayToEmail, storeScope);
                model.SecretWord_OverrideForStore = _settingService.SettingExists(skrillPaymentSettings, x => x.SecretWord, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(skrillPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(skrillPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            }

            return View("~/Plugins/Payments.Skrill/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var skrillPaymentSettings = _settingService.LoadSetting<SkrillPaymentSettings>(storeScope);

            //save settings
            skrillPaymentSettings.PayToEmail = model.PayToEmail;
            skrillPaymentSettings.SecretWord = model.SecretWord;
            skrillPaymentSettings.AdditionalFee = model.AdditionalFee;
            skrillPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(skrillPaymentSettings, x => x.PayToEmail, model.PayToEmail_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(skrillPaymentSettings, x => x.SecretWord, model.SecretWord_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(skrillPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(skrillPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
            
            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        private static string StringToMD5(string str)
        {
            var cryptHandler = new MD5CryptoServiceProvider();
            var ba = cryptHandler.ComputeHash(Encoding.UTF8.GetBytes(str));

            var hex = new StringBuilder(ba.Length * 2);

            foreach (var b in ba)
                hex.AppendFormat("{0:X2}", b);

            return hex.ToString();
        }

        public IActionResult ResponseNotificationHandler(IpnModel model)
        {
            var form = model.Form;

            var orderIdValue = GetValue("transaction_id", form);
            if (!int.TryParse(orderIdValue, out int orderId))
            {
                const string errorStr = "Skrill response notification. Can\'t parse order id";
                _logger.Error(errorStr);
                return Content("");
            }

            var order = _orderService.GetOrderById(orderId);

            //no order found
            if (order == null)
            {
                var errorStr = $"Skrill response notification. No order found with the specified id: {orderId}";
                _logger.Error(errorStr);
                return Content("");
            }

            //validate the Skrill signature
            var concatFields = GetValue("merchant_id", form)
                               + GetValue("transaction_id", form)
                               + StringToMD5(_skrillPaymentSettings.SecretWord)
                               + GetValue("mb_amount", form)
                               + GetValue("mb_currency", form)
                               + GetValue("status", form);

            var payToEmail = _skrillPaymentSettings.PayToEmail;

            //ensure that the signature is valid
            if (!GetValue("md5sig", form).Equals(StringToMD5(concatFields), StringComparison.InvariantCultureIgnoreCase))
            {
                var errorStr = $"Skrill response notification. Hash value doesn't match. Order id: {order.Id}";
                _logger.Error(errorStr);

                return Content("");
            }

            //ensure that the money is going to you
            if (GetValue("pay_to_email", form) != payToEmail)
            {
                var errorStr = $"Skrill response notification. Returned 'Pay to' email {GetValue("pay_to_email", form)} doesn't equal 'Pay to' email {payToEmail}. Order id: {order.Id}";
                _logger.Error(errorStr);

                return Content("");
            }

            //ensure that the status code == 2
            if (GetValue("status", form) != "2")
            {
                var errorStr = $"Skrill response notification. Wrong status: {GetValue("status", form)}. Order id: {order.Id}";
                _logger.Error(errorStr);

                return Content("");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Skrill response notification.");
            var keys = form.Keys.ToList();
            keys.AddRange(Request.Query.Keys.ToArray());
            foreach (var key in keys)
            {
                sb.AppendLine(key + ": " + GetValue(key, form));
            }

            //order note
            order.OrderNotes.Add(new OrderNote
            {
                Note = sb.ToString(),
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });
            _orderService.UpdateOrder(order);

            //can mark order is paid?
            if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                order.AuthorizationTransactionId = GetValue("mb_transaction_id", form);
                _orderService.UpdateOrder(order);
                _orderProcessingService.MarkOrderAsPaid(order);
            }

            //nothing should be rendered to visitor
            return Content("");
        }
    }
}