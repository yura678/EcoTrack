using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Common;
using UserEntity = Domain.Entities.User.User;

namespace Api.Tests.Integration.Notifications;

public class NotificationSubscriptionControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private const string BaseRoute = "api/v1/notification-subscriptions";

    [Fact]
    public async Task ShouldCreateEmailSubscriptionAndListIt()
    {
        var request = new CreateNotificationSubscriptionDto(
            Channel: NotificationChannel.Email,
            Email: "alerts@example.com",
            WebhookUrl: null,
            WebhookSecret: null,
            EventTypes: [ComplianceEventType.LimitExceedance],
            EmissionSourceIds: null);

        var createResponse = await Client.PostAsJsonAsync(BaseRoute, request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await Client.GetAsync(BaseRoute);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await listResponse.ToResponseModel<List<NotificationSubscriptionDto>>();
        page.Should().HaveCount(1);
        page[0].Channel.Should().Be(NotificationChannel.Email);
        page[0].Email.Should().Be("alerts@example.com");
        page[0].EventTypes.Should().BeEquivalentTo([ComplianceEventType.LimitExceedance]);
        page[0].HasWebhookSecret.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldUpdateSubscriptionFiltersAndDisable()
    {
        var createResponse = await Client.PostAsJsonAsync(BaseRoute,
            new CreateNotificationSubscriptionDto(
                NotificationChannel.Email, "alerts@example.com",
                null, null, null, null));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.ToResponseModel<NotificationSubscriptionDto>();

        var updateResponse = await Client.PutAsJsonAsync($"{BaseRoute}/{created.Id}",
            new UpdateNotificationSubscriptionDto(
                Email: null,
                WebhookUrl: null,
                WebhookSecret: null,
                EventTypes: [ComplianceEventType.OutOfRangeReading],
                EmissionSourceIds: null,
                IsEnabled: false));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var row = await Context.Set<NotificationSubscription>().AsNoTracking()
            .FirstAsync(s => s.Id == created.Id);
        row.IsEnabled.Should().BeFalse();
        row.EventTypes.Should().BeEquivalentTo([ComplianceEventType.OutOfRangeReading]);
        row.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ShouldDeleteSubscription()
    {
        var createResponse = await Client.PostAsJsonAsync(BaseRoute,
            new CreateNotificationSubscriptionDto(
                NotificationChannel.Email, "alerts@example.com",
                null, null, null, null));
        var created = await createResponse.ToResponseModel<NotificationSubscriptionDto>();

        var deleteResponse = await Client.DeleteAsync($"{BaseRoute}/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var remains = await Context.Set<NotificationSubscription>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == created.Id);
        remains.Should().BeNull();
    }

    [Fact]
    public async Task ShouldRejectEmailChannelWhenEmailMissing()
    {
        var request = new CreateNotificationSubscriptionDto(
            Channel: NotificationChannel.Email,
            Email: null,
            WebhookUrl: null,
            WebhookSecret: null,
            EventTypes: null,
            EmissionSourceIds: null);

        var response = await Client.PostAsJsonAsync(BaseRoute, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldCreateWebhookSubscriptionWithoutLeakingSecret()
    {
        var request = new CreateNotificationSubscriptionDto(
            Channel: NotificationChannel.Webhook,
            Email: null,
            WebhookUrl: "https://hooks.example.com/eco",
            WebhookSecret: "0123456789abcdef0123456789abcdef",
            EventTypes: null,
            EmissionSourceIds: null);

        var createResponse = await Client.PostAsJsonAsync(BaseRoute, request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await createResponse.ToResponseModel<NotificationSubscriptionDto>();
        dto.WebhookUrl.Should().Be("https://hooks.example.com/eco");
        dto.HasWebhookSecret.Should().BeTrue();
        // Secret itself must never appear in the DTO surface.
        dto.GetType().GetProperty("WebhookSecret").Should().BeNull();
    }

    public async Task InitializeAsync()
    {
        // FK on notification_subscription.user_id requires the test user to exist. TestUserId
        // is a static Guid shared across all tests in the session, so upsert rather than insert
        // — multiple test classes / multiple test methods in this class hit the same row.
        var exists = await Context.Set<UserEntity>()
            .AnyAsync(u => u.Id == TestAuthHandler.TestUserId);
        if (!exists)
        {
            var user = new UserEntity
            {
                Id = TestAuthHandler.TestUserId,
                UserName = $"test-{TestAuthHandler.TestUserId:N}",
                NormalizedUserName = $"TEST-{TestAuthHandler.TestUserId:N}".ToUpperInvariant(),
                Email = "test@example.com",
                NormalizedEmail = "TEST@EXAMPLE.COM",
                SecurityStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true
            };
            await Context.Set<UserEntity>().AddAsync(user);
            await SaveChangesAsync();
        }
    }


    public Task DisposeAsync() => ResetTenantDataAsync();

    public NotificationSubscriptionControllerTests(IntegrationTestWebFactory factory) : base(factory)
    {
    }
}
