﻿using Nop.Core;

namespace Nop.Plugin.Payments.Skrill
{
    /// <summary>
    /// Represents plugin constants
    /// </summary>
    public class Defaults
    {
        /// <summary>
        /// Gets the plugin system name
        /// </summary>
        public static string SystemName => "Payments.Skrill";

        /// <summary>
        /// Gets the user agent used to request third-party services
        /// </summary>
        public static string UserAgent => $"nopCommerce-{NopVersion.CurrentVersion}";

        /// <summary>
        /// Gets the third-party service URL to request
        /// </summary>
        public static string QuickCheckoutServiceUrl => "https://pay.skrill.com";

        /// <summary>
        /// Gets the third-party service URL to request
        /// </summary>
        public static string RefundServiceUrl => "https://www.skrill.com/app/refund.pl";

        /// <summary>
        /// Gets the third-party service URL to request
        /// </summary>
        public static string MqiServiceUrl => "https://www.skrill.com/app/query.pl";

        /// <summary>
        /// Gets the nopCommerce partner referral ID
        /// </summary>
        public static string ReferralId => "124956815";

        /// <summary>
        /// Gets the configuration route name
        /// </summary>
        public static string ConfigurationRouteName => "Plugin.Payments.Skrill.Configure";

        /// <summary>
        /// Gets the checkout completed route name
        /// </summary>
        public static string CheckoutCompletedRouteName => "CheckoutCompleted";

        /// <summary>
        /// Gets the order details route name
        /// </summary>
        public static string OrderDetailsRouteName => "OrderDetails";

        /// <summary>
        /// Gets the webhook route name
        /// </summary>
        public static string QuickCheckoutWebhookRouteName => "Plugin.Payments.Skrill.QuickCheckoutWebhook";

        /// <summary>
        /// Gets the webhook route name
        /// </summary>
        public static string RefundWebhookRouteName => "Plugin.Payments.Skrill.RefundWebhook";

        /// <summary>
        /// Gets a name of the view component to display refund hints
        /// </summary>
        public const string REFUND_HINTS_VIEW_COMPONENT_NAME = "SkrillRefundHints";
    }
}