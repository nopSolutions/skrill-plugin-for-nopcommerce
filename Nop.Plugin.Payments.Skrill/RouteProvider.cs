using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.Skrill
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //response notification
            routes.MapRoute("Plugin.Payments.Skrill.ResponseNotificationHandler",
                 "Plugins/PaymentSkrill/ResponseNotificationHandler",
                 new { controller = "PaymentSkrill", action = "ResponseNotificationHandler" },
                 new[] { "Nop.Plugin.Payments.Skrill.Controllers" }
            );
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
