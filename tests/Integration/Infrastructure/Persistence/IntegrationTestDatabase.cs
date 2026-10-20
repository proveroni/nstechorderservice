using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Persistence.Contexts;

namespace OrderService.Infrastructure.IntegrationTests.Persistence
{
    /// <summary>
    /// Centraliza a configuração de acesso ao PostgreSQL
    /// utilizado pelos testes de integração.
    /// </summary>
    internal static class IntegrationTestDatabase
    {
        private static string? _connectionString;


        /// <summary>
        /// Configura a conexão com o PostgreSQL iniciado pelo Testcontainers.
        /// </summary>
        /// <param name="connectionString">Conexão fornecida pelo contêiner.</param>
        public static void ConfigureConnectionString(string connectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            _connectionString = connectionString;
        }


        /// <summary>
        /// Cria uma instância do contexto de persistência
        /// configurada para o banco de testes.
        /// </summary>
        /// <returns>Contexto configurado para os testes de integração.</returns>
        public static OrderServiceDbContext CreateContext()
        {
            var connectionString = _connectionString
                    ?? throw new InvalidOperationException(
                        "O PostgreSQL de testes ainda não foi inicializado.");

            var options = new DbContextOptionsBuilder<OrderServiceDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new OrderServiceDbContext(options);
        }

        /// <summary>
        /// Cria o banco de testes, quando necessário, e aplica as migrations pendentes.
        /// </summary>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        public static async Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            await using var context = CreateContext();

            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}