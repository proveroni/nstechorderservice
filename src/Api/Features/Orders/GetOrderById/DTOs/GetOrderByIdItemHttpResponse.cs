namespace OrderService.Api.Features.Orders.GetOrderById.DTOs
{
    /// <summary>
    /// Representa um item de pedido na resposta HTTP
    /// da consulta por identificador.
    /// </summary>
    /// <param name="ProductId">Identificador do produto.</param>
    /// <param name="Quantity">Quantidade do produto no pedido.</param>
    /// <param name="UnitPrice">Preço unitário registrado no pedido.</param>
    public sealed record GetOrderByIdItemHttpResponse(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice);
}
