using OrderService.Domain.Enums;

namespace OrderService.Application.Orders.Queries.GetOrderById.DTOs.Responses
{
    /// <summary>
    /// Representa os dados de um pedido retornados pelo caso de uso
    /// de consulta por identificador.
    /// </summary>
    /// <param name="Id">Identificador do pedido.</param>
    /// <param name="CustomerId">Identificador do cliente.</param>
    /// <param name="Currency">Código da moeda do pedido, com três letras maiúsculas.</param>
    /// <param name="Status">Estado atual do pedido.</param>
    /// <param name="Total">Valor total do pedido.</param>
    /// <param name="CreatedAt">Data e hora de criação do pedido.</param>
    /// <param name="ConfirmedAt">Data e hora de confirmação, quando houver.</param>
    /// <param name="CanceledAt">Data e hora de cancelamento, quando houver.</param>
    /// <param name="Items">Itens pertencentes ao pedido.</param>
    public sealed record GetOrderByIdResponse(
        Guid Id,
        Guid CustomerId,
        string Currency,
        OrderStatusEnum Status,
        decimal Total,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? CanceledAt,
        IReadOnlyCollection<GetOrderByIdItemResponse> Items);
}
