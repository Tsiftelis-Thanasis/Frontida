namespace frontida4baby.Web.Services;

/// <summary>
/// Seller identity used on locally-issued invoices. VatNumber is a placeholder
/// until the owner's accountant finalises company registration — replace via
/// config, no code change needed.
/// </summary>
public class CompanyOptions
{
    public string Name      { get; set; } = "frontida4all";
    public string VatNumber { get; set; } = "";
    public string Address   { get; set; } = "";
    public string TaxOffice { get; set; } = "";
}
