using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Auth.Operations.GenerateToken.Handlers;
using OrderService.Application.Orders.Operations.CancelOrder.Handlers;
using OrderService.Application.Orders.Operations.ConfirmOrder.Handlers;
using OrderService.Application.Orders.Operations.CreateOrder.Handlers;
using OrderService.Application.Orders.Queries.GetOrderById.Handlers;
using OrderService.Application.Orders.Queries.GetOrders.Handlers;

namespace OrderService.Application.DI
{
    /// <summary>
    /// Centraliza o registro das dependências da camada Application.
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Registra os handlers e serviços da camada Application.
        /// </summary>
        /// <param name="services">Coleção de serviços da aplicação.</param>
        /// <returns>A coleção de serviços configurada.</returns>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            //Auth
            services.AddScoped<GenerateTokenHandler>();

            // Orders
            services.AddScoped<CreateOrderHandler>();
            services.AddScoped<ConfirmOrderHandler>();
            services.AddScoped<CancelOrderHandler>();
            services.AddScoped<GetOrderByIdHandler>();
            services.AddScoped<GetOrdersHandler>();


            return services;
        }
    }
}
