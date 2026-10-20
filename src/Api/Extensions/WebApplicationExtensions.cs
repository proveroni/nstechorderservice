
namespace OrderService.Api.Extensions
{
    /// <summary>
    /// Centraliza a configuração do pipeline HTTP da API.
    /// </summary>
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Configura os middlewares e endpoints da aplicação.
        /// </summary>
        /// <param name="app">Aplicação web.</param>
        /// <returns>A aplicação configurada.</returns>
        public static WebApplication ConfigureApiPipeline(
            this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint(
                        "/openapi/v1.json",
                        "OrderService API v1");

                    options.RoutePrefix = "swagger";
                });
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();

            return app;
        }
    }
}
