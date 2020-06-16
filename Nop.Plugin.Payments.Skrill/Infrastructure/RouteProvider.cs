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
            endpointRouteBuilder.MapControllerRoute(Defaults.ConfigurationRouteName, "Plugins/Skrill/Configure",
                new { controller = "Skrill", action = "Configure", area = AreaNames.Admin });

            endpointRouteBuilder.MapControllerRoute(Defaults.OrderPaidWebhookRouteName, "Plugins/Skrill/OrderPaidWebhook",
                new { controller = "SkrillWebhook", action = "OrderPaidWebhook" });

            endpointRouteBuilder.MapControllerRoute(Defaults.QuickCheckoutWebhookRouteName, "Plugins/Skrill/QuickCheckoutWebhook",
                new { controller = "SkrillWebhook", action = "QuickCheckoutWebhook" });

            endpointRouteBuilder.MapControllerRoute(Defaults.RefundWebhookRouteName, "Plugins/Skrill/RefundWebhook",
                new { controller = "SkrillWebhook", action = "RefundWebhook" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}