using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace OrderService.Api.Configurations
{
    /// <summary>
    /// Configura os serviços de documentação OpenAPI.
    /// </summary>
    public static class OpenApiConfiguration
    {
        /// <summary>
        /// Registra os serviços de geração do documento OpenAPI,
        /// incluindo a autenticação Bearer JWT.
        /// </summary>
        /// <param name="services">Coleção de serviços.</param>
        /// <returns>A coleção de serviços configurada.</returns>
        public static IServiceCollection AddApiOpenApi(
            this IServiceCollection services)
        {
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();

                    document.Components.SecuritySchemes ??=
                        new Dictionary<string, IOpenApiSecurityScheme>();

                    document.Components.SecuritySchemes["Bearer"] =
                        new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.Http,
                            Scheme = "bearer",
                            BearerFormat = "JWT",
                            Description = "Informe o token JWT."
                        };

                    document.Security ??= [];

                    document.Security.Add(
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                        });

                    return Task.CompletedTask;
                });
            });

            return services;
        }
    }
}