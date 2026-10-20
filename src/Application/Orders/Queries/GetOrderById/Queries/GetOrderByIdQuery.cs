namespace OrderService.Application.Orders.Queries.GetOrderById.Queries
{
    /// <summary>
    /// Representa uma solicitação de consulta de pedido por identificador.
    /// </summary>
    /// <param name="Id">Identificador do pedido a ser consultado.</param>
    public sealed record GetOrderByIdQuery(Guid Id);
}
