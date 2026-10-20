using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Application.Abstractions.Persistence
{
    /// <summary>
    /// Define as operações de persistência de pedidos
    /// necessárias à camada de aplicação.
    /// </summary>
    public interface IOrderRepository
    {
        /// <summary>
        /// Obtém um pedido pelo seu identificador.
        /// </summary>
        /// <param name="id">Identificador do pedido.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação assíncrona.
        /// </param>
        /// <returns>
        /// Pedido encontrado ou null quando não existir.
        /// </returns>
        Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);


        /// <summary>
        /// Obtém um pedido para atualização, garantindo que
        /// operações concorrentes sobre o mesmo pedido sejam
        /// serializadas durante a transação.
        /// </summary>
        /// <remarks>
        /// Deve ser utilizado dentro de uma transação ativa.
        /// O bloqueio da linha é mantido até o encerramento
        /// da transação.
        /// </remarks>
        /// <param name="id">Identificador do pedido.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação assíncrona.
        /// </param>
        /// <returns>
        /// Pedido encontrado ou null quando não existir.
        /// </returns>
        Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);


        /// <summary>
        /// Adiciona um novo pedido ao contexto de persistência.
        /// </summary>
        /// <param name="order">Pedido a ser adicionado.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação assíncrona.
        /// </param>
        Task AddAsync(Order order, CancellationToken cancellationToken = default);


        /// <summary>
        /// Consulta pedidos utilizando filtros opcionais e paginação.
        /// </summary>
        /// <param name="customerId">Identificador opcional do cliente.</param>
        /// <param name="status">Situação opcional do pedido.</param>
        /// <param name="from">Data inicial de criação, inclusive.</param>
        /// <param name="to">Data final de criação, inclusive.</param>
        /// <param name="page">Número da página, iniciado em 1.</param>
        /// <param name="pageSize">Quantidade de pedidos por página.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Pedidos encontrados e metadados da paginação.</returns>
        Task<PagedResult<Order>> GetPagedAsync(
            Guid? customerId,
            OrderStatusEnum? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
