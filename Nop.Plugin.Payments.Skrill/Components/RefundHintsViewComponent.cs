using Microsoft.AspNetCore.Mvc;
using Nop.Services.Payments;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Payments.Skrill.Components
{
    /// <summary>
    /// Represents the view component to display refund hints
    /// </summary>
    [ViewComponent(Name = Defaults.REFUND_HINTS_VIEW_COMPONENT_NAME)]
    public class RefundHintsViewComponent : NopViewComponent
    {
        #region Fields

        private readonly IPaymentPluginManager _paymentPluginManager;

        #endregion

        #region Ctor

        public RefundHintsViewComponent(IPaymentPluginManager paymentPluginManager)
        {
            _paymentPluginManager = paymentPluginManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Invoke view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>View component result</returns>
        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            if (!_paymentPluginManager.IsPluginActive(Defaults.SystemName))
                return Content(string.Empty);

            if (!widgetZone.Equals(AdminWidgetZones.OrderDetailsBlock) || !(additionalData is OrderModel model))
                return Content(string.Empty);

            if (!model.PaymentMethod.Equals(Defaults.SystemName) &&
                !model.PaymentMethod.Equals(_paymentPluginManager.LoadPluginBySystemName(Defaults.SystemName)?.PluginDescriptor.FriendlyName))
            {
                return Content(string.Empty);
            }

            return View("~/Plugins/Payments.Skrill/Views/RefundHints.cshtml");
        }

        #endregion
    }
}