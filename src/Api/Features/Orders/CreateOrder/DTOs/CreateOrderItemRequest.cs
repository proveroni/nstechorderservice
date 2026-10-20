namespace OrderService.Api.Features.Orders.CreateOrder.DTOs
{
    /// <summary>
    /// Representa um item recebido na requisição HTTP de criação de pedido.
    /// </summary>
    /// <param name="ProductId">Identificador do produto.</param>
    /// <param name="Quantity">Quantidade solicitada.</param>
    public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);
}
