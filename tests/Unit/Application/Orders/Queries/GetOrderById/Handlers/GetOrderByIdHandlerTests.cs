using Moq;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Queries.GetOrderById.Handlers;
using OrderService.Application.Orders.Queries.GetOrderById.Queries;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Application.UnitTests.Orders.Queries.GetOrderById.Handlers
{
    /// <summary>
    /// Testes unitários do caso de uso de consulta de pedido por identificador.
    /// </summary>
    public sealed class GetOrderByIdHandlerTests
    {
        private readonly Mock<IOrderRepository> _orderRepositoryMock = new();

        [Fact]
        public async Task HandleAsync_ShouldRejectNullRequest()
        {
            // Arrange
            var handler = CreateHandler();

            // Act
            var result = await handler.HandleAsync(
                null!,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);

            Assert.Contains(
                result.Errors,
                error => error.Code == "order.invalid_request");

            _orderRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectEmptyOrderId()
        {
            // Arrange
            var handler = CreateHandler();

            var query = new GetOrderByIdQuery(Guid.Empty);

            // Act
            var result = await handler.HandleAsync(
                query,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);

            Assert.Contains(
                result.Errors,
                error => error.Code == "order.invalid_id");

            _orderRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectOrderNotFound()
        {
            // Arrange
            var handler = CreateHandler();

            var orderId = Guid.NewGuid();
            var query = new GetOrderByIdQuery(orderId);

            _orderRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    orderId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);

            // Act
            var result = await handler.HandleAsync(
                query,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);

            Assert.Contains(
                result.Errors,
                error => error.Code == "order.not_found");

            _orderRepositoryMock.Verify(
                repository => repository.GetByIdAsync(
                    orderId,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _orderRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnOrderWithItems()
        {
            // Arrange
            var handler = CreateHandler();

            var customerId = Guid.NewGuid();
            var firstProductId = Guid.NewGuid();
            var secondProductId = Guid.NewGuid();

            var createdAt = new DateTimeOffset(
                2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

            var firstItem = OrderItem.Create(
                firstProductId,
                2,
                25.50m).Value;

            var secondItem = OrderItem.Create(
                secondProductId,
                3,
                10.00m).Value;

            var order = Order.Create(
                customerId,
                "BRL",
                [firstItem, secondItem],
                createdAt,
                "test-user-001").Value;

            var query = new GetOrderByIdQuery(order.Id);

            _orderRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            var result = await handler.HandleAsync(
                query,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            var response = result.Value;

            Assert.Equal(order.Id, response.Id);
            Assert.Equal(customerId, response.CustomerId);
            Assert.Equal(OrderStatusEnum.Placed, response.Status);
            Assert.Equal(createdAt, response.CreatedAt);
            Assert.Equal(81.00m, response.Total);

            Assert.Equal(2, response.Items.Count);

            Assert.Contains(response.Items, item =>
                item.ProductId == firstProductId &&
                item.Quantity == 2 &&
                item.UnitPrice == 25.50m);

            Assert.Contains(response.Items, item =>
                item.ProductId == secondProductId &&
                item.Quantity == 3 &&
                item.UnitPrice == 10.00m);

            _orderRepositoryMock.Verify(
                repository => repository.GetByIdAsync(
                    order.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _orderRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldPropagateCancellationToken()
        {
            // Arrange
            var handler = CreateHandler();

            var orderId = Guid.NewGuid();
            var query = new GetOrderByIdQuery(orderId);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            var cancellationToken = cancellationTokenSource.Token;

            _orderRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    orderId,
                    cancellationToken))
                .ReturnsAsync((Order?)null);

            // Act
            var result = await handler.HandleAsync(
                query,
                cancellationToken);

            // Assert
            Assert.True(result.IsFailure);

            Assert.Contains(
                result.Errors,
                error => error.Code == "order.not_found");

            _orderRepositoryMock.Verify(
                repository => repository.GetByIdAsync(
                    orderId,
                    cancellationToken),
                Times.Once);

            _orderRepositoryMock.VerifyNoOtherCalls();
        }

        #region [ Métodos auxiliares ]

        /// <summary>
        /// Cria o handler com suas dependências simuladas.
        /// </summary>
        private GetOrderByIdHandler CreateHandler()
        {
            return new GetOrderByIdHandler(
                _orderRepositoryMock.Object);
        }

        #endregion [ Métodos auxiliares ]
    }
}
