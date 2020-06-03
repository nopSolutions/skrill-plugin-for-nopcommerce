using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Skrill.Domain;
using Nop.Plugin.Payments.Skrill.Services;
using Nop.Services.Common;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Skrill.Controllers
{
    public class SkrillWebhookController : BaseController
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly ServiceManager _serviceManager;

        #endregion

        #region Ctor

        public SkrillWebhookController(IGenericAttributeService genericAttributeService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            ServiceManager serviceManager)
        {
            _genericAttributeService = genericAttributeService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _serviceManager = serviceManager;
        }

        #endregion

        #region Methods

        [HttpPost]
        public IActionResult QuickCheckoutWebhook()
        {
            try
            {
                //validate request
                var isValid = _serviceManager.ValidateWebhookRequest(Request.Form);
                if (!isValid)
                    return BadRequest();

                //try to get an order for this transaction
                var order = _orderService.GetOrderByCustomOrderNumber(Request.Form["transaction_id"]);
                if (order == null)
                    return Ok();

                //add order note
                var details = Request.Form.Aggregate(string.Empty, (message, parameter) => $"{message}{parameter.Key}: {parameter.Value}; ");
                _orderService.InsertOrderNote(new OrderNote
                {
                    Note = $"Webhook details: {Environment.NewLine}{details}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                //check transaction status
                switch (Request.Form["status"].ToString().ToLower())
                {
                    //order cancelled
                    case "-3":
                    case "-2":
                    case "-1":
                        if (Enum.TryParse<FailedReasonCode>(Request.Form["failed_reason_code"], out var failedReason))
                        {
                            _orderService.InsertOrderNote(new OrderNote
                            {
                                Note = $"Order cancelled. Reason: {failedReason.ToString()}",
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });
                        }
                        if (_orderProcessingService.CanCancelOrder(order))
                            _orderProcessingService.CancelOrder(order, true);
                        break;

                    //order pending
                    case "0":
                        order.OrderStatus = OrderStatus.Pending;
                        _orderService.UpdateOrder(order);
                        _orderProcessingService.CheckOrderStatus(order);
                        break;

                    //order processed
                    case "2":
                        if (_orderProcessingService.CanMarkOrderAsPaid(order))
                        {
                            if (Request.Form.TryGetValue("mb_transaction_id", out var transactionId))
                                order.CaptureTransactionId = transactionId;
                            _orderService.UpdateOrder(order);
                            _orderProcessingService.MarkOrderAsPaid(order);
                        }
                        break;
                }
            }
            catch { }

            return Ok();
        }

        [HttpPost]
        public IActionResult RefundWebhook()
        {
            try
            {
                //validate request
                var isValid = _serviceManager.ValidateWebhookRequest(Request.Form);
                if (!isValid)
                    return BadRequest();

                //try to get an order for this transaction
                var order = _orderService.GetOrderByCustomOrderNumber(Request.Form["transaction_id"]);
                if (order == null)
                    return Ok();

                //add order note
                var details = Request.Form.Aggregate(string.Empty, (message, parameter) => $"{message}{parameter.Key}: {parameter.Value}; ");
                _orderService.InsertOrderNote(new OrderNote
                {
                    Note = $"Webhook details: {Environment.NewLine}{details}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                //check transaction status
                switch (Request.Form["status"].ToString().ToLower())
                {
                    //refund processed
                    case "2":
                        //ensure that this refund has not been processed before
                        var refundGuid = _genericAttributeService.GetAttribute<string>(order, Defaults.RefundGuidAttribute);
                        if (refundGuid?.Equals(Request.Form["refund_guid"], StringComparison.InvariantCultureIgnoreCase) ?? false)
                            break;

                        if (decimal.TryParse(Request.Form["mb_amount"], out var refundedAmount) &&
                            _orderProcessingService.CanPartiallyRefundOffline(order, refundedAmount))
                        {
                            _orderProcessingService.PartiallyRefundOffline(order, refundedAmount);
                        }
                        break;
                }
            }
            catch { }

            return Ok();
        }

        #endregion
    }
}