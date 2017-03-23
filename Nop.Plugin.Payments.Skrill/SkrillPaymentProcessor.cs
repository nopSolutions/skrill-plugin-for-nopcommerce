using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Skrill.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework;

namespace Nop.Plugin.Payments.Skrill
{
    /// <summary>
    /// Skrill payment processor
    /// </summary>
    public class SkrillPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly SkrillPaymentSettings _skrillPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly ILocalizationService _localizationService;

        private const string SKRILL_URL = "https://www.moneybookers.com/app/payment.pl";
        #endregion

        #region Ctor

        public SkrillPaymentProcessor(SkrillPaymentSettings skrillPaymentSettings,
            ISettingService settingService, IOrderTotalCalculationService orderTotalCalculationService, 
            IStoreContext storeContext, IWebHelper webHelper,
            ICurrencyService currencyService, CurrencySettings currencySettings,
            ILocalizationService localizationService)
        {
            this._skrillPaymentSettings = skrillPaymentSettings;
            this._settingService = settingService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._storeContext = storeContext;
            this._webHelper = webHelper;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._localizationService = localizationService;
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
            var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var order = postProcessPaymentRequest.Order;
            var gatewayUrl = new Uri(SKRILL_URL);

            var remotePostHelper = new RemotePost
            {
                Url = gatewayUrl.ToString(),
                Method = "POST",
                FormName = "SkrillForm"
            };

            remotePostHelper.Add("pay_to_email", _skrillPaymentSettings.PayToEmail);
            remotePostHelper.Add("pay_from_email", order.Customer.Email);
            remotePostHelper.Add("recipient_description", _storeContext.CurrentStore.Name);
            remotePostHelper.Add("transaction_id", order.Id.ToString());
            remotePostHelper.Add("return_url", _webHelper.GetStoreLocation() + "checkout/completed?order_id=" + order.Id);
            remotePostHelper.Add("cancel_url", _webHelper.GetStoreLocation());
            remotePostHelper.Add("status_url", _webHelper.GetStoreLocation() + "Plugins/PaymentSkrill/ResponseNotificationHandler");
            //remotePostHelper.Add("status_url", "http://www.nopcommerce.com/recordquerytest.aspx");
            //supported languages (EN, DE, ES, FR, IT, PL, GR, RO, RU, TR, CN, CZ or NL)
            remotePostHelper.Add("language", "EN");
            remotePostHelper.Add("amount", order.OrderTotal.ToString(new CultureInfo("en-US", false).NumberFormat));
            remotePostHelper.Add("currency", _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode);
            remotePostHelper.Add("detail1_description", "Order ID:");
            remotePostHelper.Add("detail1_text", order.Id.ToString());

            remotePostHelper.Add("firstname", order.BillingAddress.FirstName);
            remotePostHelper.Add("lastname", order.BillingAddress.LastName);
            remotePostHelper.Add("address", order.BillingAddress.Address1);
            remotePostHelper.Add("phone_number", order.BillingAddress.PhoneNumber);
            remotePostHelper.Add("postal_code", order.BillingAddress.ZipPostalCode);
            remotePostHelper.Add("city", order.BillingAddress.City);

            remotePostHelper.Add("state",
                order.BillingAddress.StateProvince != null
                    ? HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.StateProvince.Abbreviation)
                    : "");
            remotePostHelper.Add("country",
                postProcessPaymentRequest.Order.BillingAddress.Country != null
                    ? HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Country.TwoLetterIsoCode)
                    : "");

            remotePostHelper.Post();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _skrillPaymentSettings.AdditionalFee, _skrillPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");
            
            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentSkrill";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Skrill.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentSkrill";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Skrill.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentSkrillController);
        }

        public override void Install()
        {
            //settings
            var settings = new SkrillPaymentSettings
            {
                PayToEmail = "name@yourStore.com",
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.RedirectionTip", "You will be redirected to Skrill site to complete the order.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.PayToEmail", "Pay to email");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.PayToEmail.Hint", "Pay to email.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord", "Secret word");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord.Hint", "Secret word.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFee", "Additional fee"); 
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Skrill.PaymentMethodDescription", "You will be redirected to Skrill site to complete the order.");

            base.Install();
        }
        
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<SkrillPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.PayToEmail");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.PayToEmail.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.SecretWord.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Skrill.PaymentMethodDescription");
            
            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            get { return _localizationService.GetResource("Plugins.Payments.Skrill.PaymentMethodDescription"); }
        }

        #endregion
    }
}
