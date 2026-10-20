namespace OrderService.Application.Orders.Operations.CreateOrder.DTOs
{
    /// <summary>
    /// Representa um item solicitado na criação de um pedido.
    /// </summary>
    /// <param name="ProductId">Identificador do produto.</param>
    /// <param name="Quantity">Quantidade solicitada.</param>
    public sealed record CreateOrderItemData(
        Guid ProductId,
        int Quantity);
}
