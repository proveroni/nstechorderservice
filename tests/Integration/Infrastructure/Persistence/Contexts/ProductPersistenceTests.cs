using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.IntegrationTests.Persistence.Contexts
{
    /// <summary>
    /// Testes de integração da persistência de produtos
    /// utilizando PostgreSQL real.
    /// </summary>
    public sealed class ProductPersistenceTests
    {
        [Fact]
        public async Task Product_ShouldBePersistedAndRetrieved()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto de teste",
                125.50m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);


            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(productResult.Value);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Act
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var persistedProduct = await context.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId,
                                     TestContext.Current.CancellationToken);

                    // Assert
                    Assert.Equal(productId, persistedProduct.Id);
                    Assert.Equal("Produto de teste", persistedProduct.Name);
                    Assert.Equal(125.50m, persistedProduct.Price);
                    Assert.Equal("BRL", persistedProduct.Currency);
                    Assert.Equal(10, persistedProduct.StockQuantity);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                var persistedProduct = await context.Products
                    .SingleOrDefaultAsync(product => product.Id == productId,
                                          TestContext.Current.CancellationToken);

                if (persistedProduct is not null)
                {
                    context.Products.Remove(persistedProduct);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }
        }
    }
}
