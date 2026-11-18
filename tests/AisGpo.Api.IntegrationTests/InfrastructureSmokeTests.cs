using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AisGpo.Api.IntegrationTests;

/// <summary>
/// Проверяет базовую работоспособность
/// integration-test инфраструктуры.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class InfrastructureSmokeTests
{
    private readonly HttpClient client;

    public InfrastructureSmokeTests(
        IntegrationTestFactory factory)
    {
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    /// <summary>
    /// Если приложение реально запустилось,
    /// PostgreSQL доступен и migrations применились,
    /// защищённый endpoint должен вернуть 401,
    /// а не ошибку запуска приложения.
    /// </summary>
    [Fact]
    public async Task Projects_WithoutToken_ReturnsUnauthorized()
    {
        var response =
            await client.GetAsync(
                "/api/v1/projects");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}