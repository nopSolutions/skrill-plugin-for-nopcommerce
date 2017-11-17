using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Skrill
{
    public partial class RouteProvider : IRouteProvider
    {
        void IRouteProvider.RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //response notification
            routeBuilder.MapRoute("Plugin.Payments.Skrill.ResponseNotificationHandler",
                 "Plugins/PaymentSkrill/ResponseNotificationHandler",
                 new { controller = "PaymentSkrill", action = "ResponseNotificationHandler" });
        }

        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
