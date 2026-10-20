using OrderService.Domain.Entities;

namespace OrderService.Application.Abstractions.Persistence
{
    /// <summary>
    /// Define as operações de consulta e movimentação de estoque
    /// necessárias aos casos de uso de pedidos.
    /// </summary>
    public interface IProductRepository
    {
        /// <summary>
        /// Obtém um produto pelo identificador.
        /// </summary>
        /// <param name="id">Identificador do produto.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação.
        /// </param>
        /// <returns>
        /// Produto encontrado ou null quando não existir.
        /// </returns>
        Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tenta reduzir o estoque de um produto de forma condicional,
        /// sem permitir que a quantidade disponível fique negativa.
        /// </summary>
        /// <param name="productId">Identificador do produto.</param>
        /// <param name="quantity">Quantidade a retirar.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação.
        /// </param>
        /// <returns>
        /// True quando a atualização foi realizada;
        /// false quando a condição não foi atendida.
        /// </returns>
        Task<bool> TryDecreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tenta repor o estoque de um produto sem ultrapassar
        /// o limite máximo permitido para a quantidade.
        /// </summary>
        /// <param name="productId">Identificador do produto.</param>
        /// <param name="quantity">Quantidade a repor.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação.
        /// </param>
        /// <returns>
        /// True quando a atualização foi realizada;
        /// false quando a condição não foi atendida.
        /// </returns>
        Task<bool> TryIncreaseStockAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
    }
}
