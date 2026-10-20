using Moq;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Operations.CancelOrder.Commands;
using OrderService.Application.Orders.Operations.CancelOrder.Handlers;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Application.UnitTests.Orders.Operations.CancelOrder.Handlers
{
    /// <summary>
    /// Testes unitários do caso de uso de cancelamento de pedidos.
    /// </summary>
    public sealed class CancelOrderHandlerTests
    {

        private const string CreatedBy = "user-creator";
        private const string ConfirmedBy = "user-confirmer";
        private const string CanceledBy = "user-canceler";

        /// <summary>
        /// Cria uma simulação de usuário autenticado para
        /// as operações de cancelamento de pedidos.
        /// </summary>
        private static ICurrentUser CreateAuthenticatedUser()
        {
            var currentUserMock = new Mock<ICurrentUser>();

            currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(true);

            currentUserMock
                .Setup(user => user.UserId)
                .Returns(CanceledBy);

            return currentUserMock.Object;
        }


        [Fact]
        public async Task HandleAsync_ShouldCancelOrder_WhenOrderIsPlaced()
        {
            // Arrange
            var productId = Guid.NewGuid();

            var itemResult = OrderItem.Create(productId, 2, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var orderRepository = new Mock<IOrderRepository>();
            var transactionManager = new Mock<ITransactionManager>();
            var productRepository = new Mock<IProductRepository>();

            orderRepository
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            transactionManager
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                        operation(cancellationToken));

            var handler = new CancelOrderHandler(
                    orderRepository.Object,
                    productRepository.Object,
                    transactionManager.Object,
                    TimeProvider.System,
                    CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Canceled, order.Status);
            Assert.NotNull(order.CanceledAt);
            Assert.Equal(CreatedBy, order.CreatedBy);
            Assert.Equal(CanceledBy, order.CanceledBy);
            Assert.Equal(order.CanceledAt, order.UpdatedAt);
            Assert.Equal(CanceledBy, order.UpdatedBy);
            Assert.Null(order.ConfirmedBy);

            orderRepository.Verify(
                repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            transactionManager.Verify(
                manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }


        [Fact]
        public async Task HandleAsync_ShouldReturnFailure_WhenOrderIdIsEmpty()
        {
            // Arrange
            var orderRepository = new Mock<IOrderRepository>();
            var transactionManager = new Mock<ITransactionManager>();
            var productRepository = new Mock<IProductRepository>();

            var handler = new CancelOrderHandler(
                    orderRepository.Object,
                    productRepository.Object,
                    transactionManager.Object,
                    TimeProvider.System,
                    CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new CancelOrderCommand(Guid.Empty), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);

            Assert.Contains(
                result.Errors,
                error => error.Code == "order.invalid_id");

            orderRepository.VerifyNoOtherCalls();
            transactionManager.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldReturnFailure_WhenOrderDoesNotExist()
        {
            // Arrange
            var orderId = Guid.NewGuid();

            var orderRepository = new Mock<IOrderRepository>();
            var transactionManager = new Mock<ITransactionManager>();
            var productRepository = new Mock<IProductRepository>();

            orderRepository
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    orderId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);

            transactionManager
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                        operation(cancellationToken));

            var handler = new CancelOrderHandler(
                    orderRepository.Object,
                    productRepository.Object,
                    transactionManager.Object,
                    TimeProvider.System,
                    CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new CancelOrderCommand(orderId), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(result.Errors, error => error.Code == "order.not_found");

            orderRepository.Verify(
                repository => repository.GetByIdForUpdateAsync(
                    orderId,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }


        [Fact]
        public async Task HandleAsync_ShouldReturnSuccess_WhenOrderIsAlreadyCanceled()
        {
            // Arrange
            var itemResult = OrderItem.Create(Guid.NewGuid(), 2, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var originalCanceledAt = DateTimeOffset.UtcNow;
            var cancelResult = order.Cancel(originalCanceledAt, CanceledBy);
            Assert.True(cancelResult.IsSuccess);

            var orderRepository = new Mock<IOrderRepository>();
            var transactionManager = new Mock<ITransactionManager>();
            var productRepository = new Mock<IProductRepository>();

            orderRepository
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            transactionManager
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                        operation(cancellationToken));

            var handler = new CancelOrderHandler(
                    orderRepository.Object,
                    productRepository.Object,
                    transactionManager.Object,
                    TimeProvider.System,
                    CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Canceled, order.Status);
            Assert.Equal(originalCanceledAt, order.CanceledAt);
            Assert.Equal(CanceledBy, order.CanceledBy);
            Assert.Equal(originalCanceledAt, order.UpdatedAt);
            Assert.Equal(CanceledBy, order.UpdatedBy);
        }


        [Fact]
        public async Task HandleAsync_ShouldRestoreStock_WhenOrderIsConfirmed()
        {
            // Arrange
            var productId = Guid.NewGuid();
            const int quantity = 3;

            var itemResult = OrderItem.Create(productId, quantity, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var confirmResult = order.Confirm(DateTimeOffset.UtcNow, ConfirmedBy);
            Assert.True(confirmResult.IsSuccess);
            Assert.Equal(OrderStatusEnum.Confirmed, order.Status);

            var orderRepository = new Mock<IOrderRepository>();
            var productRepository = new Mock<IProductRepository>();
            var transactionManager = new Mock<ITransactionManager>();

            orderRepository
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            productRepository
                .Setup(repository => repository.TryIncreaseStockAsync(
                    productId,
                    quantity,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            transactionManager
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                        operation(cancellationToken));

            var handler = new CancelOrderHandler(
                orderRepository.Object,
                productRepository.Object,
                transactionManager.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Canceled, order.Status);
            Assert.NotNull(order.CanceledAt);
            Assert.Equal(CreatedBy, order.CreatedBy);
            Assert.Equal(ConfirmedBy, order.ConfirmedBy);
            Assert.Equal(CanceledBy, order.CanceledBy);
            Assert.Equal(order.CanceledAt, order.UpdatedAt);
            Assert.Equal(CanceledBy, order.UpdatedBy);

            productRepository.Verify(
                repository => repository.TryIncreaseStockAsync(
                    productId,
                    quantity,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepository.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldNotCancelOrder_WhenStockRestorationFails()
        {
            // Arrange
            var productId = Guid.NewGuid();
            const int quantity = 3;

            var itemResult = OrderItem.Create(productId, quantity, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                Guid.NewGuid(),
                "BRL",
                [itemResult.Value],
                DateTimeOffset.UtcNow,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var confirmResult = order.Confirm(DateTimeOffset.UtcNow, ConfirmedBy);
            Assert.True(confirmResult.IsSuccess);

            var originalConfirmedAt = order.ConfirmedAt;

            var orderRepository = new Mock<IOrderRepository>();
            var productRepository = new Mock<IProductRepository>();
            var transactionManager = new Mock<ITransactionManager>();

            orderRepository
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            productRepository
                .Setup(repository => repository.TryIncreaseStockAsync(
                    productId,
                    quantity,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            transactionManager
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                        operation(cancellationToken));

            var handler = new CancelOrderHandler(
                orderRepository.Object,
                productRepository.Object,
                transactionManager.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new CancelOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);

            Assert.Contains(
                result.Errors,
                error => error.Code == "product.stock_restoration_failed");

            Assert.Equal(OrderStatusEnum.Confirmed, order.Status);
            Assert.Equal(originalConfirmedAt, order.ConfirmedAt);
            Assert.Null(order.CanceledAt);
            Assert.Equal(ConfirmedBy, order.ConfirmedBy);
            Assert.Null(order.CanceledBy);
            Assert.Equal(originalConfirmedAt, order.UpdatedAt);
            Assert.Equal(ConfirmedBy, order.UpdatedBy);

            productRepository.Verify(
                repository => repository.TryIncreaseStockAsync(
                    productId,
                    quantity,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }


        [Fact]
        public async Task HandleAsync_ShouldRejectUnauthenticatedUser()
        {
            // Arrange
            var orderRepository = new Mock<IOrderRepository>();
            var productRepository = new Mock<IProductRepository>();
            var transactionManager = new Mock<ITransactionManager>();
            var currentUserMock = new Mock<ICurrentUser>();

            currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(false);

            var handler = new CancelOrderHandler(
                orderRepository.Object,
                productRepository.Object,
                transactionManager.Object,
                TimeProvider.System,
                currentUserMock.Object);

            var command = new CancelOrderCommand(Guid.NewGuid());

            // Act
            var result = await handler.HandleAsync(
                command,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);

            Assert.Contains(
                result.Errors,
                error => error.Code == "auth.unauthenticated");

            orderRepository.VerifyNoOtherCalls();
            productRepository.VerifyNoOtherCalls();
            transactionManager.VerifyNoOtherCalls();
        }

    }
}
