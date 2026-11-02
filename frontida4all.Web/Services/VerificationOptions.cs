namespace frontida4all.Web.Services;

public class VerificationOptions
{
    /// <summary>How long a criminal-record certificate counts as valid, measured from its issue date.</summary>
    public int CriminalRecordValidityMonths { get; set; } = 6;

    /// <summary>How long an approved address proof (e.g. utility bill) stays valid, measured from approval.</summary>
    public int AddressProofValidityMonths { get; set; } = 12;

    /// <summary>Reminders go out this many days before a document expires.</summary>
    public int ExpiryWarningDays { get; set; } = 30;

    /// <summary>
    /// When false (default) the uploaded file is deleted as soon as an admin has reviewed it and only the
    /// outcome and dates are kept (data minimisation — criminal-record data is GDPR art. 10 data).
    /// </summary>
    public bool RetainDocuments { get; set; } = false;

    public int MaxFileSizeMb { get; set; } = 5;

    /// <summary>Hours between background expiry checks.</summary>
    public int ExpiryJobIntervalHours { get; set; } = 12;
}
