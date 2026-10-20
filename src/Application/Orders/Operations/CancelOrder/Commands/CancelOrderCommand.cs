namespace OrderService.Application.Orders.Operations.CancelOrder.Commands
{
    /// <summary>
    /// Representa a solicitação de cancelamento de um pedido.
    /// </summary>
    /// <param name="OrderId">Identificador do pedido a cancelar.</param>
    public sealed record CancelOrderCommand(Guid OrderId);
}
