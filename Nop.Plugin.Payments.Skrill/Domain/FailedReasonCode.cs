namespace Nop.Plugin.Payments.Skrill.Domain
{
    /// <summary>
    /// Represents failed reason code enumeration
    /// </summary>
    public enum FailedReasonCode
    {
        /// <summary>
        /// Referred by Card Issuer
        /// </summary>
        ReferredByCardIssuer = 1,

        /// <summary>
        /// Invalid Merchant. Merchant account inactive.
        /// </summary>
        InvalidMerchant = 2,

        /// <summary>
        /// Pick-up card
        /// </summary>
        PickUpCard = 3,

        /// <summary>
        /// Declined by Card Issuer
        /// </summary>
        DeclinedByCardIssuer = 4,

        /// <summary>
        /// Insufficient funds
        /// </summary>
        InsufficientFunds = 5,

        /// <summary>
        /// Merchant/NETELLER/Processor declined
        /// </summary>
        MerchantDeclined = 6,

        /// <summary>
        /// Incorrect PIN
        /// </summary>
        IncorrectPin = 7,

        /// <summary>
        /// PIN tries exceed - card blocked
        /// </summary>
        PinTriesExceed = 8,

        /// <summary>
        /// Invalid Transaction
        /// </summary>
        InvalidTransaction = 9,

        /// <summary>
        /// Transaction frequency limit exceeded
        /// </summary>
        TransactionFrequencyLimitExceeded = 10,

        /// <summary>
        /// Invalid Amount format. Amount too high. Amount too low. Limit Exceeded.
        /// </summary>
        InvalidAmountFormat = 11,

        /// <summary>
        /// Invalid credit card or bank account
        /// </summary>
        InvalidCreditCardOrBankAccount = 12,

        /// <summary>
        /// Invalid card Issuer
        /// </summary>
        InvalidCardIssuer = 13,

        /// <summary>
        /// Duplicate transaction reference
        /// </summary>
        DuplicateTransactionReference = 15,

        /// <summary>
        /// Authentication credentials expired/disabled/locked/invalid. Cannot authenticate. Request not authorized.
        /// </summary>
        RequestNotAuthorized = 19,

        /// <summary>
        /// Neteller member is in a blocked country/state/region/geolocation
        /// </summary>
        NetellerMemberBlocked = 20,

        /// <summary>
        /// Unsupported Accept header or Content type
        /// </summary>
        UnsupportedAcceptHeaderOrContentType = 22,

        /// <summary>
        /// Card expired
        /// </summary>
        CardExpired = 24,

        /// <summary>
        /// Requested API function not supported (legacy function)
        /// </summary>
        LegacyFunction = 27,

        /// <summary>
        /// Lost/stolen card
        /// </summary>
        LostStolenCard = 28,

        /// <summary>
        /// Format Failure
        /// </summary>
        FormatFailure = 30,

        /// <summary>
        /// Card Security Code (CVV2/CVC2) Check Failed
        /// </summary>
        CardSecurityCodeCheckFailed = 32,

        /// <summary>
        /// Illegal Transaction
        /// </summary>
        IllegalTransaction = 34,

        /// <summary>
        /// Member/Merchant not entitled/authorized. Account closed. Unauthorized access.
        /// </summary>
        UnauthorizedAccess = 35,

        /// <summary>
        /// Card restricted by Card Issuer
        /// </summary>
        CardRestrictedByCardIssuer = 37,

        /// <summary>
        /// Security violation
        /// </summary>
        SecurityViolation = 38,

        /// <summary>
        /// Card blocked by Card Issuer
        /// </summary>
        CardBlockedByCardIssuer = 42,

        /// <summary>
        /// Card Issuing Bank or Network is not available
        /// </summary>
        CardIssuingBankOrNetworkIsNotAvailable = 44,

        /// <summary>
        /// Processing error - card type is not processed by the authorization centre
        /// </summary>
        ProcessingError = 45,

        /// <summary>
        /// System error
        /// </summary>
        SystemError = 51,

        /// <summary>
        /// Transaction not permitted by acquirer
        /// </summary>
        TransactionNotPermittedByAcquirer = 58,

        /// <summary>
        /// Transaction not permitted for cardholder
        /// </summary>
        TransactionNotPermittedForCardholder = 63,

        /// <summary>
        /// Invalid accountId/country/currency/customer/email/field/merchant reference/merchant account currency/term length/verification code. Account not found/disabled. Entity not found. URI not found. Existing member email. Plan already exists. Bad request.
        /// </summary>
        BadRequest = 64,

        /// <summary>
        /// BitPay session expired
        /// </summary>
        BitPaySessionExpired = 67,

        /// <summary>
        /// Referenced transaction has not been settled
        /// </summary>
        ReferencedTransactionHasNotBeenSettled = 68,

        /// <summary>
        /// Referenced transaction is not fully authenticated
        /// </summary>
        ReferencedTransactionIsNotFullyAuthenticated = 69,

        /// <summary>
        /// Customer failed 3DS verification
        /// </summary>
        CustomerFailed3DsVerification = 70,

        /// <summary>
        /// Fraud rules declined
        /// </summary>
        FraudRulesDeclined = 80,

        /// <summary>
        /// Error in communication with provider
        /// </summary>
        ErrorInCommunicationWithProvider = 98,

        /// <summary>
        /// Error in communication with provider
        /// </summary>
        Other = 99
    }
}