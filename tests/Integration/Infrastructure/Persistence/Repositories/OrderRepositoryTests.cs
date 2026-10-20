using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence.Repositories;

namespace OrderService.Infrastructure.IntegrationTests.Persistence.Repositories
{
    /// <summary>
    /// Testes de integração do repositório de pedidos
    /// utilizando PostgreSQL real.
    /// </summary>
    public sealed class OrderRepositoryTests
    {
        private const string CreatedBy = "test-user-001";

        [Fact]
        public async Task GetByIdAsync_ShouldReturnOrderWithItems()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto do teste de repositório",
                50.00m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            var itemResult = OrderItem.Create(
                productId,
                2,
                50.00m);

            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(productResult.Value);
                    context.Orders.Add(order);

                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Act
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var repository = new OrderRepository(context);

                    var persistedOrder = await repository.GetByIdAsync(
                        order.Id,
                        CancellationToken.None);

                    // Assert
                    Assert.NotNull(persistedOrder);
                    Assert.Equal(order.Id, persistedOrder.Id);
                    Assert.Equal(customerId, persistedOrder.CustomerId);
                    Assert.Equal("BRL", persistedOrder.Currency);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);

                    var persistedItem = Assert.Single(persistedOrder.Items);

                    Assert.Equal(productId, persistedItem.ProductId);
                    Assert.Equal(2, persistedItem.Quantity);
                    Assert.Equal(50.00m, persistedItem.UnitPrice);

                    // Verifica que o pedido está sendo rastreado.
                    Assert.Equal(
                        EntityState.Unchanged,
                        context.Entry(persistedOrder).State);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                var persistedOrder = await context.Orders
                    .SingleOrDefaultAsync(candidate => candidate.Id == order.Id, TestContext.Current.CancellationToken);

                if (persistedOrder is not null)
                {
                    context.Orders.Remove(persistedOrder);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                var persistedProduct = await context.Products
                    .SingleOrDefaultAsync(candidate => candidate.Id == productId, TestContext.Current.CancellationToken);

                if (persistedProduct is not null)
                {
                    context.Products.Remove(persistedProduct);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }
        }

        [Fact]
        public async Task AddAsync_ShouldTrackOrderWithoutSavingAutomatically()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var itemResult = OrderItem.Create(
                productId,
                2,
                50.00m);

            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            // Act
            await using (var context = IntegrationTestDatabase.CreateContext())
            {
                var repository = new OrderRepository(context);

                await repository.AddAsync(order, CancellationToken.None);

                // Assert: o pedido foi adicionado ao Change Tracker.
                Assert.Equal(
                    EntityState.Added,
                    context.Entry(order).State);

                // O pedido ainda não foi gravado no banco.
                // Não chamamos SaveChangesAsync().
            }

            // Um novo contexto consulta diretamente o PostgreSQL.
            await using (var context = IntegrationTestDatabase.CreateContext())
            {
                var persistedOrder = await context.Orders
                    .AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.Id == order.Id, TestContext.Current.CancellationToken);

                Assert.Null(persistedOrder);
            }
        }

        [Fact]
        public async Task GetByIdForUpdateAsync_ShouldWaitUntilFirstTransactionReleasesLock()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de bloqueio",
                50.00m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            var itemResult = OrderItem.Create(
                productId,
                2,
                50.00m);

            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            try
            {
                // Persiste os dados necessários ao teste.
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(productResult.Value);
                    context.Orders.Add(order);

                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                await using var contextA = IntegrationTestDatabase.CreateContext();
                await using var contextB = IntegrationTestDatabase.CreateContext();

                await using var transactionA =
                    await contextA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

                await using var transactionB =
                    await contextB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

                var repositoryA = new OrderRepository(contextA);
                var repositoryB = new OrderRepository(contextB);

                // A primeira transação adquire o bloqueio.
                var orderA = await repositoryA.GetByIdForUpdateAsync(order.Id, TestContext.Current.CancellationToken);

                Assert.NotNull(orderA);
                Assert.Single(orderA.Items);

                var secondQueryStarted = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                // A segunda transação tenta adquirir o mesmo bloqueio.
                var secondQueryTask = Task.Run(async () =>
                {
                    secondQueryStarted.SetResult();

                    return await repositoryB.GetByIdForUpdateAsync(order.Id);
                });

                await secondQueryStarted.Task;

                // A segunda consulta não deve terminar enquanto A mantém o bloqueio.
                var completedTask = await Task.WhenAny(
                    secondQueryTask,
                    Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken));

                Assert.NotSame(secondQueryTask, completedTask);

                // Libera o bloqueio da primeira transação.
                await transactionA.CommitAsync(TestContext.Current.CancellationToken);

                // A segunda transação agora deve conseguir prosseguir.
                var orderB = await secondQueryTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                Assert.NotNull(orderB);
                Assert.Equal(order.Id, orderB.Id);
                Assert.Single(orderB.Items);

                await transactionB.CommitAsync(TestContext.Current.CancellationToken);
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                var persistedOrder = await context.Orders
                    .SingleOrDefaultAsync(candidate => candidate.Id == order.Id, TestContext.Current.CancellationToken);

                if (persistedOrder is not null)
                {
                    context.Orders.Remove(persistedOrder);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                var persistedProduct = await context.Products
                    .SingleOrDefaultAsync(candidate => candidate.Id == productId, TestContext.Current.CancellationToken);

                if (persistedProduct is not null)
                {
                    context.Products.Remove(persistedProduct);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }
        }

    }
}
