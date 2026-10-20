using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence.Contexts;

namespace OrderService.Infrastructure.Persistence.Seeding
{
    /// <summary>
    /// Inicializa os dados de exemplo utilizados no ambiente de desenvolvimento.
    /// </summary>
    public static class DevelopmentDataSeeder
    {
        /// <summary>
        /// Insere os produtos de exemplo que ainda não existem no banco de dados.
        /// </summary>
        /// <param name="dbContext">Contexto de persistência.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        public static async Task SeedAsync
            (
                OrderServiceDbContext dbContext,
                CancellationToken cancellationToken = default
            )
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            var products = new[]
            {
            CreateProduct(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                "Teclado Mecânico",
                250.00m,
                "BRL",
                10),

            CreateProduct(
                Guid.Parse("22222222-2222-4222-8222-222222222222"),
                "Mouse Sem Fio",
                120.00m,
                "BRL",
                20),

            CreateProduct(
                Guid.Parse("33333333-3333-4333-8333-333333333333"),
                "Monitor 27 Polegadas",
                1500.00m,
                "BRL",
                5)
        };

            var productIds = products
                .Select(product => product.Id)
                .ToArray();

            var existingIds = await dbContext.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .Select(product => product.Id)
                .ToListAsync(cancellationToken);

            var existingIdSet = existingIds.ToHashSet();

            var missingProducts = products
                .Where(product => !existingIdSet.Contains(product.Id))
                .ToArray();

            if (missingProducts.Length == 0)
            {
                return;
            }

            await dbContext.Products.AddRangeAsync(missingProducts, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Cria um produto de exemplo utilizando as validações do domínio.
        /// </summary>
        /// <param name="id">Identificador do produto.</param>
        /// <param name="name">Nome do produto.</param>
        /// <param name="price">Preço atual do produto.</param>
        /// <param name="currency">Código da moeda do produto.</param>
        /// <param name="stockQuantity">Quantidade inicial em estoque.</param>
        /// <returns>Produto de exemplo validado.</returns>
        private static Product CreateProduct
            (
                Guid id,
                string name,
                decimal price,
                string currency,
                int stockQuantity
            )
        {
            var result = Product.Create
                (
                    id,
                    name,
                    price,
                    currency,
                    stockQuantity
                );

            if (result.IsFailure)
            {
                throw new InvalidOperationException($"O produto de desenvolvimento '{name}' possui dados inválidos.");
            }

            return result.Value;
        }
    }
}
