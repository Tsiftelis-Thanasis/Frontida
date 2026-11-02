using frontida4all.Web.Data;
using frontida4all.Web.Models.Entities;
using frontida4all.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace frontida4all.Tests.Services;

public class LocalInvoicingServiceTests
{
    private static (ApplicationDbContext Db, LocalInvoicingService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        var company = Options.Create(new CompanyOptions { Name = "Test Co", VatNumber = "123456789" });
        return (db, new LocalInvoicingService(db, company));
    }

    private static ApplicationUser Buyer() =>
        new() { Id = "buyer-1", Email = "buyer@example.com", FirstName = "Maria", LastName = "Papadopoulou" };

    [Fact]
    public async Task IssueInvoice_SameStripeInvoiceTwice_ReturnsExistingInvoice()
    {
        var (db, service) = CreateService();
        await using var _ = db;

        var first = await service.IssueInvoiceAsync(Buyer(), "in_123", 19.90m, "EUR", new DateTime(2026, 3, 1));
        var second = await service.IssueInvoiceAsync(Buyer(), "in_123", 19.90m, "EUR", new DateTime(2026, 3, 1));

        Assert.Equal(first.Number, second.Number);
        Assert.Equal(1, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task IssueInvoice_NumbersAreSequentialWithinAYear()
    {
        var (db, service) = CreateService();
        await using var _ = db;

        var a = await service.IssueInvoiceAsync(Buyer(), "in_1", 10m, "EUR", new DateTime(2026, 1, 5));
        var b = await service.IssueInvoiceAsync(Buyer(), "in_2", 10m, "EUR", new DateTime(2026, 2, 5));

        Assert.Equal("2026-00001", a.Number);
        Assert.Equal("2026-00002", b.Number);
    }

    [Fact]
    public async Task IssueInvoice_NumberingRestartsEachYear()
    {
        var (db, service) = CreateService();
        await using var _ = db;

        await service.IssueInvoiceAsync(Buyer(), "in_1", 10m, "EUR", new DateTime(2026, 12, 31));
        var next = await service.IssueInvoiceAsync(Buyer(), "in_2", 10m, "EUR", new DateTime(2027, 1, 1));

        Assert.Equal("2027-00001", next.Number);
    }

    [Fact]
    public async Task IssueInvoice_SetsNetTotalAndVatFromTheSameAmount()
    {
        var (db, service) = CreateService();
        await using var _ = db;

        var invoice = await service.IssueInvoiceAsync(Buyer(), "in_1", 19.90m, "EUR", new DateTime(2026, 3, 1));

        Assert.Equal(19.90m, invoice.NetAmount);
        Assert.Equal(19.90m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.VatAmount);
        Assert.Equal("Test Co", invoice.SellerName);
    }
}
