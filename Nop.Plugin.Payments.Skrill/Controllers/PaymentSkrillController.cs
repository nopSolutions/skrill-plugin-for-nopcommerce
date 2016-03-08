using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Skrill.Models;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Skrill.Controllers
{
    public class PaymentSkrillController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILogger _logger;
        private readonly SkrillPaymentSettings _skrillPaymentSettings;

        public PaymentSkrillController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            ILogger logger,
            SkrillPaymentSettings skrillPaymentSettings)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._logger = logger;
            this._skrillPaymentSettings = skrillPaymentSettings;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
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

            return View("~/Plugins/Payments.Skrill/Views/PaymentSkrill/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var skrillPaymentSettings = _settingService.LoadSetting<SkrillPaymentSettings>(storeScope);

            //save settings
            skrillPaymentSettings.PayToEmail = model.PayToEmail;
            skrillPaymentSettings.SecretWord = model.SecretWord;
            skrillPaymentSettings.AdditionalFee = model.AdditionalFee;
            skrillPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.PayToEmail_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(skrillPaymentSettings, x => x.PayToEmail, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(skrillPaymentSettings, x => x.PayToEmail, storeScope);

            if (model.SecretWord_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(skrillPaymentSettings, x => x.SecretWord, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(skrillPaymentSettings, x => x.SecretWord, storeScope);

            if (model.AdditionalFee_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(skrillPaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(skrillPaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentage_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(skrillPaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(skrillPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            return Configure();
        }

        static string StringToMD5(string str)
        {
            var cryptHandler = new MD5CryptoServiceProvider();
            byte[] ba = cryptHandler.ComputeHash(Encoding.UTF8.GetBytes(str));

            var hex = new StringBuilder(ba.Length * 2);

            foreach (byte b in ba)
                hex.AppendFormat("{0:X2}", b);

            return hex.ToString();
        }


        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.Skrill/Views/PaymentSkrill/PaymentInfo.cshtml");
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [ValidateInput(false)]
        public ActionResult ResponseNotificationHandler()
        {
            var orderIdValue = Request["transaction_id"];
            int orderId;
            if (int.TryParse(orderIdValue, out orderId))
            {
                var order = _orderService.GetOrderById(orderId);

                //no order found
                if (order == null)
                {
                    string errorStr =
                        string.Format(
                            "Skrill response notification. No order found with the specified id: {0}", orderId);
                    _logger.Error(errorStr);
                    return Content("");
                }

                //validate the Skrill signature
                string concatFields = Request["merchant_id"]
                                      + Request["transaction_id"]
                                      + StringToMD5(_skrillPaymentSettings.SecretWord)
                                      + Request["mb_amount"]
                                      + Request["mb_currency"]
                                      + Request["status"];

                string payToEmail = _skrillPaymentSettings.PayToEmail;

                //ensure that the signature is valid
                if (!Request["md5sig"].Equals(StringToMD5(concatFields)))
                {
                    string errorStr =
                        string.Format(
                            "Skrill response notification. Hash value doesn't match. Order id: {0}", order.Id);
                    _logger.Error(errorStr);

                    return Content("");
                }

                //ensure that the money is going to you
                if (Request["pay_to_email"] != payToEmail)
                {
                    string errorStr =
                        string.Format(
                            "Skrill response notification. Returned 'Pay to' email {0} doesn't equal 'Pay to' email {1}. Order id: {2}",
                            Request["pay_to_email"], payToEmail, order.Id);
                    _logger.Error(errorStr);

                    return Content("");
                }

                //ensure that the status code == 2
                if (Request["status"] != "2")
                {
                    string errorStr =
                        string.Format(
                            "Skrill response notification. Wrong status: {0}. Order id: {1}", Request["status"], order.Id);
                    _logger.Error(errorStr);

                    return Content("");
                }

                var sb = new StringBuilder();
                sb.AppendLine("Skrill response notification.");
                foreach (var key in Request.Params.AllKeys)
                {
                    sb.AppendLine(key + ": " + Request[key]);
                }

                //order note
                order.OrderNotes.Add(new OrderNote()
                {
                    Note = sb.ToString(),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.UpdateOrder(order);

                //can mark order is paid?
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    order.AuthorizationTransactionId = Request["mb_transaction_id"];
                    _orderService.UpdateOrder(order);
                    _orderProcessingService.MarkOrderAsPaid(order);
                }
            }
            else
            {
                string errorStr =
                    string.Format(
                        "Skrill response notification. Can't parse order id");
                _logger.Error(errorStr);
            }

            //nothing should be rendered to visitor
            return Content("");
        }
    }
}