using frontida4baby.Web.Models.Entities;
using Microsoft.Extensions.Options;

namespace frontida4baby.Web.Services;

public class UserEmailService : IUserEmailService
{
    private readonly IEmailService      _email;
    private readonly EmailOptions       _emailOpts;
    private readonly UserEmailOptions   _opts;
    private readonly ILogger<UserEmailService> _logger;

    public UserEmailService(
        IEmailService email,
        IOptions<EmailOptions> emailOpts,
        IOptions<UserEmailOptions> opts,
        ILogger<UserEmailService> logger)
    {
        _email     = email;
        _emailOpts = emailOpts.Value;
        _opts      = opts.Value;
        _logger    = logger;
    }

    public async Task SendWelcomeAsync(ApplicationUser user)
    {
        if (!_opts.Welcome || string.IsNullOrWhiteSpace(user.Email)) return;

        var appName = AppName();
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#4f46e5">Καλωσήρθες στο {appName}! 🎉</h2>
              <p>Γεια σου <strong>{H(user.FirstName)}</strong>,</p>
              <p>Ο λογαριασμός σου επιβεβαιώθηκε. Είσαι έτοιμος/-η να ξεκινήσεις!</p>
              <p style="margin:24px 0">Τι μπορείς να κάνεις τώρα:</p>
              <ul>
                <li>Δημιούργησε ή συμπλήρωσε το προφίλ σου</li>
                <li>Ανέβασε μια αγγελία φροντίδας</li>
                <li>Βρες επαγγελματίες φροντίδας κοντά σου</li>
              </ul>
              <p style="color:#6b7280;font-size:13px;margin-top:32px">
                Καλή συνέχεια από την ομάδα του {appName}!
              </p>
            </div>
            """;

        await SendAsync(user.Email, "Καλωσήρθες στο frontida4all!", html);
    }

    public async Task SendContentRejectedAsync(ApplicationUser user, string contentSnippet, string reason)
    {
        if (!_opts.ContentRejected || string.IsNullOrWhiteSpace(user.Email)) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#dc2626">Το περιεχόμενό σου απορρίφθηκε</h2>
              <p>Γεια σου <strong>{H(user.FirstName)}</strong>,</p>
              <p>Δυστυχώς, ένα κομμάτι περιεχομένου που υπέβαλες δεν πέρασε τον αυτόματο έλεγχο μας.</p>
              <table style="border-collapse:collapse;width:100%;margin:16px 0">
                <tr>
                  <td style="padding:8px 12px;background:#fee2e2;font-weight:bold;width:140px">Λόγος</td>
                  <td style="padding:8px 12px;background:#fef2f2">{H(reason)}</td>
                </tr>
                <tr>
                  <td style="padding:8px 12px;background:#fee2e2;font-weight:bold">Απόσπασμα</td>
                  <td style="padding:8px 12px;background:#fef2f2"><em>{H(Truncate(contentSnippet, 300))}</em></td>
                </tr>
              </table>
              <p>Για να αποφύγεις μελλοντικές απορρίψεις:</p>
              <ul>
                <li>Χρησιμοποίησε κατάλληλη γλώσσα και τόνο</li>
                <li>Μην συμπεριλαμβάνεις προσωπικά στοιχεία επικοινωνίας στον τίτλο ή την περιγραφή</li>
                <li>Τήρησε τους όρους χρήσης της πλατφόρμας</li>
              </ul>
              <p style="color:#6b7280;font-size:13px;margin-top:32px">
                Εάν πιστεύεις ότι πρόκειται για λάθος, επικοινώνησε μαζί μας.
              </p>
            </div>
            """;

        await SendAsync(user.Email, "Το περιεχόμενό σου απορρίφθηκε", html);
    }

