using OrderService.Domain.Enums;

namespace OrderService.Api.Features.Orders.GetOrderById.DTOs
{
    /// <summary>
    /// Representa a resposta HTTP da consulta de um pedido por identificador.
    /// </summary>
    /// <param name="Id">Identificador do pedido.</param>
    /// <param name="CustomerId">Identificador do cliente.</param>
    /// <param name="Currency">Moeda do pedido, representada por um código de três letras.</param>
    /// <param name="Status">Estado atual do pedido.</param>
    /// <param name="Total">Valor total do pedido.</param>
    /// <param name="CreatedAt">Data e hora de criação do pedido.</param>
    /// <param name="ConfirmedAt">Data e hora de confirmação, quando houver.</param>
    /// <param name="CanceledAt">Data e hora de cancelamento, quando houver.</param>
    /// <param name="Items">Itens pertencentes ao pedido.</param>
    public sealed record GetOrderByIdHttpResponse(
        Guid Id,
        Guid CustomerId,
        string Currency,
        OrderStatusEnum Status,
        decimal Total,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? CanceledAt,
        IReadOnlyCollection<GetOrderByIdItemHttpResponse> Items);
}