namespace OrderService.Application.Orders.Operations.ConfirmOrder.Commands
{
    /// <summary>
    /// Representa a solicitação de confirmação de um pedido.
    /// </summary>
    /// <param name="OrderId">
    /// Identificador do pedido que deverá ser confirmado.
    /// </param>
    public sealed record ConfirmOrderCommand(Guid OrderId);
}
