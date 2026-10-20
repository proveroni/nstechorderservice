using OrderService.Api.Features.Orders.GetOrderById.DTOs;
using OrderService.Application.Orders.Queries.GetOrderById.DTOs.Responses;

namespace OrderService.Api.Features.Orders.GetOrderById.Mappings
{
    /// <summary>
    /// Realiza o mapeamento da resposta do caso de uso de consulta
    /// de pedido para o contrato HTTP da API.
    /// </summary>
    public static class GetOrderByIdResponseMapper
    {
        /// <summary>
        /// Converte a resposta da Application para a resposta HTTP.
        /// </summary>
        /// <param name="response">
        /// Dados do pedido retornados pelo caso de uso.
        /// </param>
        /// <returns>
        /// Representação HTTP do pedido e seus itens.
        /// </returns>
        public static GetOrderByIdHttpResponse ToHttpResponse(
            GetOrderByIdResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            var items = response.Items
                .Select(item => new GetOrderByIdItemHttpResponse(
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice))
                .ToArray();

            return new GetOrderByIdHttpResponse(
                response.Id,
                response.CustomerId,
                response.Currency,
                response.Status,
                response.Total,
                response.CreatedAt,
                response.ConfirmedAt,
                response.CanceledAt,
                items);
        }
    }
}
