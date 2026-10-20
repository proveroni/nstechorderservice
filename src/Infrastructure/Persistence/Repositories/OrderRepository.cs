using Microsoft.EntityFrameworkCore;
using OrderService.Application.Abstractions.Persistence;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Persistence.Contexts;

namespace OrderService.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implementa a persistência do agregado Order
    /// utilizando Entity Framework Core.
    /// </summary>
    public sealed class OrderRepository : IOrderRepository
    {
        private readonly OrderServiceDbContext _context;

        /// <summary>
        /// Inicializa o repositório com o contexto de persistência.
        /// </summary>
        public OrderRepository(OrderServiceDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Recupera um pedido pelo identificador,
        /// incluindo os itens pertencentes ao agregado.
        /// </summary>
        public async Task<Order?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .Include(order => order.Items)
                .SingleOrDefaultAsync(
                    order => order.Id == id,
                    cancellationToken);
        }

        /// <summary>
        /// Obtém um pedido com bloqueio de linha para atualização.
        /// Deve ser executado dentro de uma transação ativa.
        /// </summary>
        /// <param name="id">Identificador do pedido.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação assíncrona.
        /// </param>
        /// <returns>
        /// Pedido encontrado ou null quando não existir.
        /// </returns>
        public async Task<Order?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            Order? order = null;

            var query = _context.Orders
                .FromSqlInterpolated(
                    $"SELECT * FROM orders WHERE id = {id} FOR UPDATE")
                .AsAsyncEnumerable();

            await foreach (var item in query.WithCancellation(cancellationToken))
            {
                order = item;
                break;
            }

            if (order is null)
            {
                return null;
            }

            await _context.Entry(order)
                .Collection(o => o.Items)
                .LoadAsync(cancellationToken);

            return order;
        }

        /// <summary>
        /// Consulta pedidos utilizando filtros opcionais e paginação.
        /// </summary>
        /// <param name="customerId">Identificador opcional do cliente.</param>
        /// <param name="status">Situação opcional do pedido.</param>
        /// <param name="from">Data inicial de criação, inclusive.</param>
        /// <param name="to">Data final de criação, inclusive.</param>
        /// <param name="page">Número da página, iniciado em 1.</param>
        /// <param name="pageSize">Quantidade de pedidos por página.</param>
        /// <param name="cancellationToken">Token para cancelamento da operação.</param>
        /// <returns>
        /// Pedidos encontrados e informações da paginação.
        /// </returns>
        public async Task<PagedResult<Order>> GetPagedAsync(
            Guid? customerId,
            OrderStatusEnum? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IQueryable<Order> query = _context.Orders.AsNoTracking();

            if (customerId.HasValue)
            {
                query = query.Where(order =>
                    order.CustomerId == customerId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(order =>
                    order.Status == status.Value);
            }

            if (from.HasValue)
            {
                query = query.Where(order =>
                    order.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(order =>
                    order.CreatedAt <= to.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(order => order.CreatedAt)
                .ThenBy(order => order.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(order => order.Items)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return new PagedResult<Order>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        /// <summary>
        /// Adiciona um pedido ao contexto para persistência
        /// na próxima confirmação da unidade de trabalho.
        /// </summary>
        public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            await _context.Orders.AddAsync(order, cancellationToken);
        }
    }
}
