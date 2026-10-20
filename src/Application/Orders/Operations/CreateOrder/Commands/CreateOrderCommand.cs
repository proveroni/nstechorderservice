using OrderService.Application.Orders.Operations.CreateOrder.DTOs;

namespace OrderService.Application.Orders.Operations.CreateOrder.Commands
{
    /// <summary>
    /// Representa os dados necessários para criar um pedido.
    /// </summary>
    /// <param name="CustomerId">Identificador do cliente.</param>
    /// <param name="Currency">Código ISO 4217 da moeda do pedido.</param>
    /// <param name="Items">Itens solicitados.</param>
    public sealed record CreateOrderCommand(
        Guid CustomerId,
        string Currency,
        IReadOnlyCollection<CreateOrderItemData> Items);
}