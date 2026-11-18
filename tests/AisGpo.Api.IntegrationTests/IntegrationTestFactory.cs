using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AisGpo.Api.Controllers;
using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace AisGpo.Api.IntegrationTests;

public sealed class IntegrationTestFactory :
    WebApplicationFactory<ProjectsController>,
    IAsyncLifetime
{
    public const string JwtIssuer =
        "AisGpo.IntegrationTests";

    public const string JwtAudience =
        "AisGpo.IntegrationTests.Client";

    public const string JwtSecret =
        "integration-tests-secret-at-least-32-bytes";

    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("ais_gpo_tests")
            .WithUsername("ais_gpo")
            .WithPassword("ais_gpo")
            .Build();

    private readonly string storageRoot =
        Path.Combine(
            Path.GetTempPath(),
            "ais-gpo-integration-tests",
            Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, string?>
        previousEnvironment = new();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

        SetEnvironmentVariable(
            "DOTNET_ENVIRONMENT",
            "Testing");

        SetEnvironmentVariable(
            "ASPNETCORE_ENVIRONMENT",
            "Testing");

        SetEnvironmentVariable(
            "ConnectionStrings__Postgres",
            postgres.GetConnectionString());

        SetEnvironmentVariable(
            "Jwt__Issuer",
            JwtIssuer);

        SetEnvironmentVariable(
            "Jwt__Audience",
            JwtAudience);

        SetEnvironmentVariable(
            "Jwt__Secret",
            JwtSecret);

        SetEnvironmentVariable(
            "Jwt__ExpiresMinutes",
            "30");

        SetEnvironmentVariable(
            "DevSeed__Enabled",
            "false");

        SetEnvironmentVariable(
            "FileStorage__RootPath",
            storageRoot);
    }

    public HttpClient CreateAuthenticatedClient(
        long userId,
        UserRole role)
    {
        var client =
            CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

        var now =
            DateTime.UtcNow;

        var claims =
            new[]
            {
                new Claim(
                    "uid",
                    userId.ToString()),

                new Claim(
                    ClaimTypes.Role,
                    role.ToString())
            };

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    JwtSecret));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: JwtIssuer,
                audience: JwtAudience,
                claims: claims,
                notBefore: now.AddMinutes(-1),
                expires: now.AddMinutes(30),
                signingCredentials: credentials);

        var encodedToken =
            new JwtSecurityTokenHandler()
                .WriteToken(token);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                encodedToken);

        return client;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        await postgres.DisposeAsync();

        if (Directory.Exists(storageRoot))
        {
            Directory.Delete(
                storageRoot,
                recursive: true);
        }

        foreach (var item in previousEnvironment)
        {
            Environment.SetEnvironmentVariable(
                item.Key,
                item.Value);
        }
    }

    private void SetEnvironmentVariable(
        string name,
        string value)
    {
        if (!previousEnvironment.ContainsKey(name))
        {
            previousEnvironment[name] =
                Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(
            name,
            value);
    }
}