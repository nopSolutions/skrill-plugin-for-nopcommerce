namespace Nop.Plugin.Payments.Skrill.Domain
{
    /// <summary>
    /// Represents a payment flow type
    /// </summary>
    public enum PaymentFlowType
    {
        /// <summary>
        /// Customer pays for the order on Skrill site
        /// </summary>
        Redirection,

        /// <summary>
        /// Customer pays for the order on merchant site
        /// </summary>
        Inline
    }
}
