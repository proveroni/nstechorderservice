using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Orders.Operations.CreateOrder.Commands;
using OrderService.Application.Orders.Operations.CreateOrder.DTOs;
using OrderService.Application.Orders.Operations.CreateOrder.Handlers;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.IntegrationTests.Persistence;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderService.Infrastructure.Persistence.Transactions;

namespace OrderService.Infrastructure.IntegrationTests.Orders.CreateOrder
{
    /// <summary>
    /// Testes de integração da criação de pedidos com PostgreSQL.
    /// </summary>
    public sealed class CreateOrderIntegrationTests
    {
        private const string CreatedBy = "test-user-001";

        /// <summary>
        /// Cria um usuário autenticado para os testes de integração
        /// do caso de uso de criação de pedidos.
        /// </summary>
        private static ICurrentUser CreateAuthenticatedUser()
        {
            var currentUserMock = new Mock<ICurrentUser>();

            currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(true);

            currentUserMock
                .Setup(user => user.UserId)
                .Returns(CreatedBy);

            return currentUserMock.Object;
        }

        [Fact]
        public async Task CreateOrder_ShouldPersistOrderAndItems_WithoutDecreasingStock()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            const decimal productPrice = 25.50m;
            const int initialStock = 10;
            const int requestedQuantity = 3;

            var productResult = Product.Create(
                productId,
                "Produto para teste de criação",
                productPrice,
                "BRL", initialStock);

            Assert.True(productResult.IsSuccess);

            Guid? createdOrderId = null;

            try
            {
                // Persiste o produto utilizado pelo pedido.
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Executa o caso de uso utilizando os repositórios reais.
                await using (var creationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new CreateOrderHandler(
                        new OrderRepository(creationContext),
                        new ProductRepository(creationContext),
                        new EfTransactionManager(
                            creationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser());

                    var command = new CreateOrderCommand(
                        customerId,
                        "BRL",
                            [
                            new CreateOrderItemData(
                            productId,
                            requestedQuantity)
                        ]);

                    var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);

                    createdOrderId = result.Value.Id;

                    Assert.NotEqual(Guid.Empty, result.Value.Id);
                    Assert.Equal(customerId, result.Value.CustomerId);
                    Assert.Equal("BRL", result.Value.Currency);
                    Assert.Equal(OrderStatusEnum.Placed, result.Value.Status);
                    Assert.Equal(76.50m, result.Value.Total);
                }

                // Consulta os dados com outro DbContext para validar a persistência.
                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var persistedOrder = await verificationContext.Orders
                        .AsNoTracking()
                        .Include(order => order.Items)
                        .SingleAsync(order => order.Id == createdOrderId, TestContext.Current.CancellationToken);

                    var persistedProduct = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    Assert.Equal(customerId, persistedOrder.CustomerId);
                    Assert.Equal("BRL", persistedOrder.Currency);
                    Assert.Equal(OrderStatusEnum.Placed, persistedOrder.Status);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                    Assert.Null(persistedOrder.UpdatedAt);
                    Assert.Null(persistedOrder.UpdatedBy);
                    Assert.Null(persistedOrder.ConfirmedBy);
                    Assert.Null(persistedOrder.CanceledBy);

                    Assert.Single(persistedOrder.Items);

                    var persistedItem = persistedOrder.Items.Single();

                    Assert.Equal(productId, persistedItem.ProductId);
                    Assert.Equal(requestedQuantity, persistedItem.Quantity);
                    Assert.Equal(productPrice, persistedItem.UnitPrice);
                    Assert.Equal(76.50m, persistedOrder.Total);

                    // Criar o pedido não reserva nem desconta estoque.
                    Assert.Equal(initialStock, persistedProduct.StockQuantity);
                }
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                if (createdOrderId.HasValue)
                {
                    await cleanupContext.Orders
                        .Where(order => order.Id == createdOrderId.Value)
                        .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                }

