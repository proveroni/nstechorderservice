namespace OrderService.Api.Features.Orders.CreateOrder.DTOs
{
    /// <summary>
    /// Representa os dados recebidos na requisição HTTP de criação de pedido.
    /// </summary>
    /// <param name="CustomerId">Identificador do cliente.</param>
    /// <param name="Currency">Código ISO 4217 da moeda do pedido.</param>
    /// <param name="Items">Itens solicitados.</param>
    public sealed record CreateOrderRequest(
        Guid CustomerId,
        string Currency,
        IReadOnlyCollection<CreateOrderItemRequest>? Items);
}