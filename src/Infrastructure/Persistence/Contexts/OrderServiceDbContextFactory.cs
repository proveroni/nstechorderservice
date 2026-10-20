using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrderService.Infrastructure.Persistence.Contexts
{
    /// <summary>
    /// Cria o contexto de persistência em tempo de design,
    /// permitindo gerar migrations sem depender da API.
    /// </summary>
    public sealed class OrderServiceDbContextFactory
        : IDesignTimeDbContextFactory<OrderServiceDbContext>
    {
        /// <summary>
        /// Cria uma instância do contexto para ferramentas
        /// de desenvolvimento do Entity Framework Core.
        /// </summary>
        /// <param name="args">
        /// Argumentos fornecidos pelas ferramentas do EF Core.
        /// </param>
        /// <returns>Contexto configurado para PostgreSQL.</returns>
        public OrderServiceDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("A connection string 'DefaultConnection' não foi configurada.");

            var options = new DbContextOptionsBuilder<OrderServiceDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new OrderServiceDbContext(options);
        }
    }
}