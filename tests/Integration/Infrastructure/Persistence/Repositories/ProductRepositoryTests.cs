using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence.Repositories;

namespace OrderService.Infrastructure.IntegrationTests.Persistence.Repositories
{
    /// <summary>
    /// Testes de integração do repositório de produtos
    /// utilizando PostgreSQL real.
    /// </summary>
    public sealed class ProductRepositoryTests
    {
        [Fact]
        public async Task GetByIdAsync_ShouldReturnProduct_WhenProductExists()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de repositório",
                75.50m,
                "BRL",
                20);

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
                    var repository = new ProductRepository(context);

                    var product = await repository.GetByIdAsync(
                        productId,
                        CancellationToken.None);

                    // Assert
                    Assert.NotNull(product);
                    Assert.Equal(productId, product.Id);
                    Assert.Equal("Produto para teste de repositório", product.Name);
                    Assert.Equal(75.50m, product.Price);
                    Assert.Equal(20, product.StockQuantity);

                    Assert.Equal(
                        EntityState.Unchanged,
                        context.Entry(product).State);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                var product = await context.Products
                    .SingleOrDefaultAsync(candidate => candidate.Id == productId,
                    TestContext.Current.CancellationToken);

                if (product is not null)
                {
                    context.Products.Remove(product);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenProductDoesNotExist()
        {
            // Arrange
            var productId = Guid.NewGuid();

            await using var context = IntegrationTestDatabase.CreateContext();

            var repository = new ProductRepository(context);

            // Act
            var product = await repository.GetByIdAsync(
                productId,
                CancellationToken.None);

            // Assert
            Assert.Null(product);
        }

        [Fact]
        public async Task TryDecreaseStockAsync_ShouldDecreaseStock_WhenStockIsSufficient()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para redução de estoque",
                50.00m,
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
                    var repository = new ProductRepository(context);

                    var result = await repository.TryDecreaseStockAsync(
                        productId,
                        4,
                        CancellationToken.None);

                    Assert.True(result);
                }

                // Assert: verifica o estoque diretamente no PostgreSQL.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == productId,
                            TestContext.Current.CancellationToken);

                    Assert.Equal(6, product.StockQuantity);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                await context.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task TryDecreaseStockAsync_ShouldNotDecreaseStock_WhenStockIsInsufficient()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto com estoque insuficiente",
                50.00m,
                "BRL",
                3);

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
                    var repository = new ProductRepository(context);

                    var result = await repository.TryDecreaseStockAsync(
                        productId,
                        4,
                        CancellationToken.None);

                    Assert.False(result);
                }

                // Assert: nenhuma unidade foi consumida.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == productId,
                            TestContext.Current.CancellationToken);

                    Assert.Equal(3, product.StockQuantity);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                await context.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task TryDecreaseStockAsync_ShouldReturnFalse_WhenProductDoesNotExist()
        {
            // Arrange
            var productId = Guid.NewGuid();

            await using var context = IntegrationTestDatabase.CreateContext();

            var repository = new ProductRepository(context);

            // Act
            var result = await repository.TryDecreaseStockAsync(
                productId,
                1,
                CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task TryDecreaseStockAsync_ShouldReturnFalse_WhenQuantityIsInvalid(
            int quantity)
        {
            // Arrange
            var productId = Guid.NewGuid();

            await using var context = IntegrationTestDatabase.CreateContext();

            var repository = new ProductRepository(context);

            // Act
            var result = await repository.TryDecreaseStockAsync(
                productId,
                quantity,
                CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TryDecreaseStockAsync_ShouldPreventOverselling_WhenRequestsAreConcurrent()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de concorrência",
                50.00m,
                "BRL",
                5);

            Assert.True(productResult.IsSuccess);

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(productResult.Value);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Cada operação utiliza seu próprio DbContext.
                async Task<bool> DecreaseStockAsync()
                {
                    await using var context = IntegrationTestDatabase.CreateContext();

                    var repository = new ProductRepository(context);

                    return await repository.TryDecreaseStockAsync(
                        productId,
                        4,
                        CancellationToken.None);
                }

                // Act
                var results = await Task.WhenAll(
                    DecreaseStockAsync(),
                    DecreaseStockAsync());

                // Assert: somente uma operação pode consumir o estoque.
                Assert.Single(results, result => result);

                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == productId,
                        TestContext.Current.CancellationToken);

                    Assert.Equal(1, product.StockQuantity);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                await context.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task TryIncreaseStockAsync_ShouldIncreaseStock_WhenQuantityIsValid()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para devolução de estoque",
                50.00m,
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
                    var repository = new ProductRepository(context);

                    var result = await repository.TryIncreaseStockAsync(
                        productId,
                        4,
                        CancellationToken.None);

                    Assert.True(result);
                }

                // Assert: consulta novamente o estoque no PostgreSQL.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == productId,
                        TestContext.Current.CancellationToken);

                    Assert.Equal(14, product.StockQuantity);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                await context.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task TryIncreaseStockAsync_ShouldPreventOverflow_WhenStockIsNearIntMaxValue()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto próximo do limite de estoque",
                50.00m,
                "BRL",
                int.MaxValue - 1);

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
                    var repository = new ProductRepository(context);

                    var result = await repository.TryIncreaseStockAsync(
                        productId,
                        2,
                        CancellationToken.None);

                    Assert.False(result);
                }

                // Assert: o estoque não sofreu alteração.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == productId,
                        TestContext.Current.CancellationToken);

                    Assert.Equal(int.MaxValue - 1, product.StockQuantity);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                await context.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task TryIncreaseStockAsync_ShouldReturnFalse_WhenProductDoesNotExist()
        {
            // Arrange
            await using var context = IntegrationTestDatabase.CreateContext();

            var repository = new ProductRepository(context);

            // Act
            var result = await repository.TryIncreaseStockAsync(
                Guid.NewGuid(),
                1,
                CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task TryIncreaseStockAsync_ShouldReturnFalse_WhenQuantityIsInvalid(
            int quantity)
        {
            // Arrange
            await using var context = IntegrationTestDatabase.CreateContext();

            var repository = new ProductRepository(context);

            // Act
            var result = await repository.TryIncreaseStockAsync(
                Guid.NewGuid(),
                quantity,
                CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TryIncreaseStockAsync_ShouldAllowStockToReachIntMaxValue()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto no limite máximo de estoque",
                50.00m,
                "BRL",
                int.MaxValue - 1);

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
                    var repository = new ProductRepository(context);

                    var result = await repository.TryIncreaseStockAsync(
                        productId,
                        1,
                        CancellationToken.None);

                    Assert.True(result);
                }

                // Assert
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == productId,
                        TestContext.Current.CancellationToken);

                    Assert.Equal(int.MaxValue, product.StockQuantity);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                await context.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

    }
}
