using Xunit;

namespace AisGpo.Api.IntegrationTests;

/// <summary>
/// Все integration tests используют
/// один API host и один PostgreSQL-контейнер.
///
/// Последовательное выполнение необходимо,
/// потому что environment variables являются
/// глобальными для тестового процесса.
/// </summary>
[CollectionDefinition(
    Name,
    DisableParallelization = true)]
public sealed class IntegrationTestCollection :
    ICollectionFixture<IntegrationTestFactory>
{
    public const string Name =
        "Integration tests";
}