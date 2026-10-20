using OrderService.Api.Features.Orders.CreateOrder.DTOs;
using OrderService.Application.Orders.Operations.CreateOrder.Commands;
using OrderService.Application.Orders.Operations.CreateOrder.DTOs;

namespace OrderService.Api.Features.Orders.CreateOrder.Mappings
{
    /// <summary>
    /// Converte os dados da requisição HTTP para o comando
    /// de criação de pedido da camada Application.
    /// </summary>
    public static class CreateOrderRequestMapper
    {
        /// <summary>
        /// Converte a requisição HTTP em um comando de criação de pedido.
        /// </summary>
        /// <param name="request">Dados recebidos pela API.</param>
        /// <returns>Comando utilizado pelo caso de uso.</returns>
        public static CreateOrderCommand ToCommand(CreateOrderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            IReadOnlyCollection<CreateOrderItemData> items = request.Items is null
                ? null!
                : request.Items
                    .Select(item => new CreateOrderItemData(
                        item.ProductId,
                        item.Quantity))
                    .ToArray();

            return new CreateOrderCommand(
                request.CustomerId,
                request.Currency,
                items);
        }
    }
}
