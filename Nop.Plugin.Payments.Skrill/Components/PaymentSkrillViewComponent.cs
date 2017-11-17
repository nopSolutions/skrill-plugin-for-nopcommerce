using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Skrill.Components
{
    [ViewComponent(Name = "PaymentSkrill")]
    public class PaymentSkrillViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Skrill/Views/PaymentInfo.cshtml");
        }
    }
}
