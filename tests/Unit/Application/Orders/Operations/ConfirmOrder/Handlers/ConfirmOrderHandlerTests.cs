using Moq;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Operations.ConfirmOrder.Commands;
using OrderService.Application.Orders.Operations.ConfirmOrder.Handlers;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;


namespace OrderService.Application.UnitTests.Orders.Operations.ConfirmOrder.Handlers
{
    /// <summary>
    /// Testes unitários do caso de uso de confirmação de pedidos.
    /// </summary>
    public sealed class ConfirmOrderHandlerTests
    {
        private const string CreatedBy = "user-creator";
        private const string ConfirmedBy = "user-confirmer";
        private const string CanceledBy = "user-canceler";

        /// <summary>
        /// Cria uma simulação de usuário autenticado.
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


        [Fact]
        public async Task HandleAsync_ShouldReturnFailure_WhenOrderIdIsEmpty()
        {
            // Arrange
            var command = new ConfirmOrderCommand(Guid.Empty);

            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);

            orderRepositoryMock.VerifyNoOtherCalls();
            productRepositoryMock.VerifyNoOtherCalls();
            transactionManagerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnFailure_WhenOrderDoesNotExist()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var command = new ConfirmOrderCommand(orderId);

            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();

            orderRepositoryMock
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    orderId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);

            transactionManagerMock
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                    operation(cancellationToken));

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);

            orderRepositoryMock.Verify(repository =>
                repository.GetByIdForUpdateAsync(
                    orderId,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            transactionManagerMock.Verify(manager =>
                manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldReturnSuccessWithoutDecreasingStock_WhenOrderIsAlreadyConfirmed()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow.AddHours(-2);
            var confirmedAt = createdAt.AddHours(1);

            var itemResult = OrderItem.Create(productId, 2, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                createdAt,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var confirmationResult = order.Confirm(confirmedAt, ConfirmedBy);
            Assert.True(confirmationResult.IsSuccess);

            var originalConfirmedAt = order.ConfirmedAt;

            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();

            orderRepositoryMock
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            transactionManagerMock
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                    operation(cancellationToken));

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(originalConfirmedAt, order.ConfirmedAt);
            Assert.Equal(ConfirmedBy, order.ConfirmedBy);
            Assert.Equal(originalConfirmedAt, order.UpdatedAt);
            Assert.Equal(ConfirmedBy, order.UpdatedBy);

            orderRepositoryMock.Verify(repository =>
                repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldReturnFailureWithoutDecreasingStock_WhenOrderIsCanceled()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow.AddHours(-2);
            var canceledAt = createdAt.AddHours(1);

            var itemResult = OrderItem.Create(productId, 2, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                createdAt,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var cancellationResult = order.Cancel(canceledAt, CanceledBy);
            Assert.True(cancellationResult.IsSuccess);

            var originalCanceledAt = order.CanceledAt;

            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();

            orderRepositoryMock
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            transactionManagerMock
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                    operation(cancellationToken));

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(originalCanceledAt, order.CanceledAt);

            orderRepositoryMock.Verify(repository =>
                repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldReturnFailure_WhenStockIsInsufficient()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow.AddHours(-1);

            var itemResult = OrderItem.Create(productId, 3, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                createdAt,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();

            orderRepositoryMock
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            productRepositoryMock
                .Setup(repository => repository.TryDecreaseStockAsync(
                    productId,
                    3,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            transactionManagerMock
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                    operation(cancellationToken));

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Placed, order.Status);
            Assert.Null(order.ConfirmedAt);

            productRepositoryMock.Verify(repository =>
                repository.TryDecreaseStockAsync(
                    productId,
                    3,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldConfirmOrder_WhenStockIsAvailable()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow.AddHours(-1);

            var itemResult = OrderItem.Create(productId, 2, 25m);
            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                createdAt,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();

            orderRepositoryMock
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            productRepositoryMock
                .Setup(repository => repository.TryDecreaseStockAsync(
                    productId,
                    2,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            transactionManagerMock
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                    operation(cancellationToken));

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Confirmed, order.Status);
            Assert.NotNull(order.ConfirmedAt);
            Assert.True(order.ConfirmedAt >= createdAt);
            Assert.Equal(CreatedBy, order.CreatedBy);
            Assert.Equal(ConfirmedBy, order.ConfirmedBy);
            Assert.Equal(order.ConfirmedAt, order.UpdatedAt);
            Assert.Equal(ConfirmedBy, order.UpdatedBy);
            Assert.Null(order.CanceledBy);

            orderRepositoryMock.Verify(repository =>
                repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.Verify(repository =>
                repository.TryDecreaseStockAsync(
                    productId,
                    2,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldDecreaseStockForEveryItem_WhenOrderHasMultipleItems()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var firstProductId = Guid.NewGuid();
            var secondProductId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow.AddHours(-1);

            var firstItemResult = OrderItem.Create(firstProductId, 2, 25m);
            var secondItemResult = OrderItem.Create(secondProductId, 3, 40m);

            Assert.True(firstItemResult.IsSuccess);
            Assert.True(secondItemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [firstItemResult.Value, secondItemResult.Value],
                createdAt,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();

            orderRepositoryMock
                .Setup(repository => repository.GetByIdForUpdateAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            productRepositoryMock
                .Setup(repository => repository.TryDecreaseStockAsync(
                    firstProductId,
                    2,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            productRepositoryMock
                .Setup(repository => repository.TryDecreaseStockAsync(
                    secondProductId,
                    3,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            transactionManagerMock
                .Setup(manager => manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken, Task<Result>> operation,
                    CancellationToken cancellationToken) =>
                    operation(cancellationToken));

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                CreateAuthenticatedUser());

            // Act
            var result = await handler.HandleAsync(new ConfirmOrderCommand(order.Id), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Confirmed, order.Status);
            Assert.NotNull(order.ConfirmedAt);

            productRepositoryMock.Verify(repository =>
                repository.TryDecreaseStockAsync(
                    firstProductId,
                    2,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.Verify(repository =>
                repository.TryDecreaseStockAsync(
                    secondProductId,
                    3,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            productRepositoryMock.VerifyNoOtherCalls();

            transactionManagerMock.Verify(manager =>
                manager.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<Result>>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }


        [Fact]
        public async Task HandleAsync_ShouldRejectUnauthenticatedUser()
        {
            // Arrange
            var orderRepositoryMock = new Mock<IOrderRepository>();
            var productRepositoryMock = new Mock<IProductRepository>();
            var transactionManagerMock = new Mock<ITransactionManager>();
            var currentUserMock = new Mock<ICurrentUser>();

            currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(false);

            var handler = new ConfirmOrderHandler(
                orderRepositoryMock.Object,
                productRepositoryMock.Object,
                transactionManagerMock.Object,
                TimeProvider.System,
                currentUserMock.Object);

            var command = new ConfirmOrderCommand(Guid.NewGuid());

            // Act
            var result = await handler.HandleAsync(
                command,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);

            Assert.Contains(
                result.Errors,
                error => error.Code == "auth.unauthenticated");

            orderRepositoryMock.VerifyNoOtherCalls();
            productRepositoryMock.VerifyNoOtherCalls();
            transactionManagerMock.VerifyNoOtherCalls();
        }


    }
}
