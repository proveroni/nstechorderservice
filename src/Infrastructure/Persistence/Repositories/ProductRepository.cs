using Microsoft.EntityFrameworkCore;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence.Contexts;

namespace OrderService.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implementa a persistência e as operações de estoque
    /// dos produtos utilizando Entity Framework Core.
    /// </summary>
    public sealed class ProductRepository : IProductRepository
    {
        private readonly OrderServiceDbContext _context;

        /// <summary>
        /// Inicializa o repositório com o contexto de persistência.
        /// </summary>
        public ProductRepository(OrderServiceDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Recupera um produto pelo identificador.
        /// </summary>
        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .SingleOrDefaultAsync(
                    product => product.Id == id,
                    cancellationToken);
        }

        /// <summary>
        /// Tenta reduzir o estoque de um produto de forma atômica,
        /// somente quando existe quantidade suficiente.
        /// </summary>
        public async Task<bool> TryDecreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
        {
            if (productId == Guid.Empty || quantity <= 0)
            {
                return false;
            }

            var affectedRows = await _context.Products
                .Where(product =>
                    product.Id == productId &&
                    product.StockQuantity >= quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        product => product.StockQuantity,
                        product => product.StockQuantity - quantity),
                    cancellationToken);

            return affectedRows == 1;
        }

        /// <summary>
        /// Tenta aumentar o estoque de um produto de forma atômica,
        /// impedindo que a quantidade ultrapasse o limite de int.
        /// </summary>
        public async Task<bool> TryIncreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
        {
            if (productId == Guid.Empty || quantity <= 0)
            {
                return false;
            }

            var affectedRows = await _context.Products
                .Where(product =>
                    product.Id == productId &&
                    product.StockQuantity <= int.MaxValue - quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        product => product.StockQuantity,
                        product => product.StockQuantity + quantity),
                    cancellationToken);

            return affectedRows == 1;
        }


    }
}