                await cleanupContext.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task CreateOrder_ShouldPreserveItemPrice_WhenProductPriceChanges()
        {
            var productId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            const decimal originalPrice = 25.50m;
            const decimal updatedPrice = 39.90m;

            var productResult = Product.Create(
                productId,
                "Produto para teste de preço histórico",
                originalPrice,
                "BRL", 10);

            Assert.True(productResult.IsSuccess);

            Guid? createdOrderId = null;

            try
            {
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddAsync(productResult.Value, TestContext.Current.CancellationToken);
                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Cria o pedido com o preço original do produto.
                await using (var creationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new CreateOrderHandler(
                        new OrderRepository(creationContext),
                        new ProductRepository(creationContext),
                        new EfTransactionManager(
                            creationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser());

                    var result = await handler.HandleAsync(
                        new CreateOrderCommand(
                            customerId,
                            "BRL",
                            [new CreateOrderItemData(productId, 2)]),
                        TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);

                    createdOrderId = result.Value.Id;
                    Assert.Equal(51.00m, result.Value.Total);
                }

                // Atualiza o preço do produto diretamente no PostgreSQL.
                await using (var updateContext = IntegrationTestDatabase.CreateContext())
                {
                    var affectedRows = await updateContext.Products
                        .Where(product => product.Id == productId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                product => product.Price,
                                updatedPrice), TestContext.Current.CancellationToken);

                    Assert.Equal(1, affectedRows);
                }

                // Consulta novamente o pedido e o produto.
                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var persistedOrder = await verificationContext.Orders
                        .AsNoTracking()
                        .Include(order => order.Items)
                        .SingleAsync(order => order.Id == createdOrderId, TestContext.Current.CancellationToken);

                    var persistedProduct = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

                    var persistedItem = Assert.Single(persistedOrder.Items);

                    Assert.Equal(updatedPrice, persistedProduct.Price);
                    Assert.Equal(originalPrice, persistedItem.UnitPrice);
                    Assert.Equal(51.00m, persistedOrder.Total);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                }
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                if (createdOrderId.HasValue)
                {
                    await cleanupContext.Orders
                        .Where(order => order.Id == createdOrderId.Value)
                        .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                }

                await cleanupContext.Products
                    .Where(product => product.Id == productId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task CreateOrder_ShouldPersistMultipleItems_WithoutDecreasingStock()
        {
            var firstProductId = Guid.NewGuid();
            var secondProductId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var firstProductResult = Product.Create(
                firstProductId, "Produto A - múltiplos itens", 25.50m, "BRL", 10);

            var secondProductResult = Product.Create(
                secondProductId, "Produto B - múltiplos itens", 15.75m, "BRL", 20);

            Assert.True(firstProductResult.IsSuccess);
            Assert.True(secondProductResult.IsSuccess);

            Guid? createdOrderId = null;

            try
            {
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddRangeAsync(
                        firstProductResult.Value,
                        secondProductResult.Value);

                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                await using (var creationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new CreateOrderHandler(
                        new OrderRepository(creationContext),
                        new ProductRepository(creationContext),
                        new EfTransactionManager(
                            creationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser());

                    var result = await handler.HandleAsync(
                        new CreateOrderCommand(
                            customerId,
                            "BRL",
                            [
                                new CreateOrderItemData(firstProductId, 2),
                        new CreateOrderItemData(secondProductId, 3)
                            ]),
                        TestContext.Current.CancellationToken);

                    Assert.True(result.IsSuccess);

                    createdOrderId = result.Value.Id;

                    Assert.Equal(OrderStatusEnum.Placed, result.Value.Status);
                    Assert.Equal(98.25m, result.Value.Total);
                }

                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var persistedOrder = await verificationContext.Orders
                        .AsNoTracking()
                        .Include(order => order.Items)
                        .SingleAsync(order => order.Id == createdOrderId, TestContext.Current.CancellationToken);

                    var persistedProducts = await verificationContext.Products
                        .AsNoTracking()
                        .Where(product =>
                            product.Id == firstProductId ||
                            product.Id == secondProductId)
                        .ToListAsync(TestContext.Current.CancellationToken);

                    Assert.Equal(2, persistedOrder.Items.Count);
                    Assert.Equal(98.25m, persistedOrder.Total);
                    Assert.Equal(OrderStatusEnum.Placed, persistedOrder.Status);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);

                    var firstItem = Assert.Single(
                        persistedOrder.Items,
                        item => item.ProductId == firstProductId);

                    Assert.Equal(2, firstItem.Quantity);
                    Assert.Equal(25.50m, firstItem.UnitPrice);

                    var secondItem = Assert.Single(
                        persistedOrder.Items,
                        item => item.ProductId == secondProductId);

                    Assert.Equal(3, secondItem.Quantity);
                    Assert.Equal(15.75m, secondItem.UnitPrice);

                    Assert.Equal(
                        10,
                        persistedProducts.Single(product =>
                            product.Id == firstProductId).StockQuantity);

                    Assert.Equal(
                        20,
                        persistedProducts.Single(product =>
                            product.Id == secondProductId).StockQuantity);
                }
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                if (createdOrderId.HasValue)
                {
                    await cleanupContext.Orders
                        .Where(order => order.Id == createdOrderId.Value)
                        .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                }

                await cleanupContext.Products
                    .Where(product =>
                        product.Id == firstProductId ||
                        product.Id == secondProductId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

        [Fact]
        public async Task CreateOrder_ShouldNotPersistOrder_WhenProductHasInsufficientStock()
        {
            var firstProductId = Guid.NewGuid();
            var secondProductId = Guid.NewGuid();
            var customerId = Guid.NewGuid();

            var firstProductResult = Product.Create(
                firstProductId,
                "Produto A - criação rejeitada",
                25m,
                "BRL", 10);

            var secondProductResult = Product.Create(
                secondProductId,
                "Produto B - estoque insuficiente",
                15m,
                "BRL", 2);

            Assert.True(firstProductResult.IsSuccess);
            Assert.True(secondProductResult.IsSuccess);

            try
            {
                await using (var setupContext = IntegrationTestDatabase.CreateContext())
                {
                    await setupContext.Products.AddRangeAsync(
                        firstProductResult.Value,
                        secondProductResult.Value);

                    await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                await using (var creationContext = IntegrationTestDatabase.CreateContext())
                {
                    var handler = new CreateOrderHandler(
                        new OrderRepository(creationContext),
                        new ProductRepository(creationContext),
                        new EfTransactionManager(
                            creationContext,
                            NullLogger<EfTransactionManager>.Instance),
                        TimeProvider.System,
                        CreateAuthenticatedUser());

                    var command = new CreateOrderCommand(
                        customerId,
                        "BRL",
                            [
                            new CreateOrderItemData(firstProductId, 3),
                    new CreateOrderItemData(secondProductId, 5)
                        ]);

                    var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

                    Assert.False(result.IsSuccess);

                    Assert.Contains(
                        result.Errors,
                        error => error.Code == "order.insufficient_stock");
                }

                // Verifica que a tentativa rejeitada não criou pedidos nem itens.
                await using (var verificationContext = IntegrationTestDatabase.CreateContext())
                {
                    var persistedOrders = await verificationContext.Orders
                        .AsNoTracking()
                        .Where(order => order.CustomerId == customerId)
                        .ToListAsync(TestContext.Current.CancellationToken);

                    Assert.Empty(persistedOrders);

                    var firstProduct = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == firstProductId, TestContext.Current.CancellationToken);

                    var secondProduct = await verificationContext.Products
                        .AsNoTracking()
                        .SingleAsync(product => product.Id == secondProductId, TestContext.Current.CancellationToken);

                    Assert.Equal(10, firstProduct.StockQuantity);
                    Assert.Equal(2, secondProduct.StockQuantity);
                }
            }
            finally
            {
                await using var cleanupContext = IntegrationTestDatabase.CreateContext();

                await cleanupContext.Orders
                    .Where(order => order.CustomerId == customerId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

                await cleanupContext.Products
                    .Where(product =>
                        product.Id == firstProductId ||
                        product.Id == secondProductId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }
        }

    }
}
