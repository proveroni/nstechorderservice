using OrderService.Domain.Enums;

namespace OrderService.Api.Features.Orders.CreateOrder.DTOs
{
    /// <summary>
    /// Representa os dados retornados pela API após a criação de um pedido.
    /// </summary>
    /// <param name="Id">Identificador do pedido criado.</param>
    /// <param name="CustomerId">Identificador do cliente.</param>
    /// <param name="Currency">Código ISO 4217 da moeda do pedido.</param>
    /// <param name="Status">Estado inicial do pedido.</param>
    /// <param name="Total">Valor total do pedido.</param>
    /// <param name="CreatedAt">Data e hora de criação do pedido.</param>
    public sealed record CreateOrderHttpResponse(
        Guid Id,
        Guid CustomerId,
        string Currency,
        OrderStatusEnum Status,
        decimal Total,
        DateTimeOffset CreatedAt);
}