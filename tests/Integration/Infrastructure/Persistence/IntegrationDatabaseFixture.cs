
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace OrderService.Infrastructure.IntegrationTests.Persistence
{
    /// <summary>
    /// Gerencia o ciclo de vida do PostgreSQL utilizado nos testes de integração.
    /// </summary>
    public sealed class IntegrationDatabaseFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;

        /// <summary>
        /// Inicia o PostgreSQL e aplica as migrations necessárias.
        /// </summary>
        public async ValueTask InitializeAsync()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Testing.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            var section = configuration.GetSection("IntegrationTests:PostgreSql");

            var image = section["Image"]
                ?? throw new InvalidOperationException("Imagem PostgreSQL não configurada.");

            var database = section["Database"]
                ?? throw new InvalidOperationException("Banco PostgreSQL não configurado.");

            var username = section["Username"]
                ?? throw new InvalidOperationException("Usuário PostgreSQL não configurado.");

            var password = section["Password"]
                ?? throw new InvalidOperationException("Senha PostgreSQL não configurada.");

            _container = new PostgreSqlBuilder(image)
                .WithDatabase(database)
                .WithUsername(username)
                .WithPassword(password)
                .Build();

            await _container.StartAsync(TestContext.Current.CancellationToken);

            IntegrationTestDatabase.ConfigureConnectionString(
                _container.GetConnectionString());

            await IntegrationTestDatabase.InitializeAsync(
                TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Encerra e descarta o contêiner PostgreSQL após os testes.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }
    }
}
