using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Application.Common.Interfaces.Identity;
using Application.Models.Profile;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;

namespace Api.Tests.Integration.Users;

public class ProfileTests : BaseIntegrationTest, IAsyncLifetime
{
    private const string OriginalPassword = "Original!Pass1";

    private readonly string _email = $"profile-{Guid.NewGuid():N}@zavod.ua";
    private readonly IntegrationTestWebFactory _factory;
    private Guid _userId;

    public ProfileTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldReturnIdentityForCurrentUser()
    {
        var response = await SendAsync(HttpMethod.Get, "api/v1/profile/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.ToResponseModel<MyProfileInfo>();
        profile.UserId.Should().Be(_userId);
        profile.Email.Should().Be(_email);
        profile.Name.Should().Be("Alex");
        profile.FamilyName.Should().Be("Petrenko");
        profile.EmailConfirmed.Should().BeTrue();
        profile.Memberships.Should().BeEmpty("no memberships seeded for this user");
    }

    [Fact]
    public async Task ShouldUpdateNameAndFamilyName()
    {
        var update = new UpdateMyProfileDto(Name: "Олексій", FamilyName: "Петренко");
        var response = await SendAsync(HttpMethod.Patch, "api/v1/profile/me", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.ToResponseModel<MyProfileInfo>();
        profile.Name.Should().Be("Олексій");
        profile.FamilyName.Should().Be("Петренко");

        // Re-fetch to confirm persistence.
        var dbUser = await Context.Set<User>().AsNoTracking().FirstAsync(u => u.Id == _userId);
        dbUser.Name.Should().Be("Олексій");
        dbUser.FamilyName.Should().Be("Петренко");
    }

    [Fact]
    public async Task ShouldRejectChangePasswordWithWrongCurrent()
    {
        var change = new ChangeMyPasswordDto(CurrentPassword: "WrongOld!", NewPassword: "BrandNew!Pass2");
        var response = await SendAsync(HttpMethod.Post, "api/v1/profile/change-password", change);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);

        // Old password still verifies — nothing changed.
        (await VerifyPasswordAsync(OriginalPassword)).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldChangePasswordWithCorrectCurrent()
    {
        const string newPassword = "BrandNew!Pass2";
        var change = new ChangeMyPasswordDto(CurrentPassword: OriginalPassword, NewPassword: newPassword);
        var response = await SendAsync(HttpMethod.Post, "api/v1/profile/change-password", change);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await VerifyPasswordAsync(newPassword)).Should().BeTrue();
        (await VerifyPasswordAsync(OriginalPassword)).Should().BeFalse();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.UserIdOverrideHeader, _userId.ToString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    private async Task<bool> VerifyPasswordAsync(string password)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var user = (await um.GetUserByEmail(_email)).Match(u => u, () => null!);
        return await um.IsPasswordValidAsync(user, password);
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var scopedDb = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = _email,
            Email = _email,
            EmailConfirmed = true,
            Name = "Alex",
            FamilyName = "Petrenko"
        };
        var created = await um.CreateUser(user, OriginalPassword);
        created.Succeeded.Should().BeTrue(
            string.Join("; ", created.Errors.Select(e => e.Description)));
        await scopedDb.SaveChangesAsync();
        _userId = user.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
