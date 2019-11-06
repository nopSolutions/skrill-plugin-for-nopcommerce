﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Skrill.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.Skrill
{
    /// <summary>
    /// Represents a payment method implementation
    /// </summary>
    public class PaymentMethod : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly ServiceManager _serviceManager;

        #endregion

        #region Ctor

        public PaymentMethod(IActionContextAccessor actionContextAccessor,
            ILocalizationService localizationService,
            INotificationService notificationService,
            ISettingService settingService,
            IUrlHelperFactory urlHelperFactory,
            ServiceManager serviceManager)
        {
            _actionContextAccessor = actionContextAccessor;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _settingService = settingService;
            _urlHelperFactory = urlHelperFactory;
            _serviceManager = serviceManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var redirectUrl = _serviceManager.PrepareCheckoutUrl(postProcessPaymentRequest);
            if (string.IsNullOrEmpty(redirectUrl))
            {
                redirectUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(Defaults.OrderDetailsRouteName);
                _notificationService.ErrorNotification("Something went wrong, contact the store manager");
            }
            _actionContextAccessor.ActionContext.HttpContext.Response.Redirect(redirectUrl);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var amount = refundPaymentRequest.AmountToRefund != refundPaymentRequest.Order.OrderTotal
                ? (decimal?)refundPaymentRequest.AmountToRefund
                : null;
            var (completed, error) = _serviceManager.Refund(refundPaymentRequest.Order, amount);

            if (!string.IsNullOrEmpty(error))
                return new RefundPaymentResult { Errors = new[] { error } };

            var result = new RefundPaymentResult
            {
                NewPaymentStatus = completed
                    ? (refundPaymentRequest.IsPartialRefund
                    ? PaymentStatus.PartiallyRefunded
                    : PaymentStatus.Refunded)
                    : refundPaymentRequest.Order.PaymentStatus
            };

            //the refund is pending, actually it'll be completed upon receiving successful refund status report
            if (!completed)
                result.AddError(_localizationService.GetResource("Plugins.Payments.Skrill.Refund.Warning"));

            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return decimal.Zero;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(Defaults.ConfigurationRouteName);
        }

        /// <summary>
        /// Gets a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <param name="viewComponentName">View component name</param>
        public string GetPublicViewComponentName()
        {
            return null;
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new SkrillSettings());

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Credentials.Valid", "Specified credentials are valid");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Credentials.Invalid", "Specified email and password are invalid (see details in the <a href=\"{0}\" target=\"_blank\">log</a>)");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.MerchantEmail", "Merchant email");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.MerchantEmail.Hint", "Enter email address of your Skrill merchant account.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.MerchantEmail.Required", "Merchant email is required");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.Password", "API/MQI password");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.Password.Hint", "Enter your API/MQI password.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord", "Secret word");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord.Hint", "Enter your secret word.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord.Required", "Secret word is required");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.PaymentMethodDescription", "You will be redirected to Skrill to complete the payment");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Refund.Warning", "The refund is pending, actually it'll be completed upon receiving successful refund status report.");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<SkrillSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Credentials.Valid");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Credentials.Invalid");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.MerchantEmail");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.MerchantEmail.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.MerchantEmail.Required");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.Password");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.Password.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord.Required");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.PaymentMethodDescription");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Skrill.Refund.Warning");

            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => false;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => true;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => true;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => false;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => true;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.Skrill.PaymentMethodDescription");

        #endregion
    }
}