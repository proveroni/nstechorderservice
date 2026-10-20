using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Queries.GetOrderById.DTOs.Responses;
using OrderService.Application.Orders.Queries.GetOrders.Queries;
using OrderService.CrossCutting.Results;

namespace OrderService.Application.Orders.Queries.GetOrders.Handlers
{
    /// <summary>
    /// Executa o caso de uso de consulta paginada de pedidos.
    /// </summary>
    public sealed class GetOrdersHandler
    {
        private const int MaxPageSize = 100;

        private readonly IOrderRepository _orderRepository;

        /// <summary>
        /// Inicializa o handler com suas dependências.
        /// </summary>
        /// <param name="orderRepository">
        /// Repositório utilizado para consultar os pedidos.
        /// </param>
        public GetOrdersHandler(IOrderRepository orderRepository)
        {
            ArgumentNullException.ThrowIfNull(orderRepository);

            _orderRepository = orderRepository;
        }

        /// <summary>
        /// Consulta pedidos utilizando filtros opcionais e paginação.
        /// </summary>
        /// <param name="request">Critérios da consulta.</param>
        /// <param name="cancellationToken">
        /// Token de cancelamento da operação.
        /// </param>
        /// <returns>
        /// Resultado contendo os pedidos e os metadados da paginação,
        /// ou os erros de validação.
        /// </returns>
        public async Task<Result<PagedResult<GetOrderByIdResponse>>> HandleAsync(
            GetOrdersQuery request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result<PagedResult<GetOrderByIdResponse>>.Failure(
                    new Error(
                        "order.invalid_request",
                        "Os dados da consulta são obrigatórios."));
            }

            if (request.CustomerId == Guid.Empty)
            {
                return Result<PagedResult<GetOrderByIdResponse>>.Failure(
                    new Error(
                        "order.invalid_customer_id",
                        "O identificador do cliente é inválido."));
            }

            if (request.Status.HasValue &&
                !Enum.IsDefined(request.Status.Value))
            {
                return Result<PagedResult<GetOrderByIdResponse>>.Failure(
                    new Error(
                        "order.invalid_status",
                        "A situação do pedido é inválida."));
            }

            if (request.From.HasValue &&
                request.To.HasValue &&
                request.From.Value > request.To.Value)
            {
                return Result<PagedResult<GetOrderByIdResponse>>.Failure(
                    new Error(
                        "order.invalid_date_range",
                        "A data inicial não pode ser posterior à data final."));
            }

            if (request.Page < 1)
            {
                return Result<PagedResult<GetOrderByIdResponse>>.Failure(
                    new Error(
                        "order.invalid_page",
                        "O número da página deve ser maior ou igual a 1."));
            }

            if (request.PageSize < 1 || request.PageSize > MaxPageSize)
            {
                return Result<PagedResult<GetOrderByIdResponse>>.Failure(
                    new Error(
                        "order.invalid_page_size",
                        $"A quantidade de pedidos por página deve estar entre 1 e {MaxPageSize}."));
            }

            if ((long)(request.Page - 1) * request.PageSize > int.MaxValue)
            {
                return Result<PagedResult<GetOrderByIdResponse>>.Failure(
                    new Error(
                        "order.invalid_page",
                        "A página solicitada excede o limite permitido."));
            }

            var result = await _orderRepository.GetPagedAsync(
                request.CustomerId,
                request.Status,
                request.From,
                request.To,
                request.Page,
                request.PageSize,
                cancellationToken);

            var items = result.Items
                .Select(order => new GetOrderByIdResponse(
                    order.Id,
                    order.CustomerId,
                    order.Currency,
                    order.Status,
                    order.Total,
                    order.CreatedAt,
                    order.ConfirmedAt,
                    order.CanceledAt,
                    order.Items
                        .Select(item => new GetOrderByIdItemResponse(
                            item.ProductId,
                            item.Quantity,
                            item.UnitPrice))
                        .ToArray()))
                .ToArray();

            var response = new PagedResult<GetOrderByIdResponse>
            {
                Items = items,
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount
            };

            return Result<PagedResult<GetOrderByIdResponse>>.Success(response);
        }
    }
}
