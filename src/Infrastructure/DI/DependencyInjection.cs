using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Infrastructure.Authentication;
using OrderService.Infrastructure.Persistence.Contexts;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderService.Infrastructure.Persistence.Transactions;

namespace OrderService.Infrastructure.DI
{
    /// <summary>
    /// Centraliza o registro das dependências da camada Infrastructure.
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Registra os serviços de infraestrutura e configura a persistência PostgreSQL.
        /// </summary>
        /// <param name="services">Coleção de serviços da aplicação.</param>
        /// <param name="configuration">Configurações da aplicação.</param>
        /// <returns>A coleção de serviços configurada.</returns>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("A connection string 'DefaultConnection' não foi configurada.");

            services.AddDbContext<OrderServiceDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ITransactionManager, EfTransactionManager>();
            services.AddScoped<IPasswordHasher<DemoUserOptions>, PasswordHasher<DemoUserOptions>>();
            services.AddScoped<ICredentialValidator, DemoUserCredentialValidator>();
            services.AddScoped<ITokenGenerator, JwtTokenGenerator>();

            return services;
        }
    }
}
