using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Skrill.Infrastructure
{
    /// <summary>
    /// Represents plugin route provider
    /// </summary>
    public class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="endpointRouteBuilder">Route builder</param>
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapControllerRoute(name: Defaults.ConfigurationRouteName,
                pattern: "Plugins/Skrill/Configure",
                defaults: new { controller = "Skrill", action = "Configure", area = AreaNames.Admin });

            endpointRouteBuilder.MapControllerRoute(name: Defaults.OrderPaidWebhookRouteName,
                pattern: "Plugins/Skrill/OrderPaidWebhook",
                defaults: new { controller = "SkrillWebhook", action = "OrderPaidWebhook" });

            endpointRouteBuilder.MapControllerRoute(name: Defaults.QuickCheckoutWebhookRouteName,
                pattern: "Plugins/Skrill/QuickCheckoutWebhook",
                defaults: new { controller = "SkrillWebhook", action = "QuickCheckoutWebhook" });

            endpointRouteBuilder.MapControllerRoute(name: Defaults.RefundWebhookRouteName,
                pattern: "Plugins/Skrill/RefundWebhook",
                defaults: new { controller = "SkrillWebhook", action = "RefundWebhook" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}