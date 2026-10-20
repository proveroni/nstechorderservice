namespace OrderService.Api.Configurations
{
    /// <summary>
    /// Configura os serviços MVC utilizados pela API.
    /// </summary>
    public static class MvcConfiguration
    {
        /// <summary>
        /// Registra os serviços necessários aos controllers.
        /// </summary>
        /// <param name="services">Coleção de serviços.</param>
        /// <returns>A coleção de serviços configurada.</returns>
        public static IServiceCollection AddApiMvc(this IServiceCollection services)
        {
            services.AddControllers();

            return services;
        }
    }
}
