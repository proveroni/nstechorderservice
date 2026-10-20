using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Orders.Operations.ConfirmOrder.Commands;
using OrderService.Application.Orders.Operations.ConfirmOrder.Handlers;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.IntegrationTests.Persistence;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderService.Infrastructure.Persistence.Transactions;

namespace OrderService.Infrastructure.IntegrationTests.Orders.ConfirmOrder
{
    /// <summary>
    /// Testes de integração do caso de uso de confirmação de pedidos,
    /// utilizando PostgreSQL e implementações reais da infraestrutura.
    /// </summary>
    public sealed class ConfirmOrderIntegrationTests
    {

        private const string CreatedBy = "user-creator";
        private const string ConfirmedBy = "user-confirmer";

        [Fact]
        public async Task ConfirmOrder_ShouldRollbackStock_WhenSecondProductHasInsufficientStock()
        {
            // Arrange
            var firstProductId = Guid.NewGuid();
            var secondProductId = Guid.NewGuid();

            var firstProductResult = Product.Create(
                firstProductId,
                "Produto A",
                25m,
                "BRL",
                10);

            var secondProductResult = Product.Create(
                secondProductId,
                "Produto B",
                40m,
                "BRL",
                2);

            Assert.True(firstProductResult.IsSuccess);
            Assert.True(secondProductResult.IsSuccess);

            var firstItemResult = OrderItem.Create(firstProductId, 3, 25m);
            var secondItemResult = OrderItem.Create(secondProductId, 5, 40m);

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

                // Act
                await using (var executionContext = IntegrationTestDatabase.CreateContext())
                {
                    var orderRepository = new OrderRepository(executionContext);
                    var productRepository = new ProductRepository(executionContext);

                    var transactionManager = new EfTransactionManager(
                        executionContext,
                        NullLogger<EfTransactionManager>.Instance);

                    var handler = new ConfirmOrderHandler(
                        orderRepository,
                        productRepository,
                        transactionManager,
                        TimeProvider.System,
                        CreateAuthenticatedUser());

                    var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

                    Assert.False(result.IsSuccess);
                }

                // Assert
                // Utilizamos outro contexto para consultar o estado persistido.
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

                    Assert.Equal(10, firstProduct.StockQuantity);
                    Assert.Equal(2, secondProduct.StockQuantity);

                    Assert.Equal(OrderStatusEnum.Placed, persistedOrder.Status);

                    Assert.Equal("BRL", persistedOrder.Currency);
                    Assert.Null(persistedOrder.ConfirmedAt);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                    Assert.Null(persistedOrder.ConfirmedBy);
                    Assert.Null(persistedOrder.UpdatedAt);
                    Assert.Null(persistedOrder.UpdatedBy);
                }
            }
            finally
            {
                // Limpeza dos registros criados exclusivamente pelo teste.
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                // O banco possui FK de order_items para orders com exclusão em cascata.
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
        public async Task ConfirmOrder_ShouldDecreaseStockOnlyOnce_WhenTwoConfirmationsRunConcurrently()
        {
            // Arrange
            // Prepararemos um produto com estoque de 10 unidades
            // e um pedido Placed solicitando 3 unidades.
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de concorrência",
                25m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            var itemResult = OrderItem.Create(
                productId,
                3,
                25m);

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
                await using var setupContext = IntegrationTestDatabase.CreateContext();

                await setupContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
                await setupContext.Orders.AddAsync(order, TestContext.Current.CancellationToken);

                await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);


                // Act
                // Executaremos duas confirmações simultâneas,
                // cada uma utilizando seu próprio DbContext.
                async Task<Result> ConfirmAsync()
                {
                    await using var executionContext = IntegrationTestDatabase.CreateContext();

                    var orderRepository = new OrderRepository(executionContext);
                    var productRepository = new ProductRepository(executionContext);

                    var transactionManager = new EfTransactionManager(
                        executionContext,
                        NullLogger<EfTransactionManager>.Instance);

                    var handler = new ConfirmOrderHandler(
                        orderRepository,
                        productRepository,
                        transactionManager,
                        TimeProvider.System,
                        CreateAuthenticatedUser());

                    return await handler.HandleAsync(
                        new ConfirmOrderCommand(order.Id));
                }

                var firstConfirmation = ConfirmAsync();
                var secondConfirmation = ConfirmAsync();

                var results = await Task.WhenAll(
                    firstConfirmation,
                    secondConfirmation);

                // Assert
                // As duas operações devem retornar sucesso.
                // O pedido deve terminar Confirmed.
                // O estoque deve terminar em 7, e não em 4.
                Assert.All(results, result => Assert.True(result.IsSuccess));

                await using var verificationContext = IntegrationTestDatabase.CreateContext();

                var persistedProduct = await verificationContext.Products
                    .AsNoTracking()
                    .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                var persistedOrder = await verificationContext.Orders
                    .AsNoTracking()
                    .SingleAsync(persisted => persisted.Id == order.Id, TestContext.Current.CancellationToken);

                Assert.Equal(7, persistedProduct.StockQuantity);
                Assert.Equal(OrderStatusEnum.Confirmed, persistedOrder.Status);
                Assert.Equal("BRL", persistedOrder.Currency);
                Assert.NotNull(persistedOrder.ConfirmedAt);
                Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                Assert.Equal(ConfirmedBy, persistedOrder.ConfirmedBy);
                Assert.Equal(persistedOrder.ConfirmedAt, persistedOrder.UpdatedAt);
                Assert.Equal(ConfirmedBy, persistedOrder.UpdatedBy);
                Assert.Null(persistedOrder.CanceledBy);

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
        public async Task ConfirmOrder_ShouldWaitForOrderLock_BeforeConfirming()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var productResult = Product.Create(
                productId,
                "Produto para teste de bloqueio",
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
                // Persiste os dados iniciais.
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
                    await setupContext.Orders.AddAsync(order, TestContext.Current.CancellationToken);
                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                await using var lockingContext = IntegrationTestDatabase.CreateContext();

                await using var lockingTransaction =
                    await lockingContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

                var lockReleased = false;

                async Task ReleaseLockAsync()
                {
                    if (lockReleased)
                    {
                        return;
                    }

                    await lockingTransaction.RollbackAsync(CancellationToken.None);
                    lockReleased = true;
                }

                var lockingRepository = new OrderRepository(lockingContext);

                var lockedOrder = await lockingRepository.GetByIdForUpdateAsync(order.Id, TestContext.Current.CancellationToken);

                Assert.NotNull(lockedOrder);
                Assert.Equal(OrderStatusEnum.Placed, lockedOrder.Status);

                var lockingConnectionId = await lockingContext.Database
                    .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"")
                    .SingleAsync(TestContext.Current.CancellationToken);

                Assert.True(lockingConnectionId > 0);

                // Cada confirmação utiliza seu próprio DbContext e sua própria conexão.
                async Task<Result> ConfirmAsync(
                    TaskCompletionSource<int> connectionIdSource,
                    CancellationToken cancellationToken)
                {
                    await using var executionContext = IntegrationTestDatabase.CreateContext();

                    try
                    {
                        await executionContext.Database.OpenConnectionAsync(cancellationToken);

                        var connectionId = await executionContext.Database
                            .SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"")
                            .SingleAsync(cancellationToken);

                        connectionIdSource.TrySetResult(connectionId);

                        var orderRepository = new OrderRepository(executionContext);
                        var productRepository = new ProductRepository(executionContext);

                        var transactionManager = new EfTransactionManager(
                            executionContext,
                            NullLogger<EfTransactionManager>.Instance);

                        var handler = new ConfirmOrderHandler(
                            orderRepository,
                            productRepository,
                            transactionManager,
                            TimeProvider.System,
                            CreateAuthenticatedUser());

                        return await handler.HandleAsync(
                            new ConfirmOrderCommand(order.Id),
                            cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        connectionIdSource.TrySetException(exception);
                        throw;
                    }
                }

                using var confirmationCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                var connectionIdSource = new TaskCompletionSource<int>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                Task<Result>? confirmationTask = null;

                try
                {
                    // Act
                    confirmationTask = ConfirmAsync(
                        connectionIdSource,
                        confirmationCancellation.Token);

                    var confirmationConnectionId = await connectionIdSource.Task
                        .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                    Assert.True(confirmationConnectionId > 0);
                    Assert.NotEqual(lockingConnectionId, confirmationConnectionId);

                    // Consulta uma terceira conexão para observar o bloqueio.
                    await using var monitoringContext =
                        IntegrationTestDatabase.CreateContext();

                    var blockingSessionDetected = false;

                    using var monitoringTimeout = new CancellationTokenSource(
                        TimeSpan.FromSeconds(10));

                    while (!monitoringTimeout.IsCancellationRequested)
                    {
                        var blockingSessions = await monitoringContext.Database
                            .SqlQueryRaw<int>(
                                """
                                SELECT unnest(pg_blocking_pids({0})) AS "Value"
                                """,
                                confirmationConnectionId)
                            .ToListAsync(TestContext.Current.CancellationToken);

                        if (blockingSessions.Contains(lockingConnectionId))
                        {
                            blockingSessionDetected = true;
                            break;
                        }

                        if (confirmationTask.IsCompleted)
                        {
                            break;
                        }

                        try
                        {
                            await Task.Delay(
                                TimeSpan.FromMilliseconds(50),
                                monitoringTimeout.Token);
                        }
                        catch (OperationCanceledException)
                            when (monitoringTimeout.IsCancellationRequested)
                        {
                            break;
                        }
                    }

                    // Assert
                    Assert.True(
                        blockingSessionDetected,
                        "O PostgreSQL não identificou a confirmação aguardando o bloqueio do pedido.");

                    Assert.False(
                        confirmationTask.IsCompleted,
                        "A confirmação terminou enquanto o bloqueio estava ativo.");

                    // Libera o bloqueio para a confirmação prosseguir.
                    await ReleaseLockAsync();

                    var result = await confirmationTask.WaitAsync(
                        TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);
                }
                finally
                {
                    // Garante a liberação do bloqueio em caso de falha.
                    await ReleaseLockAsync();

                    if (confirmationTask is not null)
                    {
                        if (!confirmationTask.IsCompleted)
                        {
                            confirmationCancellation.Cancel();
                        }

                        try
                        {
                            await confirmationTask.WaitAsync(
                                TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                        }
                        catch (OperationCanceledException)
                            when (confirmationCancellation.IsCancellationRequested)
                        {
                            // A confirmação reconheceu o cancelamento.
                        }
                    }
                }

                // Verifica o estado persistido usando outro DbContext.
                await using var verificationContext =
                    IntegrationTestDatabase.CreateContext();

                var persistedProduct = await verificationContext.Products
                    .AsNoTracking()
                    .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                var persistedOrder = await verificationContext.Orders
                    .AsNoTracking()
                    .SingleAsync(persisted => persisted.Id == order.Id, TestContext.Current.CancellationToken);

                Assert.Equal(7, persistedProduct.StockQuantity);
                Assert.Equal(OrderStatusEnum.Confirmed, persistedOrder.Status);
                Assert.Equal("BRL", persistedOrder.Currency);
                Assert.NotNull(persistedOrder.ConfirmedAt);
                Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                Assert.Equal(ConfirmedBy, persistedOrder.ConfirmedBy);
                Assert.Equal(persistedOrder.ConfirmedAt, persistedOrder.UpdatedAt);
                Assert.Equal(ConfirmedBy, persistedOrder.UpdatedBy);
            }
            finally
            {
                await using var cleanupContext =
                    IntegrationTestDatabase.CreateContext();

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
        /// Cria uma simulação de usuário autenticado para os testes
        /// de integração de confirmação de pedidos.
        /// </summary>
        private static ICurrentUser CreateAuthenticatedUser()
        {
            var currentUserMock = new Mock<ICurrentUser>();

            currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(true);

            currentUserMock
                .Setup(user => user.UserId)
                .Returns(ConfirmedBy);

            return currentUserMock.Object;
        }

        #endregion [ Métodos auxiliares ]
    }
}
