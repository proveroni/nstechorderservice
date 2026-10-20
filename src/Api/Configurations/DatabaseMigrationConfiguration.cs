using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Persistence.Contexts;
using OrderService.Infrastructure.Persistence.Seeding;

namespace OrderService.Api.Configurations
{
    /// <summary>
    /// Configura a atualização automática do esquema do banco de dados.
    /// </summary>
    public static class DatabaseMigrationConfiguration
    {
        /// <summary>
        /// Prepara o banco de dados antes de a API começar a receber requisições,
        /// aplicando as migrations pendentes e inicializando os dados de exemplo
        /// exclusivamente no ambiente de desenvolvimento.
        /// </summary>
        /// <remarks>
        /// As migrations são verificadas em todos os ambientes.
        /// O seed de desenvolvimento é executado somente no ambiente Development,
        /// inserindo os produtos de exemplo que ainda não existem no banco de dados,
        /// sem modificar os registros existentes.
        /// </remarks>
        /// <param name="app">Instância da aplicação web.</param>
        /// <returns>Uma tarefa que representa a preparação assíncrona do banco de dados.</returns>
        public static async Task ApplyDatabaseMigrationsAsync(
            this WebApplication app)
        {
            await using var scope = app.Services.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<OrderServiceDbContext>();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderServiceDbContext>>();

            logger.LogInformation("Verificando migrations pendentes do banco de dados.");

            await dbContext.Database.MigrateAsync();

            logger.LogInformation("Migrations do banco de dados verificadas e aplicadas.");

            if (app.Environment.IsDevelopment())
            {
                logger.LogInformation("Verificando dados iniciais do ambiente de desenvolvimento.");

                await DevelopmentDataSeeder.SeedAsync(dbContext);

                logger.LogInformation("Seed de desenvolvimento verificado e aplicado quando necessário.");
            }
        }
    }
}
