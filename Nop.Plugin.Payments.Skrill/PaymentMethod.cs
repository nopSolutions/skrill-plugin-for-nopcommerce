using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.Skrill.Domain;
using Nop.Plugin.Payments.Skrill.Services;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Payments.Skrill
{
    /// <summary>
    /// Represents a payment method implementation
    /// </summary>
    public class PaymentMethod : BasePlugin, IPaymentMethod, IWidgetPlugin
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICustomerService _customerService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly CurrencySettings _currencySettings;
        private readonly ServiceManager _serviceManager;
        private readonly WidgetSettings _widgetSettings;

        #endregion

        #region Ctor

        public PaymentMethod(IActionContextAccessor actionContextAccessor,
            ICustomerService customerService,
            ICurrencyService currencyService,
            ILocalizationService localizationService,
            INotificationService notificationService,
            ISettingService settingService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IGenericAttributeService genericAttributeService,
            IUrlHelperFactory urlHelperFactory,
            CurrencySettings currencySettings,
            ServiceManager serviceManager,
            WidgetSettings widgetSettings)
        {
            _actionContextAccessor = actionContextAccessor;
            _customerService = customerService;
            _currencyService = currencyService;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _settingService = settingService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _genericAttributeService = genericAttributeService;
            _urlHelperFactory = urlHelperFactory;
            _currencySettings = currencySettings;
            _serviceManager = serviceManager;
            _widgetSettings = widgetSettings;
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
            switch (_serviceManager.GetPaymentFlowType())
            {
                case PaymentFlowType.Redirection:
                    var redirectUrl = _serviceManager.PrepareCheckoutUrl(postProcessPaymentRequest);
                    if (string.IsNullOrEmpty(redirectUrl))
                    {
                        redirectUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                            .RouteUrl(Defaults.OrderDetailsRouteName, new { orderId = postProcessPaymentRequest.Order.Id });
                        _notificationService.ErrorNotification("Something went wrong, contact the store manager");
                    }
                    _actionContextAccessor.ActionContext.HttpContext.Response.Redirect(redirectUrl);
                    return;

                case PaymentFlowType.Inline:
                    var customer = _customerService.GetCustomerById(postProcessPaymentRequest.Order.CustomerId);
                    if (customer != null)
                    {
                        var order = postProcessPaymentRequest.Order;
                        var transactionId = _genericAttributeService.GetAttribute<string>(customer, Defaults.PaymentTransactionIdAttribute);
                        if (!string.IsNullOrEmpty(transactionId) && _orderProcessingService.CanMarkOrderAsPaid(order))
                        {
                            order.CaptureTransactionId = transactionId;
                            _orderService.UpdateOrder(order);
                            _orderProcessingService.MarkOrderAsPaid(order);

                            _genericAttributeService.SaveAttribute<string>(customer, Defaults.PaymentTransactionIdAttribute, null);
                        }
                    }
                    return;
            }
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
            // null to refund full amount
            decimal? amountToRefund = null;

            if (refundPaymentRequest.AmountToRefund != refundPaymentRequest.Order.OrderTotal)
            {
                amountToRefund = refundPaymentRequest.AmountToRefund;

                //try convert to Skrill account currency
                var capturedTransactionId = refundPaymentRequest.Order.CaptureTransactionId;
                var (currencyCode, currencyCodeError) = _serviceManager.GetTransactionCurrencyCode(capturedTransactionId);
                if (!string.IsNullOrEmpty(currencyCodeError))
                    return new RefundPaymentResult { Errors = new[] { currencyCodeError } };

                var primaryCurrency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);
                if (!primaryCurrency.CurrencyCode.Equals(currencyCode, StringComparison.InvariantCultureIgnoreCase))
                {
                    var skrillCurrency = _currencyService.GetCurrencyByCode(currencyCode);
                    if (skrillCurrency != null)
                        amountToRefund = _currencyService.ConvertCurrency(amountToRefund.Value, primaryCurrency, skrillCurrency);
                    else
                    {
                        var currencyError = $"Cannot convert the refund amount to Skrill currency {currencyCode}. Currency ({currencyCode}) not install.";
                        return new RefundPaymentResult { Errors = new[] { currencyError } };
                    }
                }
            }

            var (completed, error) = _serviceManager.Refund(refundPaymentRequest.Order, amountToRefund);

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
            return _serviceManager.GetPaymentFlowType() == PaymentFlowType.Redirection;
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
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            if (_serviceManager.GetPaymentFlowType() == PaymentFlowType.Inline)
                //already set
                return _actionContextAccessor.ActionContext.HttpContext.Session.Get<ProcessPaymentRequest>(Defaults.PaymentRequestSessionKey);

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
            return _serviceManager.GetPaymentFlowType() == PaymentFlowType.Inline
                ? Defaults.PAYMENT_INFO_VIEW_COMPONENT_NAME
                : null;
        }

        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>Widget zones</returns>
        public IList<string> GetWidgetZones()
        {
            return new List<string> { AdminWidgetZones.OrderDetailsBlock };
        }

        /// <summary>
        /// Gets a name of a view component for displaying widget
        /// </summary>
        /// <param name="widgetZone">Name of the widget zone</param>
        /// <returns>View component name</returns>
        public string GetWidgetViewComponentName(string widgetZone)
        {
            if (widgetZone == null)
                throw new ArgumentNullException(nameof(widgetZone));

            if (widgetZone.Equals(AdminWidgetZones.OrderDetailsBlock))
                return Defaults.REFUND_HINTS_VIEW_COMPONENT_NAME;

            return string.Empty;
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new SkrillSettings
            {
                PaymentFlowType = PaymentFlowType.Inline,
                RequestTimeout = 10
            });

            if (!_widgetSettings.ActiveWidgetSystemNames.Contains(Defaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Add(Defaults.SystemName);
                _settingService.SaveSetting(_widgetSettings);
            }

            //locales
            _localizationService.AddPluginLocaleResource(new Dictionary<string, string>
            {
                ["Enums.Nop.Plugin.Payments.Skrill.Domain.PaymentFlowType.Redirection"] = "On the Skrill side",
                ["Enums.Nop.Plugin.Payments.Skrill.Domain.PaymentFlowType.Inline"] = "On the Merchant side",
                ["Plugins.Payments.Skrill.Credentials.Valid"] = "Specified credentials are valid",
                ["Plugins.Payments.Skrill.Credentials.Invalid"] = "Specified email and password are invalid (see details in the <a href=\"{0}\" target=\"_blank\">log</a>)",
                ["Plugins.Payments.Skrill.Fields.MerchantEmail"] = "Merchant email",
                ["Plugins.Payments.Skrill.Fields.MerchantEmail.Hint"] = "Enter email address of your Skrill merchant account.",
                ["Plugins.Payments.Skrill.Fields.MerchantEmail.Required"] = "Merchant email is required",
                ["Plugins.Payments.Skrill.Fields.Password"] = "API/MQI password",
                ["Plugins.Payments.Skrill.Fields.Password.Hint"] = "Insert API/MQI password created in your Skrill merchants account settings.",
                ["Plugins.Payments.Skrill.Fields.SecretWord"] = "Secret word",
                ["Plugins.Payments.Skrill.Fields.SecretWord.Hint"] = "Insert secret word created in your Skrill merchants account settings.",
                ["Plugins.Payments.Skrill.Fields.SecretWord.Required"] = "Secret word is required",
                ["Plugins.Payments.Skrill.Fields.PaymentFlowType"] = "Payment flow",
                ["Plugins.Payments.Skrill.Fields.PaymentFlowType.Hint"] = "Select a payment flow. Choose option 'On the Skrill side' to redirect customers to the Skrill website to make a payment; choose option 'On the Merchant side' to embed the Skrill Quick Checkout page in checkout process, in this case customers make a payment by staying on your website.",
                ["Plugins.Payments.Skrill.PaymentMethodDescription"] = "Pay by Skrill Quick Checkout",
                ["Plugins.Payments.Skrill.Refund.Offline.Hint"] = "This option only puts transactions which are refunded in Skrill merchant account in the same status (refunded) in your nopCommerce store.",
                ["Plugins.Payments.Skrill.Refund.Warning"] = "The refund is pending, actually it'll be completed upon receiving successful refund status report.",
                ["Plugins.Payments.Skrill.Payment.Successful"] = "We have received your payment. Thanks!",
                ["Plugins.Payments.Skrill.Payment.Invalid"] = "Payment transaction is invalid.",
            });

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            if (_widgetSettings.ActiveWidgetSystemNames.Contains(Defaults.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Remove(Defaults.SystemName);
                _settingService.SaveSetting(_widgetSettings);
            }
            _settingService.DeleteSetting<SkrillSettings>();

            //locales
            _localizationService.DeletePluginLocaleResources("Plugins.Payments.Skrill");
            _localizationService.DeletePluginLocaleResources("Enums.Nop.Plugin.Payments.Skrill");

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
        public PaymentMethodType PaymentMethodType => (_serviceManager.GetPaymentFlowType()) switch
        {
            PaymentFlowType.Redirection => PaymentMethodType.Redirection,
            PaymentFlowType.Inline => PaymentMethodType.Standard,
            _ => throw new InvalidOperationException($"Cannot convert {nameof(PaymentFlowType)}."),
        };

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => _serviceManager.GetPaymentFlowType() == PaymentFlowType.Redirection;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.Skrill.PaymentMethodDescription");

        /// <summary>
        /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
        /// </summary>
        public bool HideInWidgetList => true;

        #endregion
    }
}