    public async Task SendReactionApprovedAsync(ApplicationUser caregiver, string postTitle, ApplicationUser op)
    {
        if (!_opts.ReactionApproved || string.IsNullOrWhiteSpace(caregiver.Email)) return;

        var opName = $"{op.FirstName} {op.LastName}".Trim();
        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#4f46e5">Η αντίδρασή σου εγκρίθηκε! ✅</h2>
              <p>Γεια σου <strong>{H(caregiver.FirstName)}</strong>,</p>
              <p>
                Ο/Η <strong>{H(opName)}</strong> ενέκρινε την αντίδρασή σου στην αγγελία
                «<strong>{H(postTitle)}</strong>».
              </p>
              <p>
                Τώρα έχεις πρόσβαση στα στοιχεία επικοινωνίας του/της.
                Επίσκεψε την αγγελία για να δεις τα στοιχεία και να επικοινωνήσεις άμεσα.
              </p>
              <p style="color:#6b7280;font-size:13px;margin-top:32px">
                Καλή επιτυχία από την ομάδα του {AppName()}!
              </p>
            </div>
            """;

        await SendAsync(caregiver.Email, "Η αντίδρασή σου εγκρίθηκε!", html);
    }

    public async Task SendPaymentSucceededAsync(ApplicationUser user)
    {
        if (!_opts.PaymentSucceeded || string.IsNullOrWhiteSpace(user.Email)) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#16a34a">Η συνδρομή σου στο Premium είναι ενεργή! 🌟</h2>
              <p>Γεια σου <strong>{H(user.FirstName)}</strong>,</p>
              <p>Η πληρωμή σου ολοκληρώθηκε επιτυχώς. Καλώς ήρθες στο Premium!</p>
              <p>Τώρα έχεις πρόσβαση σε:</p>
              <ul>
                <li>Απεριόριστες αγγελίες και αντιδράσεις</li>
                <li>Προτεραιότητα στα αποτελέσματα αναζήτησης</li>
                <li>Προηγμένα φίλτρα και στατιστικά</li>
              </ul>
              <p style="color:#6b7280;font-size:13px;margin-top:32px">
                Ευχαριστούμε για την εμπιστοσύνη σου! — {AppName()}
              </p>
            </div>
            """;

        await SendAsync(user.Email, "Η εγγραφή σου στο Premium είναι ενεργή", html);
    }

    public async Task SendPaymentFailedAsync(ApplicationUser user)
    {
        if (!_opts.PaymentFailed || string.IsNullOrWhiteSpace(user.Email)) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#d97706">Αποτυχία χρέωσης — ενέργεια απαιτείται ⚠️</h2>
              <p>Γεια σου <strong>{H(user.FirstName)}</strong>,</p>
              <p>Δεν καταφέραμε να χρεώσουμε την κάρτα σου για τη συνδρομή Premium.</p>
              <p>
                Παρακαλώ επισκέψου τη σελίδα διαχείρισης συνδρομής και ενημέρωσε
                τα στοιχεία πληρωμής σου για να διατηρήσεις την πρόσβασή σου.
              </p>
              <p style="color:#6b7280;font-size:13px;margin-top:32px">
                Εάν χρειάζεσαι βοήθεια, επικοινώνησε μαζί μας. — {AppName()}
              </p>
            </div>
            """;

        await SendAsync(user.Email, "Αποτυχία χρέωσης — ενέργεια απαιτείται", html);
    }

    public async Task SendSubscriptionCancelledAsync(ApplicationUser user)
    {
        if (!_opts.SubscriptionCancelled || string.IsNullOrWhiteSpace(user.Email)) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#6b7280">Η συνδρομή σου ακυρώθηκε</h2>
              <p>Γεια σου <strong>{H(user.FirstName)}</strong>,</p>
              <p>Η Premium συνδρομή σου έχει ακυρωθεί. Ο λογαριασμός σου επιστρέφει στο δωρεάν πλάνο.</p>
              <p>Με το δωρεάν πλάνο μπορείς να:</p>
              <ul>
                <li>Δημοσιεύσεις έως 2 αγγελίες το μήνα</li>
                <li>Κάνεις έως 5 αντιδράσεις το μήνα</li>
                <li>Βλέπεις όλες τις αγγελίες της πλατφόρμας</li>
              </ul>
              <p>
                Μπορείς να αναβαθμίσεις ξανά οποιαδήποτε στιγμή από τη σελίδα συνδρομής.
              </p>
              <p style="color:#6b7280;font-size:13px;margin-top:32px">
                Ευχαριστούμε που χρησιμοποίησες το {AppName()}!
              </p>
            </div>
            """;

        await SendAsync(user.Email, "Η συνδρομή σου ακυρώθηκε", html);
    }

    public async Task SendSupportConfirmationAsync(string toEmail, string name, string subject)
    {
        if (!_opts.SupportConfirmation || string.IsNullOrWhiteSpace(toEmail)) return;

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#4f46e5">Λάβαμε το μήνυμά σου ✉️</h2>
              <p>Γεια σου <strong>{H(name)}</strong>,</p>
              <p>Λάβαμε το αίτημα υποστήριξης με θέμα: <strong>«{H(subject)}»</strong>.</p>
              <p>Η ομάδα μας θα σου απαντήσει εντός 1–2 εργάσιμων ημερών.</p>
              <p style="color:#6b7280;font-size:13px;margin-top:32px">
                Ευχαριστούμε — {AppName()}
              </p>
            </div>
            """;

        await SendAsync(toEmail, "Λάβαμε το μήνυμά σου", html);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task SendAsync(string to, string subject, string html)
    {
        var appName  = AppName();
        var fullSubj = $"{appName} | {subject}";
        try
        {
            await _email.SendAsync(to, fullSubj, html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UserEmailService failed to send '{Subject}' to {To}", subject, to);
        }
    }

    private string AppName() =>
        string.IsNullOrWhiteSpace(_emailOpts.FromName) ? "frontida4all" : _emailOpts.FromName;

    private static string H(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
