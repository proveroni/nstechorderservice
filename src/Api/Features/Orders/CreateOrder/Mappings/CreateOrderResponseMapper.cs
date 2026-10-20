using OrderService.Api.Features.Orders.CreateOrder.DTOs;
using OrderService.Application.Orders.Operations.CreateOrder.DTOs.Responses;

namespace OrderService.Api.Features.Orders.CreateOrder.Mappings
{
    /// <summary>
    /// Converte a resposta do caso de uso de criação de pedido
    /// para o contrato HTTP da API.
    /// </summary>
    public static class CreateOrderResponseMapper
    {
        /// <summary>
        /// Converte a resposta da Application em um DTO HTTP.
        /// </summary>
        /// <param name="response">Resposta produzida pelo caso de uso.</param>
        /// <returns>DTO que será retornado ao consumidor da API.</returns>
        public static CreateOrderHttpResponse ToHttpResponse(
            CreateOrderResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            return new CreateOrderHttpResponse(
                response.Id,
                response.CustomerId,
                response.Currency,
                response.Status,
                response.Total,
                response.CreatedAt);
        }
    }
}
