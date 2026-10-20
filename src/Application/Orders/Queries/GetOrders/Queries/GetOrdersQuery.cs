using OrderService.Domain.Enums;

namespace OrderService.Application.Orders.Queries.GetOrders.Queries
{
    /// <summary>
    /// Representa os critérios de filtragem e paginação
    /// utilizados na consulta de pedidos.
    /// </summary>
    public sealed class GetOrdersQuery
    {
        /// <summary>
        /// Identificador do cliente utilizado como filtro.
        /// </summary>
        public Guid? CustomerId { get; init; }

        /// <summary>
        /// Situação do pedido utilizada como filtro.
        /// </summary>
        public OrderStatusEnum? Status { get; init; }

        /// <summary>
        /// Data inicial de criação dos pedidos, inclusive.
        /// </summary>
        public DateTimeOffset? From { get; init; }

        /// <summary>
        /// Data final de criação dos pedidos, inclusive.
        /// </summary>
        public DateTimeOffset? To { get; init; }

        /// <summary>
        /// Número da página solicitada, iniciado em 1.
        /// </summary>
        public int Page { get; init; } = 1;

        /// <summary>
        /// Quantidade máxima de pedidos por página.
        /// </summary>
        public int PageSize { get; init; } = 20;
    }
}
