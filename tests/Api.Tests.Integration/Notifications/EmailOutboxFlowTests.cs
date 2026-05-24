using Application.Common.Interfaces.Persistence;
using Domain.Entities.Notifications;
using FluentAssertions;
using Infrastructure.EmailProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tests.Common;

namespace Api.Tests.Integration.Notifications;

/// <summary>
/// Verifies the transactional outbox pattern for outbound emails: the IEmailService write goes
/// into the caller's ApplicationDbContext change tracker (same scope, same tx), the
/// EmailOutboxDispatchInterceptor schedules a Hangfire job only after a successful
/// SaveChanges, and the EmailOutboxProcessor + EmailOutboxSweeper finish the delivery loop.
/// </summary>
public class EmailOutboxFlowTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    public EmailOutboxFlowTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldNotPersistOrDispatchUntilCallerCommits()
    {
        // EmailService just adds to the change tracker. Without SaveChanges nothing reaches the
        // DB and the interceptor never fires → no Hangfire dispatch. Pre-rewrite this was a
        // dual-write that committed independently.
        var realEmailService = new EmailService(Context);
        await realEmailService.SendEmailAsync(
            "alice@example.com", "Outbox test", "<p>body</p>", CancellationToken.None);

        var rowsBeforeSave = await Context.Set<EmailOutbox>().AsNoTracking()
            .Where(x => x.ToEmail == "alice@example.com").ToListAsync();
        rowsBeforeSave.Should().BeEmpty("AddAsync just tracks; no commit yet");
        _factory.Jobs.Created.Should().BeEmpty("interceptor fires only after SavedChanges");
    }

    [Fact]
    public async Task ShouldCommitRowAndScheduleProcessorOnCallerSaveChanges()
    {
        var realEmailService = new EmailService(Context);
        await realEmailService.SendEmailAsync(
            "bob@example.com", "Outbox test", "<p>body</p>", CancellationToken.None);
        await Context.SaveChangesAsync();

        var row = await Context.Set<EmailOutbox>().AsNoTracking()
            .FirstAsync(x => x.ToEmail == "bob@example.com");
        row.Status.Should().Be(EmailOutboxStatus.Pending);

        _factory.Jobs.Created.Should().HaveCount(1,
            "interceptor schedules one processor job per Added EmailOutbox row");
        var dispatched = _factory.Jobs.Created.Single();
        dispatched.Job.Method.Name.Should().Be(nameof(EmailOutboxProcessor.ProcessAsync));
        dispatched.Job.Args[0].Should().Be(row.Id);
    }

    [Fact]
    public async Task ShouldRollBackRowAndNoOpProcessorWhenCallerTxRollsBack()
    {
        // Explicit user tx + rollback: the outbox row commits-then-rolls-back with the rest of
        // the tx. The dispatch interceptor still fires inside the tx (SavedChanges runs after
        // flush but before commit), so a Hangfire job IS scheduled — but the processor finds
        // the row missing once the tx is undone and exits quietly.
        var realEmailService = new EmailService(Context);
        Guid orphanId;
        await using (var tx = await Context.Database.BeginTransactionAsync())
        {
            await realEmailService.SendEmailAsync(
                "carol@example.com", "Outbox test", "<p>body</p>", CancellationToken.None);
            await Context.SaveChangesAsync();
            orphanId = (await Context.Set<EmailOutbox>().AsNoTracking()
                .FirstAsync(x => x.ToEmail == "carol@example.com")).Id;
            await tx.RollbackAsync();
        }
        Context.ChangeTracker.Clear();

        var rows = await Context.Set<EmailOutbox>().AsNoTracking()
            .Where(x => x.ToEmail == "carol@example.com").ToListAsync();
        rows.Should().BeEmpty("rollback undid the outbox row alongside business state");

        // The processor must be safe against the racing schedule → run it and verify no-op.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        var processor = new EmailOutboxProcessor(
            dbContext,
            new SuccessSmtpSender(),
            scope.ServiceProvider.GetRequiredService<ILogger<EmailOutboxProcessor>>());
        await processor.ProcessAsync(orphanId, CancellationToken.None);
        // No exception, no email — the row is gone, processor logs a warning and returns.
    }

    [Fact]
    public async Task ProcessorShouldMarkRowSentWhenSmtpSucceeds()
    {
        var row = await SeedPendingRowAsync("dave@example.com");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        var smtp = new SuccessSmtpSender();
        var processor = new EmailOutboxProcessor(
            dbContext, smtp,
            scope.ServiceProvider.GetRequiredService<ILogger<EmailOutboxProcessor>>());
        await processor.ProcessAsync(row.Id, CancellationToken.None);

        var refreshed = await Context.Set<EmailOutbox>().AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        refreshed.Status.Should().Be(EmailOutboxStatus.Sent);
        refreshed.SentAt.Should().NotBeNull();
        smtp.SendCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessorShouldBumpAttemptCountAndRethrowOnSmtpFailure()
    {
        var row = await SeedPendingRowAsync("erin@example.com");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        var processor = new EmailOutboxProcessor(
            dbContext,
            new ThrowingSmtpSender("relay rejected"),
            scope.ServiceProvider.GetRequiredService<ILogger<EmailOutboxProcessor>>());

        var act = async () => await processor.ProcessAsync(row.Id, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>(
            "rethrow drives Hangfire's retry schedule");

        var refreshed = await Context.Set<EmailOutbox>().AsNoTracking()
            .FirstAsync(x => x.Id == row.Id);
        refreshed.Status.Should().Be(EmailOutboxStatus.Pending);
        refreshed.AttemptCount.Should().Be(1);
        refreshed.LastError.Should().Contain("relay rejected");
    }

    [Fact]
    public async Task SweeperShouldEnqueueProcessorForStalePendingRows()
    {
        // Backstop: a Pending row that's been sitting for >1 min (e.g. interceptor's Hangfire
        // enqueue failed) gets picked up by the recurring sweep.
        var stale = EmailOutbox.NewPending(Guid.NewGuid(), "frank@example.com", "s", "b");
        BackdateCreatedAt(stale, TimeSpan.FromMinutes(5));
        await Context.Set<EmailOutbox>().AddAsync(stale);

        var fresh = EmailOutbox.NewPending(Guid.NewGuid(), "grace@example.com", "s", "b");
        await Context.Set<EmailOutbox>().AddAsync(fresh);
        await SaveChangesAsync();

        // Clear what the dispatch interceptor enqueued on the SaveChanges above so the sweeper
        // assertion is unambiguous.
        _factory.Jobs.Clear();

        using var scope = _factory.Services.CreateScope();
        var sweeper = scope.ServiceProvider.GetRequiredService<EmailOutboxSweeper>();
        await sweeper.SweepAsync(CancellationToken.None);

        _factory.Jobs.Created.Should().HaveCount(1, "only the stale row is older than the cutoff");
        _factory.Jobs.Created.Single().Job.Args[0].Should().Be(stale.Id);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<EmailOutbox> SeedPendingRowAsync(string toEmail)
    {
        var row = EmailOutbox.NewPending(Guid.NewGuid(), toEmail, "subj", "<p>body</p>");
        await Context.Set<EmailOutbox>().AddAsync(row);
        await SaveChangesAsync();
        // SaveChangesAsync above triggers the dispatch interceptor; clear the recording so
        // per-test assertions stay focused.
        _factory.Jobs.Clear();
        return row;
    }

    private static void BackdateCreatedAt(EmailOutbox row, TimeSpan howLongAgo)
    {
        typeof(EmailOutbox).GetProperty(nameof(EmailOutbox.CreatedAt))!
            .SetValue(row, DateTime.UtcNow - howLongAgo);
    }

    private sealed class SuccessSmtpSender() : SmtpEmailSender(
        Options.Create(new MailSettings()),
        new LoggerFactory().CreateLogger<SmtpEmailSender>())
    {
        public int SendCount { get; private set; }

        public override Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            SendCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSmtpSender(string message) : SmtpEmailSender(
        Options.Create(new MailSettings()),
        new LoggerFactory().CreateLogger<SmtpEmailSender>())
    {
        public override Task SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            throw new InvalidOperationException(message);
    }

    public Task InitializeAsync()
    {
        _factory.Jobs.Clear();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _factory.Jobs.Clear();
        await Context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE email_outbox;");
    }
}
