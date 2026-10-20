using OrderService.Api.Configurations;
using OrderService.Api.Features.Auth.Services;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.DI;
using OrderService.Infrastructure.DI;

namespace OrderService.Api.DI
{
    /// <summary>
    /// Centraliza a composição das dependências da API.
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Registra as dependências necessárias à execução da API.
        /// </summary>
        /// <param name="services">Coleção de serviços.</param>
        /// <param name="configuration">Configurações da aplicação.</param>
        /// <returns>A coleção de serviços configurada.</returns>
        public static IServiceCollection AddApiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddApplication();
            services.AddInfrastructure(configuration);

            services.AddSingleton(TimeProvider.System);

            services.AddApiAuthentication(configuration);

            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, HttpCurrentUser>();

            services.AddApiMvc();
            services.AddApiOpenApi();

            return services;
        }
    }
}
