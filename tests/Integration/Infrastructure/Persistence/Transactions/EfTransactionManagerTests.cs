using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderService.Infrastructure.Persistence.Transactions;

namespace OrderService.Infrastructure.IntegrationTests.Persistence.Transactions
{
    /// <summary>
    /// Testes de integração do gerenciador de transações.
    /// </summary>
    public sealed class EfTransactionManagerTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldCommitChanges_WhenOperationSucceeds()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de commit",
                25.00m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    // Act
                    var result = await transactionManager.ExecuteAsync(
                        async cancellationToken =>
                        {
                            await context.Products.AddAsync(
                                productResult.Value,
                                cancellationToken);

                            return Result.Success();
                        },
                        CancellationToken.None);

                    Assert.True(result.IsSuccess);
                }

                // Assert: verifica a persistência em outro DbContext.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            candidate => candidate.Id == productId,
                            TestContext.Current.CancellationToken);

                    Assert.NotNull(product);
                    Assert.Equal(10, product.StockQuantity);
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
        public async Task ExecuteAsync_ShouldRollbackChanges_WhenOperationReturnsFailure()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de rollback",
                25.00m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    // Act
                    var result = await transactionManager.ExecuteAsync(
                        async cancellationToken =>
                        {
                            await context.Products.AddAsync(
                                productResult.Value,
                                cancellationToken);

                            // Força o envio da inserção ao PostgreSQL
                            // antes de retornar a falha de negócio.
                            await context.SaveChangesAsync(cancellationToken);

                            return Result.Failure(
                                new Error(
                                    "Test.Rollback",
                                    "Falha de negócio simulada."));
                        },
                        CancellationToken.None);

                    Assert.False(result.IsSuccess);
                }

                // Assert: o produto não pode existir após o rollback.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var productExists = await context.Products
                        .AsNoTracking()
                        .AnyAsync(candidate => candidate.Id == productId, TestContext.Current.CancellationToken);

                    Assert.False(productExists);
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
        public async Task ExecuteAsync_ShouldRollbackAndRethrow_WhenOperationThrowsException()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de exceção",
                25.00m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            var expectedException = new InvalidOperationException(
                "Falha inesperada simulada.");

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    // Act
                    var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
                        () => transactionManager.ExecuteAsync(
                            async cancellationToken =>
                            {
                                await context.Products.AddAsync(
                                    productResult.Value,
                                    cancellationToken);

                                await context.SaveChangesAsync(cancellationToken);

                                throw expectedException;
                            },
                            CancellationToken.None));

                    // Assert: a exceção original foi preservada.
                    Assert.Same(expectedException, actualException);
                }

                // Assert: a inserção foi desfeita pelo rollback.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var productExists = await context.Products
                        .AsNoTracking()
                        .AnyAsync(candidate => candidate.Id == productId, TestContext.Current.CancellationToken);

                    Assert.False(productExists);
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
        public async Task ExecuteAsync_ShouldRollbackStockUpdate_WhenOperationReturnsFailure()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de atomicidade",
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
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    var productRepository = new ProductRepository(context);

                    var result = await transactionManager.ExecuteAsync(
                        async cancellationToken =>
                        {
                            var stockDecreased =
                                await productRepository.TryDecreaseStockAsync(
                                    productId,
                                    4,
                                    cancellationToken);

                            Assert.True(stockDecreased);

                            return Result.Failure(
                                new Error(
                                    "Test.StockRollback",
                                    "Falha de negócio simulada."));
                        },
                        CancellationToken.None);

                    Assert.False(result.IsSuccess);
                }

                // Assert: a atualização de estoque foi revertida.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == productId, TestContext.Current.CancellationToken);

                    Assert.Equal(10, product.StockQuantity);
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
        public async Task ExecuteAsync_ShouldRollbackTrackedAndDirectChanges_WhenOperationFails()
        {
            // Arrange
            var existingProductId = Guid.NewGuid();
            var newProductId = Guid.NewGuid();

            var existingProductResult = Product.Create(
                existingProductId,
                "Produto com estoque",
                50.00m,
                "BRL",
                10);

            var newProductResult = Product.Create(
                newProductId,
                "Produto inserido na transação",
                25.00m,
                "BRL",
                5);

            Assert.True(existingProductResult.IsSuccess);
            Assert.True(newProductResult.IsSuccess);

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(existingProductResult.Value);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Act
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    var productRepository = new ProductRepository(context);

                    var result = await transactionManager.ExecuteAsync(
                        async cancellationToken =>
                        {
                            var stockDecreased =
                                await productRepository.TryDecreaseStockAsync(
                                    existingProductId,
                                    4,
                                    cancellationToken);

                            Assert.True(stockDecreased);

                            await context.Products.AddAsync(
                                newProductResult.Value,
                                cancellationToken);

                            // Persiste a inserção ainda dentro da transação.
                            await context.SaveChangesAsync(cancellationToken);

                            return Result.Failure(
                                new Error(
                                    "Test.Atomicity",
                                    "Falha simulada após as alterações."));
                        },
                        CancellationToken.None);

                    Assert.False(result.IsSuccess);
                }

                // Assert: ambas as alterações devem ter sido revertidas.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var existingProduct = await context.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == existingProductId, TestContext.Current.CancellationToken);

                    Assert.Equal(10, existingProduct.StockQuantity);

                    var newProductExists = await context.Products
                        .AsNoTracking()
                        .AnyAsync(product => product.Id == newProductId, TestContext.Current.CancellationToken);

                    Assert.False(newProductExists);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                await context.Products
                    .Where(product =>
                        product.Id == existingProductId ||
                        product.Id == newProductId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task ExecuteAsyncGeneric_ShouldCommitAndReturnValue_WhenOperationSucceeds()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de transação genérica",
                35.00m,
                "BRL",
                12);

            Assert.True(productResult.IsSuccess);

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    // Act
                    var result = await transactionManager.ExecuteAsync<Guid>(
                        async cancellationToken =>
                        {
                            await context.Products.AddAsync(
                                productResult.Value,
                                cancellationToken);

                            return Result<Guid>.Success(productId);
                        },
                        CancellationToken.None);

                    // Assert: resultado genérico preservado.
                    Assert.True(result.IsSuccess);
                    Assert.Equal(productId, result.Value);
                }

                // Assert: alteração confirmada no PostgreSQL.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var persistedProduct = await context.Products
                        .AsNoTracking()
                        .SingleOrDefaultAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.NotNull(persistedProduct);
                    Assert.Equal(12, persistedProduct.StockQuantity);
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
        public async Task ExecuteAsyncGeneric_ShouldRollback_WhenOperationReturnsFailure()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para rollback genérico",
                35.00m,
                "BRL",
                12);

            Assert.True(productResult.IsSuccess);

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    // Act
                    var result = await transactionManager.ExecuteAsync<Guid>(
                        async cancellationToken =>
                        {
                            await context.Products.AddAsync(
                                productResult.Value,
                                cancellationToken);

                            // Garante que o INSERT seja executado
                            // dentro da transação antes do rollback.
                            await context.SaveChangesAsync(cancellationToken);

                            return Result<Guid>.Failure(
                                new Error(
                                    "Test.GenericRollback",
                                    "Falha de negócio simulada."));
                        },
                        CancellationToken.None);

                    Assert.False(result.IsSuccess);
                }

                // Assert: o produto não deve existir após o rollback.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var productExists = await context.Products
                        .AsNoTracking()
                        .AnyAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.False(productExists);
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
        public async Task ExecuteAsyncGeneric_ShouldRollbackAndRethrow_WhenOperationThrowsException()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de exceção genérica",
                35.00m,
                "BRL",
                12);

            Assert.True(productResult.IsSuccess);

            var expectedException = new InvalidOperationException(
                "Falha inesperada na transação genérica.");

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    // Act
                    var actualException =
                        await Assert.ThrowsAsync<InvalidOperationException>(
                            () => transactionManager.ExecuteAsync<Guid>(
                                async cancellationToken =>
                                {
                                    await context.Products.AddAsync(
                                        productResult.Value,
                                        cancellationToken);

                                    // Executa o INSERT dentro da transação.
                                    await context.SaveChangesAsync(cancellationToken);

                                    throw expectedException;
                                },
                                CancellationToken.None));

                    // Assert: preserva a mesma instância da exceção.
                    Assert.Same(expectedException, actualException);
                }

                // Assert: o INSERT foi revertido.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var productExists = await context.Products
                        .AsNoTracking()
                        .AnyAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.False(productExists);
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
        public async Task ExecuteAsync_ShouldRollback_WhenSaveChangesFails()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var originalProductResult = Product.Create(
                productId,
                "Produto original",
                25.00m,
                "BRL",
                10);

            var duplicateProductResult = Product.Create(
                productId,
                "Produto com identificador duplicado",
                50.00m,
                "BRL",
                20);

            Assert.True(originalProductResult.IsSuccess);
            Assert.True(duplicateProductResult.IsSuccess);

            try
            {
                // Persiste o produto original.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(originalProductResult.Value);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Act
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    await Assert.ThrowsAsync<DbUpdateException>(
                        () => transactionManager.ExecuteAsync(
                            async cancellationToken =>
                            {
                                await context.Products.AddAsync(
                                    duplicateProductResult.Value,
                                    cancellationToken);

                                return Result.Success();
                            },
                            CancellationToken.None));
                }

                // Assert: o produto original permanece inalterado.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var persistedProduct = await context.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.Equal("Produto original", persistedProduct.Name);
                    Assert.Equal(25.00m, persistedProduct.Price);
                    Assert.Equal(10, persistedProduct.StockQuantity);
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
        public async Task ExecuteAsync_ShouldRollbackPreviousStockUpdate_WhenSaveChangesFails()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var originalProductResult = Product.Create(
                productId,
                "Produto original para teste de rollback",
                50.00m,
                "BRL",
                10);

            var duplicateProductResult = Product.Create(
                productId,
                "Produto com identificador duplicado",
                25.00m,
                "BRL",
                20);

            Assert.True(originalProductResult.IsSuccess);
            Assert.True(duplicateProductResult.IsSuccess);

            try
            {
                // Persiste o produto original.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(originalProductResult.Value);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Act
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    var productRepository = new ProductRepository(context);

                    await Assert.ThrowsAsync<DbUpdateException>(
                        () => transactionManager.ExecuteAsync(
                            async cancellationToken =>
                            {
                                // Primeira alteração: redução válida de estoque.
                                var stockDecreased =
                                    await productRepository.TryDecreaseStockAsync(
                                        productId,
                                        4,
                                        cancellationToken);

                                Assert.True(stockDecreased);

                                // Segunda alteração: inserção que viola a PK.
                                await context.Products.AddAsync(
                                    duplicateProductResult.Value,
                                    cancellationToken);

                                return Result.Success();
                            },
                            CancellationToken.None));
                }

                // Assert: a redução anterior também foi revertida.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var persistedProduct = await context.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.Equal("Produto original para teste de rollback",
                        persistedProduct.Name);

                    Assert.Equal(10, persistedProduct.StockQuantity);
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
        public async Task ExecuteAsync_ShouldRollback_WhenOperationIsCanceled()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de cancelamento",
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

                using var cancellationTokenSource = new CancellationTokenSource();

                // Act
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var transactionManager = new EfTransactionManager(
                        context,
                        NullLogger<EfTransactionManager>.Instance);

                    var productRepository = new ProductRepository(context);

                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        () => transactionManager.ExecuteAsync(
                            async cancellationToken =>
                            {
                                var stockDecreased =
                                    await productRepository.TryDecreaseStockAsync(
                                        productId,
                                        4,
                                        cancellationToken);

                                Assert.True(stockDecreased);

                                // Simula o cancelamento após a alteração do estoque.
                                cancellationTokenSource.Cancel();

                                cancellationToken.ThrowIfCancellationRequested();

                                return Result.Success();
                            },
                            cancellationTokenSource.Token));
                }

                // Assert
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var persistedProduct = await context.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.Equal(10, persistedProduct.StockQuantity);
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
