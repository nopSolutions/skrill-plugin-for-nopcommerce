using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.Skrill.Services;
using Nop.Services.Payments;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Skrill.Components
{
    /// <summary>
    /// Represents payment info view component
    /// </summary>
    [ViewComponent(Name = Defaults.PAYMENT_INFO_VIEW_COMPONENT_NAME)]
    public class PaymentSkrillViewComponent : NopViewComponent
    {
        #region Fields

        private readonly IPaymentService _paymentService;
        private readonly ServiceManager _serviceManager;

        #endregion

        #region Ctor

        public PaymentSkrillViewComponent(IPaymentService paymentService,
            ServiceManager serviceManager)
        {
            _paymentService = paymentService;
            _serviceManager = serviceManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Invokes view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>View component result</returns>
        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            HttpContext.Session.Remove(Defaults.PaymentRequestSessionKey);
            var processPaymentRequest = new ProcessPaymentRequest();
            _paymentService.GenerateOrderGuid(processPaymentRequest);
            HttpContext.Session.Set(Defaults.PaymentRequestSessionKey, processPaymentRequest);

            var paymentUrl = await _serviceManager.PrepareCheckoutUrlAsync(processPaymentRequest.OrderGuid);

            return View("~/Plugins/Payments.Skrill/Views/PaymentInfo.cshtml", paymentUrl);
        }

        #endregion
    }
}