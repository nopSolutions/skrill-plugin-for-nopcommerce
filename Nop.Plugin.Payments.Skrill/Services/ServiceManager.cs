using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Skrill.Domain;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Skrill.Services
{
    /// <summary>
    /// Represents the service manager
    /// </summary>
    public class ServiceManager
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IProductService _productService;
        private readonly IOrderService _orderService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreContext _storeContext;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ServiceHttpClient _httpClient;
        private readonly SkrillSettings _settings;

        #endregion

        #region Ctor

        public ServiceManager(CurrencySettings currencySettings,
            IActionContextAccessor actionContextAccessor,
            IAddressService addressService,
            ICountryService countryService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            ILanguageService languageService,
            ILocalizationService localizationService,
            ILogger logger,
            IProductService productService,
            IOrderService orderService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IStateProvinceService stateProvinceService,
            IStoreContext storeContext,
            IShoppingCartService shoppingCartService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper,
            IWorkContext workContext,
            ServiceHttpClient httpClient,
            SkrillSettings settings)
        {
            _currencySettings = currencySettings;
            _actionContextAccessor = actionContextAccessor;
            _addressService = addressService;
            _countryService = countryService;
            _currencyService = currencyService;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _languageService = languageService;
            _localizationService = localizationService;
            _logger = logger;
            _productService = productService;
            _orderService = orderService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _stateProvinceService = stateProvinceService;
            _storeContext = storeContext;
            _shoppingCartService = shoppingCartService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
            _workContext = workContext;
            _httpClient = httpClient;
            _settings = settings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Check whether the plugin is configured
        /// </summary>
        /// <returns>Result</returns>
        private bool IsConfigured()
        {
            //merchant email and secret word are required to request services
            return !string.IsNullOrEmpty(_settings.MerchantEmail) && !string.IsNullOrEmpty(_settings.SecretWord);
        }

        /// <summary>
        /// Handle function and get result
        /// </summary>
        /// <typeparam name="TResult">Result type</typeparam>
        /// <param name="function">Function</param>
        /// <returns>Result; error message if exists</returns>
        private (TResult Result, string ErrorMessage) HandleFunction<TResult>(Func<TResult> function)
        {
            try
            {
                //ensure that plugin is configured
                if (!IsConfigured())
                    throw new NopException("Plugin not configured");

                //invoke function
                return (function(), default);
            }
            catch (Exception exception)
            {
                //log errors
                var errorMessage = $"{Defaults.SystemName} error: {Environment.NewLine}{exception.Message}";
                _logger.Error(errorMessage, exception, _workContext.CurrentCustomer);

                return (default, errorMessage);
            }
        }

        /// <summary>
        /// Convert string to MD5 hash
        /// </summary>
        /// <param name="value">Input string</param>
        /// <returns>Hash</returns>
        private string ToMD5(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var hash = new StringBuilder();
            new MD5CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(value)).ToList().ForEach(bytes => hash.AppendFormat("{0:X2}", bytes));
            return hash.ToString();
        }

        /// <summary>
        /// Prepare parameters to request the Quick Checkout service
        /// </summary>
        /// <param name="request">Payment info required for an order processing</param>
        /// <returns>URL to request</returns>
        private string PrepareSessionRequestUrl(PostProcessPaymentRequest request)
        {
            var order = request.Order;
            if (order == null)
                throw new NopException("Order is not set");

            var customer = _customerService.GetCustomerById(order.CustomerId);
            if (customer == null)
                throw new NopException("Order customer is not set");

            var billingAddress = _addressService.GetAddressById(order.BillingAddressId);
            if (billingAddress == null)
                throw new NopException("Order billing address is not set");

            var billingCountryThreeLetterIsoCode = string.Empty;
            if (billingAddress.CountryId.HasValue)
                billingCountryThreeLetterIsoCode = _countryService.GetCountryById(billingAddress.CountryId.Value)?.ThreeLetterIsoCode;

            var billingStateProvinceName = string.Empty;
            if (billingAddress.StateProvinceId.HasValue)
                billingStateProvinceName = _stateProvinceService.GetStateProvinceById(billingAddress.StateProvinceId.Value)?.Name;

            var currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);
            if (currency == null)
                throw new NopException("Primary store currency is not set");

            //prepare URLs
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
            var successUrl = urlHelper.RouteUrl(Defaults.CheckoutCompletedRouteName, new { orderId = order.Id }, _webHelper.CurrentRequestProtocol);
            var failUrl = urlHelper.RouteUrl(Defaults.OrderDetailsRouteName, new { orderId = order.Id }, _webHelper.CurrentRequestProtocol);
            var webhookUrl = urlHelper.RouteUrl(Defaults.QuickCheckoutWebhookRouteName, null, _webHelper.CurrentRequestProtocol);

            //prepare some customer details
            var customerLanguage = _languageService.GetLanguageById(order.CustomerLanguageId) ?? _workContext.WorkingLanguage;
            var dateOfBirthValue = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.DateOfBirthAttribute);
            var customerDateOfBirth = DateTime.TryParse(dateOfBirthValue, out var dateOfBirth) ? dateOfBirth.ToString("ddMMyyyy") : string.Empty;

            //prepare some item details
            var orderItems = _orderService.GetOrderItems(order.Id);
            var item1 = orderItems.FirstOrDefault();
            var product1 = item1 != null ? _productService.GetProductById(item1.ProductId) : null;
            var detailDescription1 = "Product name:";
            var detailText1 = product1 != null ? _localizationService.GetLocalized(product1, entity => entity.Name) : string.Empty;

            var item2 = orderItems.Skip(1).FirstOrDefault();
            var product2 = item2 != null ? _productService.GetProductById(item2.ProductId) : null;
            var detailDescription2 = product2 != null ? detailDescription1 : "Product description:";
            var detailText2 = product2 != null
                ? _localizationService.GetLocalized(product2, entity => entity.Name)
                : (product1 != null ? _localizationService.GetLocalized(product1, entity => entity.ShortDescription) : string.Empty);

            var item3 = orderItems.Skip(2).FirstOrDefault();
            var product3 = item3 != null ? _productService.GetProductById(item3.ProductId) : null;
            var detailDescription3 = product3 != null ? detailDescription1 : (product2 != null ? string.Empty : "Quantity:");
            var detailText3 = product3 != null
                ? _localizationService.GetLocalized(product3, entity => entity.Name)
                : (product2 != null
                ? string.Empty
                : item1?.Quantity.ToString());

            var item4 = orderItems.Skip(3).FirstOrDefault();
            var product4 = item4 != null ? _productService.GetProductById(item4.ProductId) : null;
            var detailDescription4 = product4 != null ? detailDescription1 : string.Empty;
            var detailText4 = product4 != null ? _localizationService.GetLocalized(product4, entity => entity.Name) : string.Empty;

            var item5 = orderItems.Skip(4).FirstOrDefault();
            var product5 = item5 != null ? _productService.GetProductById(item5.ProductId) : null;
            var detailDescription5 = product5 != null ? detailDescription1 : string.Empty;
            var detailText5 = product5 != null ? _localizationService.GetLocalized(product5, entity => entity.Name) : string.Empty;

            //prepare URL to request
            var url = QueryHelpers.AddQueryString(Defaults.QuickCheckoutServiceUrl, new Dictionary<string, string>
            {
                //merchant details
                ["pay_to_email"] = CommonHelper.EnsureMaximumLength(_settings.MerchantEmail, 50) ?? string.Empty,
                ["recipient_description"] = CommonHelper.EnsureMaximumLength(_storeContext.CurrentStore.Name, 30) ?? string.Empty,
                ["transaction_id"] = order.OrderGuid.ToString(),
                ["return_url"] = CommonHelper.EnsureMaximumLength(successUrl, 240) ?? string.Empty,
                ["return_url_text"] = CommonHelper.EnsureMaximumLength($"Back to {_storeContext.CurrentStore.Name}", 35) ?? string.Empty,
                //["return_url_target"] = "1", //default value
                ["cancel_url"] = CommonHelper.EnsureMaximumLength(failUrl, 240) ?? string.Empty,
                //["cancel_url_target"] = "1", //default value
                ["status_url"] = CommonHelper.EnsureMaximumLength(webhookUrl, 400) ?? string.Empty,
                //["status_url2"] = null, //single webhook handler is enough
                ["language"] = CommonHelper.EnsureMaximumLength(customerLanguage?.UniqueSeoCode ?? "EN", 2) ?? string.Empty,
                //["logo_url"] = null, //not used, only store name will be shown
                ["prepare_only"] = "1", //first, prepare the order details
                ["dynamic_descriptor "] = CommonHelper.EnsureMaximumLength(_storeContext.CurrentStore.Name, 25) ?? string.Empty,
                //["sid"] = null, //used in the next request
                //["rid"] = CommonHelper.EnsureMaximumLength(Defaults.ReferralId, 100) ?? string.Empty, //according to Skrill managers "referral ID" should be passed in additional merchant fields, well ok
                //["ext_ref_id"] = CommonHelper.EnsureMaximumLength(Defaults.UserAgent, 100) ?? string.Empty, //according to Skrill managers "referral ID" should be passed in additional merchant fields, well ok
                ["merchant_fields"] = CommonHelper.EnsureMaximumLength("platform,platform_version", 240),
                ["platform"] = CommonHelper.EnsureMaximumLength(Defaults.ReferralId, 240),
                ["platform_version"] = CommonHelper.EnsureMaximumLength(NopVersion.CurrentVersion, 240),

                //customer details
                ["pay_from_email"] = CommonHelper.EnsureMaximumLength(billingAddress.Email, 100) ?? string.Empty,
                ["firstname"] = CommonHelper.EnsureMaximumLength(billingAddress.FirstName, 20) ?? string.Empty,
                ["lastname"] = CommonHelper.EnsureMaximumLength(billingAddress.LastName, 50) ?? string.Empty,
                ["date_of_birth"] = CommonHelper.EnsureMaximumLength(customerDateOfBirth, 8) ?? string.Empty,
                ["address"] = CommonHelper.EnsureMaximumLength(billingAddress.Address1, 100) ?? string.Empty,
                ["address2"] = CommonHelper.EnsureMaximumLength(billingAddress.Address2, 100) ?? string.Empty,
                ["phone_number"] = CommonHelper.EnsureMaximumLength(billingAddress.PhoneNumber, 20) ?? string.Empty,
                ["postal_code"] = CommonHelper.EnsureMaximumLength(billingAddress.ZipPostalCode, 9) ?? string.Empty,
                ["city"] = CommonHelper.EnsureMaximumLength(billingAddress.City, 50) ?? string.Empty,
                ["state"] = CommonHelper.EnsureMaximumLength(billingStateProvinceName, 50) ?? string.Empty,
                ["country"] = CommonHelper.EnsureMaximumLength(billingCountryThreeLetterIsoCode, 3) ?? string.Empty,
                //["neteller_account"] = null, //not used
                //["neteller_secure_id"] = null, // not used

                //payment details
                ["amount"] = CommonHelper.EnsureMaximumLength(order.OrderTotal.ToString("F").TrimEnd('0').TrimEnd('0').TrimEnd('.'), 19) ?? string.Empty,
                ["currency"] = CommonHelper.EnsureMaximumLength(currency?.CurrencyCode, 3) ?? string.Empty,
                ["amount2_description "] = CommonHelper.EnsureMaximumLength("Item total:", 240),
                ["amount2"] = CommonHelper.EnsureMaximumLength(order.OrderSubtotalExclTax.ToString("F").TrimEnd('0').TrimEnd('0').TrimEnd('.'), 19) ?? string.Empty,
                ["amount3_description "] = CommonHelper.EnsureMaximumLength("Shipping total:", 240) ?? string.Empty,
                ["amount3"] = CommonHelper.EnsureMaximumLength(order.OrderShippingExclTax.ToString("F").TrimEnd('0').TrimEnd('0').TrimEnd('.'), 19) ?? string.Empty,
                ["amount4_description "] = CommonHelper.EnsureMaximumLength("Tax total:", 240) ?? string.Empty,
                ["amount4"] = CommonHelper.EnsureMaximumLength(order.OrderTax.ToString("F").TrimEnd('0').TrimEnd('0').TrimEnd('.'), 19) ?? string.Empty,
                ["detail1_description"] = CommonHelper.EnsureMaximumLength(detailDescription1, 240) ?? string.Empty,
                ["detail1_text"] = CommonHelper.EnsureMaximumLength(detailText1, 240) ?? string.Empty,
                ["detail2_description"] = CommonHelper.EnsureMaximumLength(detailDescription2, 240) ?? string.Empty,
                ["detail2_text"] = CommonHelper.EnsureMaximumLength(detailText2, 240) ?? string.Empty,
                ["detail3_description"] = CommonHelper.EnsureMaximumLength(detailDescription3, 240) ?? string.Empty,
                ["detail3_text"] = CommonHelper.EnsureMaximumLength(detailText3, 240) ?? string.Empty,
                ["detail4_description"] = CommonHelper.EnsureMaximumLength(detailDescription4, 240) ?? string.Empty,
                ["detail4_text"] = CommonHelper.EnsureMaximumLength(detailText4, 240) ?? string.Empty,
                ["detail5_description"] = CommonHelper.EnsureMaximumLength(detailDescription5, 240) ?? string.Empty,
                ["detail5_text"] = CommonHelper.EnsureMaximumLength(detailText5, 240) ?? string.Empty
            });

            return url;
        }

        /// <summary>
        /// Prepare parameters to request the Quick Checkout service
        /// </summary>
        /// <param name="orderGuid">Order GUID</param>
        /// <returns>URL to request</returns>
        private string PrepareSessionRequestUrl(Guid orderGuid)
        {
            var customer = _workContext.CurrentCustomer;

            var billingAddress = _customerService.GetCustomerBillingAddress(customer);
            if (billingAddress == null)
                throw new NopException("Order billing address is not set");

            var currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);
            if (currency == null)
                throw new NopException("Primary store currency is not set");

            var billingCountryThreeLetterIsoCode = string.Empty;
            if (billingAddress.CountryId.HasValue)
                billingCountryThreeLetterIsoCode = _countryService.GetCountryById(billingAddress.CountryId.Value)?.ThreeLetterIsoCode;

            var billingStateProvinceName = string.Empty;
            if (billingAddress.StateProvinceId.HasValue)
                billingStateProvinceName = _stateProvinceService.GetStateProvinceById(billingAddress.StateProvinceId.Value)?.Name;

            //prepare URLs
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
            var successUrl = urlHelper.RouteUrl(Defaults.OrderPaidWebhookRouteName, null, _webHelper.CurrentRequestProtocol);
            var webhookUrl = urlHelper.RouteUrl(Defaults.QuickCheckoutWebhookRouteName, null, _webHelper.CurrentRequestProtocol);

            //prepare some customer details
            var customerLanguage = _workContext.WorkingLanguage;
            var dateOfBirthValue = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.DateOfBirthAttribute);
            var customerDateOfBirth = DateTime.TryParse(dateOfBirthValue, out var dateOfBirth) ? dateOfBirth.ToString("ddMMyyyy") : string.Empty;

            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);
            var orderTotal = _orderTotalCalculationService.GetShoppingCartTotal(cart, usePaymentMethodAdditionalFee: false) ?? decimal.Zero;

            var item1 = cart.FirstOrDefault();
            var product1 = item1 != null ? _productService.GetProductById(item1.ProductId) : null;
            var detailDescription1 = "Product name:";
            var detailText1 = product1 != null ? _localizationService.GetLocalized(product1, entity => entity.Name) : string.Empty;

            var item2 = cart.Skip(1).FirstOrDefault();
            var product2 = item2 != null ? _productService.GetProductById(item2.ProductId) : null;
            var detailDescription2 = product2 != null ? detailDescription1 : "Product description:";
            var detailText2 = product2 != null
                ? _localizationService.GetLocalized(product2, entity => entity.Name)
                : (product1 != null ? _localizationService.GetLocalized(product1, entity => entity.ShortDescription) : string.Empty);

            var item3 = cart.Skip(2).FirstOrDefault();
            var product3 = item3 != null ? _productService.GetProductById(item3.ProductId) : null;
            var detailDescription3 = product3 != null ? detailDescription1 : (product2 != null ? string.Empty : "Quantity:");
            var detailText3 = product3 != null
                ? _localizationService.GetLocalized(product3, entity => entity.Name)
                : (product2 != null
                ? string.Empty
                : item1?.Quantity.ToString());

            var item4 = cart.Skip(3).FirstOrDefault();
            var product4 = item4 != null ? _productService.GetProductById(item4.ProductId) : null;
            var detailDescription4 = product4 != null ? detailDescription1 : string.Empty;
            var detailText4 = product4 != null ? _localizationService.GetLocalized(product4, entity => entity.Name) : string.Empty;

            var item5 = cart.Skip(4).FirstOrDefault();
            var product5 = item5 != null ? _productService.GetProductById(item5.ProductId) : null;
            var detailDescription5 = product5 != null ? detailDescription1 : string.Empty;
            var detailText5 = product5 != null ? _localizationService.GetLocalized(product5, entity => entity.Name) : string.Empty;

            var query = new Dictionary<string, string>
            {
                ["pay_to_email"] = CommonHelper.EnsureMaximumLength(_settings.MerchantEmail, 50) ?? string.Empty,
                ["recipient_description"] = CommonHelper.EnsureMaximumLength(_storeContext.CurrentStore.Name, 30) ?? string.Empty,
                ["transaction_id"] = orderGuid.ToString(),
                ["status_url"] = CommonHelper.EnsureMaximumLength(webhookUrl, 400) ?? string.Empty,
                //["status_url2"] = string.Empty, //single webhook handler is enough
                ["language"] = CommonHelper.EnsureMaximumLength(customerLanguage?.UniqueSeoCode ?? "EN", 2) ?? string.Empty,
                //["logo_url"] = null, //not used, only store name will be shown
                ["prepare_only"] = "1", //first, prepare the order details
                ["dynamic_descriptor"] = CommonHelper.EnsureMaximumLength(_storeContext.CurrentStore.Name, 25) ?? string.Empty,
                //["sid"] = null, //used in the next request
                //["rid"] = CommonHelper.EnsureMaximumLength(Defaults.ReferralId, 100) ?? string.Empty, //according to Skrill managers "referral ID" should be passed in additional merchant fields, well ok
                //["ext_ref_id"] = CommonHelper.EnsureMaximumLength(Defaults.UserAgent, 100) ?? string.Empty, //according to Skrill managers "referral ID" should be passed in additional merchant fields, well ok
                ["merchant_fields"] = CommonHelper.EnsureMaximumLength("platform,platform_version,nop_customer_id", 240),
                ["platform"] = CommonHelper.EnsureMaximumLength(Defaults.ReferralId, 240),
                ["platform_version"] = CommonHelper.EnsureMaximumLength(NopVersion.CurrentVersion, 240),
                ["return_url_target"] = "3", // opens the target URL in the same frame as the payment form.
                ["return_url"] = CommonHelper.EnsureMaximumLength(successUrl, 240) ?? string.Empty,

                //customer details
                ["pay_from_email"] = CommonHelper.EnsureMaximumLength(billingAddress.Email, 100) ?? string.Empty,
                ["firstname"] = CommonHelper.EnsureMaximumLength(billingAddress.FirstName, 20) ?? string.Empty,
                ["lastname"] = CommonHelper.EnsureMaximumLength(billingAddress.LastName, 50) ?? string.Empty,
                ["date_of_birth"] = CommonHelper.EnsureMaximumLength(customerDateOfBirth, 8) ?? string.Empty,
                ["address"] = CommonHelper.EnsureMaximumLength(billingAddress.Address1, 100) ?? string.Empty,
                ["address2"] = CommonHelper.EnsureMaximumLength(billingAddress.Address2, 100) ?? string.Empty,
                ["phone_number"] = CommonHelper.EnsureMaximumLength(billingAddress.PhoneNumber, 20) ?? string.Empty,
                ["postal_code"] = CommonHelper.EnsureMaximumLength(billingAddress.ZipPostalCode, 9) ?? string.Empty,
                ["city"] = CommonHelper.EnsureMaximumLength(billingAddress.City, 50) ?? string.Empty,
                ["state"] = CommonHelper.EnsureMaximumLength(billingStateProvinceName, 50) ?? string.Empty,
                ["country"] = CommonHelper.EnsureMaximumLength(billingCountryThreeLetterIsoCode, 3) ?? string.Empty,
                ["nop_customer_id"] = customer.Id.ToString(),

                //payment details
                ["amount"] = CommonHelper.EnsureMaximumLength(orderTotal.ToString("F").TrimEnd('0').TrimEnd('0').TrimEnd('.'), 19) ?? string.Empty,
                ["currency"] = CommonHelper.EnsureMaximumLength(currency?.CurrencyCode, 3) ?? string.Empty,
                ["amount2_description "] = CommonHelper.EnsureMaximumLength("Item total:", 240),

                ["detail1_description"] = CommonHelper.EnsureMaximumLength(detailDescription1, 240) ?? string.Empty,
                ["detail1_text"] = CommonHelper.EnsureMaximumLength(detailText1, 240) ?? string.Empty,
                ["detail2_description"] = CommonHelper.EnsureMaximumLength(detailDescription2, 240) ?? string.Empty,
                ["detail2_text"] = CommonHelper.EnsureMaximumLength(detailText2, 240) ?? string.Empty,
                ["detail3_description"] = CommonHelper.EnsureMaximumLength(detailDescription3, 240) ?? string.Empty,
                ["detail3_text"] = CommonHelper.EnsureMaximumLength(detailText3, 240) ?? string.Empty,
                ["detail4_description"] = CommonHelper.EnsureMaximumLength(detailDescription4, 240) ?? string.Empty,
                ["detail4_text"] = CommonHelper.EnsureMaximumLength(detailText4, 240) ?? string.Empty,
                ["detail5_description"] = CommonHelper.EnsureMaximumLength(detailDescription5, 240) ?? string.Empty,
                ["detail5_text"] = CommonHelper.EnsureMaximumLength(detailText5, 240) ?? string.Empty,
            };

            //return URL to request session id
            return QueryHelpers.AddQueryString(Defaults.QuickCheckoutServiceUrl, query);
        }

        /// <summary>
        /// Prepare parameters to request the refund service
        /// </summary>
        /// <param name="refundedOrder">Order to refund</param>
        /// <param name="refundedAmount">Amount to refund</param>
        /// <returns>URL to request</returns>
        private string PrepareRefundParameters(Order refundedOrder, decimal? refundedAmount)
        {
            if (refundedOrder == null)
                throw new NopException("Order is not set");

            if (string.IsNullOrEmpty(refundedOrder.CaptureTransactionId))
                throw new NopException("Transaction ID is not set");

            //prepare URLs
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
            var webhookUrl = urlHelper.RouteUrl(Defaults.RefundWebhookRouteName, null, _webHelper.CurrentRequestProtocol);

            //prepare unique refund identifier to avoid duplicated refunding
            var refundGuid = Guid.NewGuid().ToString().ToLowerInvariant();
            _genericAttributeService.SaveAttribute(refundedOrder, Defaults.RefundGuidAttribute, refundGuid);

            //prepare URL to request
            var url = QueryHelpers.AddQueryString(Defaults.RefundServiceUrl, new Dictionary<string, string>
            {
                //merchant details
                ["action"] = "prepare" ?? string.Empty,
                ["email"] = _settings.MerchantEmail ?? string.Empty,
                ["password"] = ToMD5(_settings.Password).ToLowerInvariant() ?? string.Empty,
                ["transaction_id"] = refundedOrder.OrderGuid.ToString() ?? string.Empty,
                ["mb_transaction_id"] = refundedOrder.CaptureTransactionId ?? string.Empty,
                ["amount"] = refundedAmount?.ToString("F").TrimEnd('0').TrimEnd('0').TrimEnd('.') ?? string.Empty,
                ["merchant_fields"] = CommonHelper.EnsureMaximumLength("refund_guid", 240),
                ["refund_guid"] = CommonHelper.EnsureMaximumLength(refundGuid, 240),
                ["refund_status_url"] = webhookUrl ?? string.Empty,
                //["refund_note"] = null, // not used
            });

            return url;
        }

        /// <summary>
        /// Prepare URL to complete checkout
        /// </summary>
        /// <param name="sessionRequestUrl">URL to request session details</param>
        /// <returns>URL</returns>
        private string PrepareCheckoutUrl(string sessionRequestUrl)
        {
            //first prepare checkout and get session id
            var sessionResponse = _httpClient.GetAsync(sessionRequestUrl).Result;
            var sessionError = new { code = string.Empty, message = string.Empty };
            try
            {
                sessionError = JsonConvert.DeserializeAnonymousType(sessionResponse, sessionError);
            }
            catch { }
            if (!string.IsNullOrEmpty(sessionError?.code))
                throw new NopException($"{sessionError.code} - {sessionError.message}");

            //and now build URL to redirect
            var sessionId = sessionResponse;
            return QueryHelpers.AddQueryString(Defaults.QuickCheckoutServiceUrl, new Dictionary<string, string>
            {
                ["sid"] = CommonHelper.EnsureMaximumLength(sessionId, 32),
            });
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get payment flow type
        /// </summary>
        /// <returns>Result</returns>
        public PaymentFlowType GetPaymentFlowType()
        {
            return _settings.PaymentFlowType;
        }

        /// <summary>
        /// Prepare checkout URL to redirect customer
        /// </summary>
        /// <param name="orderGuid">Order guid</param>
        /// <returns>URL</returns>
        public string PrepareCheckoutUrl(Guid orderGuid)
        {
            return HandleFunction(() =>
            {
                var sessionRequestUrl = PrepareSessionRequestUrl(orderGuid);
                return PrepareCheckoutUrl(sessionRequestUrl);
            }).Result;
        }

        /// <summary>
        /// Prepare checkout URL to redirect customer
        /// </summary>
        /// <param name="request">Payment info required for an order processing</param>
        /// <returns>URL</returns>
        public string PrepareCheckoutUrl(PostProcessPaymentRequest request)
        {
            return HandleFunction(() =>
            {
                var sessionRequestUrl = PrepareSessionRequestUrl(request);
                return PrepareCheckoutUrl(sessionRequestUrl);
            }).Result;
        }

        /// <summary>
        /// Gets the transaction currency code by transaction id
        /// </summary>
        /// <param name="transactionId">The transaction id</param>
        /// <returns>The currency code; otherwise error</returns>
        public (string CurrencyCode, string Error) GetTransactionCurrencyCode(string transactionId)
        {
            return HandleFunction(() =>
            {
                if (string.IsNullOrEmpty(transactionId))
                    throw new NopException("Transaction ID not set");

                //prepare URL to request
                var url = QueryHelpers.AddQueryString(Defaults.MqiServiceUrl, new Dictionary<string, string>
                {
                    ["email"] = _settings.MerchantEmail,
                    ["password"] = ToMD5(_settings.Password).ToLowerInvariant(),
                    ["action"] = "status_trn",
                    ["mb_trn_id"] = transactionId,
                });
                var response = _httpClient.GetAsync(url).Result;
                if (response != null)
                {
                    var match = Regex.Match(response, @"mb_currency=(\w*)");
                    if (match.Success && match.Groups.Count > 1 && match.Groups[1].Success)
                        return match.Groups[1].Value;
                }

                throw new NopException($"Cannot get the currency code of the transaction");
            });
        }

        /// <summary>
        /// Refund order
        /// </summary>
        /// <param name="refundedOrder">Order to refund</param>
        /// <param name="refundedAmount">Amount to refund</param>
        /// <returns>Whether refund is completed; Error if exists</returns>
        public (bool Completed, string Error) Refund(Order refundedOrder, decimal? refundedAmount)
        {
            return HandleFunction(() =>
            {
                //first prepare refund
                var url = PrepareRefundParameters(refundedOrder, refundedAmount);
                var prepareResponse = _httpClient.GetAsync(url).Result;

                //try to get session id
                var prepareXml = XDocument.Parse(prepareResponse);
                var sessionId = prepareXml?.Root?.Element("sid")?.Value;
                if (string.IsNullOrEmpty(sessionId))
                {
                    var errorMessage = prepareXml?.Root?.Element("error")?.Element("error_msg")?.Value;
                    throw new NopException($"Refund order. {(!string.IsNullOrEmpty(errorMessage) ? errorMessage : "Response is empty")}");
                }

                //and now request service to refund
                var refundResponse = _httpClient.GetAsync(QueryHelpers.AddQueryString(Defaults.RefundServiceUrl, new Dictionary<string, string>
                {
                    ["action"] = "refund",
                    ["sid"] = sessionId,
                })).Result;

                //check refund status
                var refundXml = XDocument.Parse(refundResponse);
                var status = refundXml?.Root?.Element("status")?.Value;

                //refund completed
                if (status == "2")
                    return true;

                //refund failed
                if (status == "-2")
                {
                    var errorMessage = refundXml?.Root?.Element("error")?.Value;
                    throw new NopException($"Refund order. {(!string.IsNullOrEmpty(errorMessage) ? errorMessage : "Error")}");
                }

                //refund pending
                return false;
            });
        }

        /// <summary>
        /// Check whether specified credentials are valid
        /// </summary>
        /// <returns>Result</returns>
        public bool ValidateCredentials()
        {
            var (result, error) = HandleFunction(() =>
            {
                //use a random get request only to check credentials validity
                var response = _httpClient.GetAsync(QueryHelpers.AddQueryString(Defaults.MqiServiceUrl, new Dictionary<string, string>
                {
                    ["email"] = _settings.MerchantEmail,
                    ["password"] = ToMD5(_settings.Password).ToLowerInvariant(),
                    ["action"] = "history",
                    ["start_date"] = DateTime.UtcNow.AddDays(-1).ToString("dd-MM-yyyy")
                })).Result;

                //whether the response contains bad request status codes (400, 401, etc)
                if (response.StartsWith("4"))
                {
                    if (response.Contains("remote ip"))
                    {
                        //some special messages
                        response = $"{response}{Environment.NewLine}You are trying to save Skrill credentials " +
                            $"from unregistered IP address in your Skrill account. In order to complete the process, please login to your " +
                            $"Skrill account >> Developer Settings and add your server IP in the MQI and API IP addresses.";
                    }

                    throw new NopException(response);
                }

                //request is succeeded, so credentials are valid
                return true;
            });

            return result;
        }

        /// <summary>
        /// Validate whether webhook request is initiated by the service
        /// </summary>
        /// <param name="form">Request form parameters</param>
        /// <returns>Result</returns>
        public bool ValidateWebhookRequest(IFormCollection form)
        {
            var (result, error) = HandleFunction(() =>
            {
                if (!form?.Any() ?? true)
                    throw new NopException("Webhook request is empty");

                //ensure that request is signed using a signature parameter
                if (!form.TryGetValue("md5sig", out var signature))
                    throw new NopException("Webhook request not signed by a signature parameter");

                //validate transaction_id if any
                if (!Guid.TryParse(form["transaction_id"].ToString(), out var transactionId))
                    throw new NopException("Invalid 'transaction_id' format");

                //get encrypted string from the request message
                var hashedSecretWord = ToMD5(_settings.SecretWord).ToUpperInvariant();
                var encryptedString = ToMD5($"{form["merchant_id"]}{transactionId}{hashedSecretWord}{form["mb_amount"]}{form["mb_currency"]}{form["status"]}");

                //equal this encrypted string with the received signature
                if (!signature.ToString().Equals(encryptedString, StringComparison.InvariantCultureIgnoreCase))
                    throw new NopException("Webhook request isn't valid");

                return true;
            });

            return result;
        }

        #endregion
    }
}