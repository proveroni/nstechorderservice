using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Queries.GetOrderById.DTOs.Responses;
using OrderService.Application.Orders.Queries.GetOrderById.Queries;
using OrderService.CrossCutting.Results;

namespace OrderService.Application.Orders.Queries.GetOrderById.Handlers
{
    /// <summary>
    /// Executa o caso de uso de consulta de pedido por identificador.
    /// </summary>
    public sealed class GetOrderByIdHandler
    {
        private readonly IOrderRepository _orderRepository;

        /// <summary>
        /// Inicializa o handler com suas dependências.
        /// </summary>
        /// <param name="orderRepository">
        /// Repositório utilizado para consultar os pedidos.
        /// </param>
        public GetOrderByIdHandler(IOrderRepository orderRepository)
        {
            ArgumentNullException.ThrowIfNull(orderRepository);

            _orderRepository = orderRepository;
        }

        /// <summary>
        /// Consulta um pedido pelo identificador informado.
        /// </summary>
        /// <param name="request">Dados da consulta.</param>
        /// <param name="cancellationToken">
        /// Token de cancelamento da operação.
        /// </param>
        /// <returns>
        /// Resultado contendo os dados do pedido ou os erros da consulta.
        /// </returns>
        public async Task<Result<GetOrderByIdResponse>> HandleAsync(
            GetOrderByIdQuery request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result<GetOrderByIdResponse>.Failure(
                    new Error(
                        "order.invalid_request",
                        "Os dados da consulta são obrigatórios."));
            }

            if (request.Id == Guid.Empty)
            {
                return Result<GetOrderByIdResponse>.Failure(
                    new Error(
                        "order.invalid_id",
                        "O identificador do pedido é obrigatório."));
            }

            var order = await _orderRepository.GetByIdAsync(
                request.Id,
                cancellationToken);

            if (order is null)
            {
                return Result<GetOrderByIdResponse>.Failure(
                    new Error(
                        "order.not_found",
                        "O pedido informado não foi encontrado."));
            }

            var items = order.Items
                .Select(item => new GetOrderByIdItemResponse(
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice))
                .ToArray();

            var response = new GetOrderByIdResponse(
                order.Id,
                order.CustomerId,
                order.Currency,
                order.Status,
                order.Total,
                order.CreatedAt,
                order.ConfirmedAt,
                order.CanceledAt,
                items);

            return Result<GetOrderByIdResponse>.Success(response);
        }
    }
}
