using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Orders.Operations.CancelOrder.Commands;
using OrderService.Application.Orders.Operations.CancelOrder.Handlers;
using OrderService.Application.Orders.Operations.ConfirmOrder.Commands;
using OrderService.Application.Orders.Operations.ConfirmOrder.Handlers;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.IntegrationTests.Persistence;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderService.Infrastructure.Persistence.Transactions;

namespace OrderService.Infrastructure.IntegrationTests.Orders.CancelOrder
{
    /// <summary>
    /// Testes de integração do cancelamento de pedidos com PostgreSQL.
    /// </summary>
    public sealed class CancelOrderIntegrationTests
    {
        private const string CreatedBy = "user-creator";
        private const string ConfirmedBy = "user-confirmer";
        private const string CanceledBy = "user-canceler";

        [Fact]
        public async Task CancelOrder_ShouldRestoreStock_WhenOrderWasConfirmed()
        {
            // Arrange

            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de cancelamento",
                25m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            var itemResult = OrderItem.Create(productId, 3, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            try
            {
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
                    await setupContext.Orders.AddAsync(order, TestContext.Current.CancellationToken);
                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Confirma o pedido.
                await using (var confirmationContext = IntegrationTestDatabase.CreateContext())
                {
                    var orderRepository = new OrderRepository(confirmationContext);
                    var productRepository = new ProductRepository(confirmationContext);

                    var transactionManager = new EfTransactionManager(
                        confirmationContext,
                        NullLogger<EfTransactionManager>.Instance);

                    var handler = new ConfirmOrderHandler(
                        orderRepository,
                        productRepository,
                        transactionManager,
                        TimeProvider.System,
                        CreateAuthenticatedUser(ConfirmedBy));

                    var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }

                // Confirma que o estoque foi reduzido.
                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var product = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.Equal(7, product.StockQuantity);
                }

                // Cancela o pedido.
                await using (var cancellationContext = IntegrationTestDatabase.CreateContext())
                {
                    var orderRepository = new OrderRepository(cancellationContext);
                    var productRepository = new ProductRepository(cancellationContext);

                    var transactionManager = new EfTransactionManager(
                        cancellationContext,
                        NullLogger<EfTransactionManager>.Instance);

                    var handler = new CancelOrderHandler(
                        orderRepository,
                        productRepository,
                        transactionManager,
                        TimeProvider.System,
                        CreateAuthenticatedUser(CanceledBy));

                    var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }

                // Verifica os dados efetivamente persistidos.
                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var persistedProduct = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    var persistedOrder = await verificationContext.Orders
                        .AsNoTracking()
                        .SingleAsync(persisted => persisted.Id == order.Id, TestContext.Current.CancellationToken);

                    Assert.Equal(10, persistedProduct.StockQuantity);
                    Assert.Equal(OrderStatusEnum.Canceled, persistedOrder.Status);
                    Assert.Equal("BRL", persistedOrder.Currency);
                    Assert.NotNull(persistedOrder.CanceledAt);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                    Assert.Equal(ConfirmedBy, persistedOrder.ConfirmedBy);
                    Assert.Equal(CanceledBy, persistedOrder.CanceledBy);
                    Assert.NotNull(persistedOrder.ConfirmedAt);
                    Assert.Equal(persistedOrder.CanceledAt, persistedOrder.UpdatedAt);
                    Assert.Equal(CanceledBy, persistedOrder.UpdatedBy);
                }
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                await cleanupContext.Orders
                    .Where(persisted => persisted.Id == order.Id)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

                await cleanupContext.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task CancelOrder_ShouldRollbackStock_WhenSecondProductRestorationFails()
        {
            var firstProductId = Guid.NewGuid();
            var secondProductId = Guid.NewGuid();

            var firstProductResult = Product.Create(
                firstProductId, "Produto A - rollback", 25m, "BRL", 10);

            var secondProductResult = Product.Create(
                secondProductId, "Produto B - rollback", 15m, "BRL", int.MaxValue);

            Assert.True(firstProductResult.IsSuccess);
            Assert.True(secondProductResult.IsSuccess);

            var firstItemResult = OrderItem.Create(firstProductId, 3, 25m);
            var secondItemResult = OrderItem.Create(secondProductId, 1, 15m);

            Assert.True(firstItemResult.IsSuccess);
            Assert.True(secondItemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [firstItemResult.Value, secondItemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            try
            {
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddRangeAsync(
                        firstProductResult.Value,
                        secondProductResult.Value);

                    await setupContext.Orders.AddAsync(order, TestContext.Current.CancellationToken);
                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Confirma o pedido e desconta o estoque dos dois produtos.
                await using (var confirmationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new ConfirmOrderHandler(
                        new OrderRepository(confirmationContext),
                        new ProductRepository(confirmationContext),
                        new EfTransactionManager(
                            confirmationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser(ConfirmedBy));

                    var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }

                // Provoca uma condição de falha na reposição do segundo produto.
                await using (var adjustmentContext = IntegrationTestDatabase.CreateContext())
                {
                    var affectedRows = await adjustmentContext.Products
                        .Where(product => product.Id == secondProductId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                product => product.StockQuantity,
                                int.MaxValue),
                            TestContext.Current.CancellationToken);

                    Assert.Equal(1, affectedRows);
                }

                // Tenta cancelar o pedido.
                await using (var cancellationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new CancelOrderHandler(
                        new OrderRepository(cancellationContext),
                        new ProductRepository(cancellationContext),
                        new EfTransactionManager(
                            cancellationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser(CanceledBy));

                    var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.False(result.IsSuccess);

                    Assert.Contains(
                        result.Errors,
                        error => error.Code == "product.stock_restoration_failed");
                }

                // Verifica que a primeira reposição também foi desfeita.
                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var firstProduct = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == firstProductId, TestContext.Current.CancellationToken);

                    var secondProduct = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == secondProductId, TestContext.Current.CancellationToken);

                    var persistedOrder = await verificationContext.Orders
                        .AsNoTracking()
                        .SingleAsync(persisted => persisted.Id == order.Id, TestContext.Current.CancellationToken);

                    Assert.Equal(7, firstProduct.StockQuantity);
                    Assert.Equal(int.MaxValue, secondProduct.StockQuantity);
                    Assert.Equal(OrderStatusEnum.Confirmed, persistedOrder.Status);
                    Assert.Equal("BRL", persistedOrder.Currency);
                    Assert.Null(persistedOrder.CanceledAt);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                    Assert.Equal(ConfirmedBy, persistedOrder.ConfirmedBy);
                    Assert.Null(persistedOrder.CanceledBy);
                    Assert.NotNull(persistedOrder.ConfirmedAt);
                    Assert.Equal(persistedOrder.ConfirmedAt, persistedOrder.UpdatedAt);
                    Assert.Equal(ConfirmedBy, persistedOrder.UpdatedBy);
                }
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                await cleanupContext.Orders
                    .Where(persisted => persisted.Id == order.Id)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

                await cleanupContext.Products
                    .Where(product =>
                        product.Id == firstProductId ||
                        product.Id == secondProductId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task CancelOrder_ShouldRestoreStockOnlyOnce_WhenCanceledTwice()
        {
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId, "Produto para teste de idempotência", 25m, "BRL", 10);

            Assert.True(productResult.IsSuccess);

            var itemResult = OrderItem.Create(productId, 3, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            try
            {
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
                    await setupContext.Orders.AddAsync(order, TestContext.Current.CancellationToken);
                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Confirma o pedido, reduzindo o estoque para 7.
                await using (var confirmationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new ConfirmOrderHandler(
                        new OrderRepository(confirmationContext),
                        new ProductRepository(confirmationContext),
                        new EfTransactionManager(
                            confirmationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser(ConfirmedBy));

                    var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }

                DateTimeOffset? originalCanceledAt;
                string? originalCanceledBy = null;
                DateTimeOffset? originalUpdatedAt = null;
                string? originalUpdatedBy = null;

                // Primeiro cancelamento: devolve o estoque.
                await using (var firstCancellationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new CancelOrderHandler(
                        new OrderRepository(firstCancellationContext),
                        new ProductRepository(firstCancellationContext),
                        new EfTransactionManager(
                            firstCancellationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser(CanceledBy));

                    var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }

                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var persistedOrder = await verificationContext.Orders
                        .AsNoTracking()
                        .SingleAsync(persisted => persisted.Id == order.Id, TestContext.Current.CancellationToken);

                    originalCanceledAt = persistedOrder.CanceledAt;
                    originalCanceledBy = persistedOrder.CanceledBy;
                    originalUpdatedAt = persistedOrder.UpdatedAt;
                    originalUpdatedBy = persistedOrder.UpdatedBy;

                    Assert.Equal(CanceledBy, originalCanceledBy);
                    Assert.Equal(originalCanceledAt, originalUpdatedAt);
                    Assert.Equal(CanceledBy, originalUpdatedBy);

                    var product = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(persisted => persisted.Id == productId, TestContext.Current.CancellationToken);

                    Assert.Equal(10, product.StockQuantity);
                    Assert.NotNull(originalCanceledAt);
                }

                // Segundo cancelamento: deve retornar sucesso sem alterar estoque.
                await using (var secondCancellationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new CancelOrderHandler(
                        new OrderRepository(secondCancellationContext),
                        new ProductRepository(secondCancellationContext),
                        new EfTransactionManager(
                            secondCancellationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser(CanceledBy));

                    var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }

                // Verifica os dados persistidos após os dois cancelamentos.
                await using (var finalContext = IntegrationTestDatabase.CreateContext())
                {
                    var persistedProduct = await finalContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    var persistedOrder = await finalContext.Orders
                        .AsNoTracking()
                        .SingleAsync(persisted => persisted.Id == order.Id, TestContext.Current.CancellationToken);

                    Assert.Equal(10, persistedProduct.StockQuantity);
                    Assert.Equal(OrderStatusEnum.Canceled, persistedOrder.Status);
                    Assert.Equal("BRL", persistedOrder.Currency);
                    Assert.Equal(originalCanceledAt, persistedOrder.CanceledAt);
                    Assert.Equal(originalCanceledBy, persistedOrder.CanceledBy);
                    Assert.Equal(originalUpdatedAt, persistedOrder.UpdatedAt);
                    Assert.Equal(originalUpdatedBy, persistedOrder.UpdatedBy);
                }
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                await cleanupContext.Orders
                    .Where(persisted => persisted.Id == order.Id)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

                await cleanupContext.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task CancelOrder_ShouldRestoreStockOnlyOnce_WhenTwoCancellationsRunConcurrently()
        {
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId, "Produto para cancelamento concorrente", 25m, "BRL", 10);

            Assert.True(productResult.IsSuccess);

            var itemResult = OrderItem.Create(productId, 3, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            try
            {
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
                    await setupContext.Orders.AddAsync(order, TestContext.Current.CancellationToken);
                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Confirma o pedido antes das tentativas de cancelamento.
                await using (var confirmationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new ConfirmOrderHandler(
                        new OrderRepository(confirmationContext),
                        new ProductRepository(confirmationContext),
                        new EfTransactionManager(
                            confirmationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser(ConfirmedBy));

                    var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }

                async Task<OrderService.CrossCutting.Results.Result> CancelAsync()
                {
                    await using var context = IntegrationTestDatabase.CreateContext();

                    var handler = new CancelOrderHandler(
                        new OrderRepository(context),
                        new ProductRepository(context),
                        new EfTransactionManager(
                            context,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser(CanceledBy));

                    return await handler.HandleAsync(
                        new CancelOrderCommand(order.Id));
                }

                // Executa duas solicitações independentes de cancelamento.
                var results = await Task.WhenAll(
                    CancelAsync(),
                    CancelAsync());

                Assert.All(results, result => Assert.True(result.IsSuccess));

                // Verifica o estado final diretamente no PostgreSQL.
                await using var verificationContext = IntegrationTestDatabase.CreateContext();

                var persistedProduct = await verificationContext.Products
                    .AsNoTracking()
                    .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                var persistedOrder = await verificationContext.Orders
                    .AsNoTracking()
                    .SingleAsync(persisted => persisted.Id == order.Id, TestContext.Current.CancellationToken);

                Assert.Equal(10, persistedProduct.StockQuantity);
                Assert.Equal(OrderStatusEnum.Canceled, persistedOrder.Status);
                Assert.Equal("BRL", persistedOrder.Currency);
                Assert.NotNull(persistedOrder.CanceledAt);
                Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                Assert.Equal(ConfirmedBy, persistedOrder.ConfirmedBy);
                Assert.Equal(CanceledBy, persistedOrder.CanceledBy);
                Assert.Equal(persistedOrder.CanceledAt, persistedOrder.UpdatedAt);
                Assert.Equal(CanceledBy, persistedOrder.UpdatedBy);
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                await cleanupContext.Orders
                    .Where(persisted => persisted.Id == order.Id)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

                await cleanupContext.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        #region [ Métodos auxiliares ]

        /// <summary>
        /// Cria uma simulação de usuário autenticado com
        /// o identificador informado.
        /// </summary>
        private static ICurrentUser CreateAuthenticatedUser(string userId)
        {
            var currentUserMock = new Mock<ICurrentUser>();

            currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(true);

            currentUserMock
                .Setup(user => user.UserId)
                .Returns(userId);

            return currentUserMock.Object;
        }

        #endregion [ Métodos auxiliares ]

    }
}
