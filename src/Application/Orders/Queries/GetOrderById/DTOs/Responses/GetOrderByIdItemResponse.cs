namespace OrderService.Application.Orders.Queries.GetOrderById.DTOs.Responses
{
    /// <summary>
    /// Representa um item de pedido retornado pelo caso de uso
    /// de consulta de pedido por identificador.
    /// </summary>
    /// <param name="ProductId">Identificador do produto.</param>
    /// <param name="Quantity">Quantidade do produto no pedido.</param>
    /// <param name="UnitPrice">Preço unitário registrado no pedido.</param>
    public sealed record GetOrderByIdItemResponse(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice);
}
