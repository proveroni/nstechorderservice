using Microsoft.Extensions.Time.Testing;
using Moq;
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Operations.CreateOrder.Commands;
using OrderService.Application.Orders.Operations.CreateOrder.DTOs;
using OrderService.Application.Orders.Operations.CreateOrder.DTOs.Responses;
using OrderService.Application.Orders.Operations.CreateOrder.Handlers;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Application.UnitTests.Orders.Operations.CreateOrder.Handlers
{
    /// <summary>
    /// Testes unitários do caso de uso de criação de pedidos.
    /// </summary>
    public sealed class CreateOrderHandlerTests
    {
        private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
        private readonly Mock<IProductRepository> _productRepositoryMock = new();
        private readonly Mock<ITransactionManager> _transactionManagerMock = new();

        private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

        private const string AuthenticatedUserId = "test-user-001";
        private const string Currency = "BRL";
        private readonly Mock<ICurrentUser> _currentUserMock = new();

        [Fact]
        public async Task HandleAsync_ShouldRejectNullRequest()
        {
            // Arrange
            var handler = CreateHandler();

            // Act
            var result = await handler.HandleAsync(null!, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_request");

            VerifyNoPersistenceCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectEmptyCustomerId()
        {
            // Arrange
            var handler = CreateHandler();

            var request = new CreateOrderCommand(
                Guid.Empty,
                Currency,
                [
                    new CreateOrderItemData(Guid.NewGuid(), 1)
                ]);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_customer");

            VerifyNoPersistenceCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectNullItems()
        {
            // Arrange
            var handler = CreateHandler();

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                null!);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_items");

            VerifyNoPersistenceCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectEmptyItems()
        {
            // Arrange
            var handler = CreateHandler();

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                []);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.empty_items");

            VerifyNoPersistenceCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectNullItem()
        {
            // Arrange
            var handler = CreateHandler();

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [
                    null!
                ]);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_item");

            VerifyNoPersistenceCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldAccumulateInputErrors()
        {
            // Arrange
            var handler = CreateHandler();

            var request = new CreateOrderCommand(
                Guid.Empty,
                Currency,
                []);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);

            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_customer");

            Assert.Contains(result.Errors,
                error => error.Code == "order.empty_items");

            VerifyNoPersistenceCalls();
        }


        [Fact]
        public async Task HandleAsync_ShouldRejectEmptyProductId()
        {
            // Arrange
            var handler = CreateHandler();

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [new CreateOrderItemData(Guid.Empty, 1)]);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_product");

            VerifyNoPersistenceCalls();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task HandleAsync_ShouldRejectInvalidQuantity(
            int quantity)
        {
            // Arrange
            var handler = CreateHandler();

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [new CreateOrderItemData(Guid.NewGuid(), quantity)]);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_quantity");

            VerifyNoPersistenceCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectQuantityOverflow()
        {
            // Arrange
            var handler = CreateHandler();
            var productId = Guid.NewGuid();

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [
                    new CreateOrderItemData(productId, int.MaxValue),
                new CreateOrderItemData(productId, 1)
                ]);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.quantity_overflow");

            VerifyNoPersistenceCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectProductNotFound()
        {
            // Arrange
            var handler = CreateHandler();
            var productId = Guid.NewGuid();

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [new CreateOrderItemData(productId, 1)]);

            _productRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    productId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.product_not_found");

            _productRepositoryMock.Verify(
                repository => repository.GetByIdAsync(
                    productId,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _orderRepositoryMock.VerifyNoOtherCalls();
            _transactionManagerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectInsufficientStock()
        {
            // Arrange
            var handler = CreateHandler();
            var productId = Guid.NewGuid();

            var product = OrderService.Domain.Entities.Product.Create(
                productId,
                "Produto de teste",
                10m,
                Currency,
                3).Value;

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [new CreateOrderItemData(productId, 4)]);

            _productRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    productId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.insufficient_stock");

            Assert.Equal(3, product.StockQuantity);

            _orderRepositoryMock.VerifyNoOtherCalls();
            _transactionManagerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldCreateOrderWithConsolidatedItems()
        {
            // Arrange
            var handler = CreateHandler();

            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var product = Product.Create(
                productId,
                "Produto de teste",
                25.50m,
                Currency,
                10).Value;

            var request = new CreateOrderCommand(
                customerId,
                Currency,
                [
                    new CreateOrderItemData(productId, 2),
                new CreateOrderItemData(productId, 3)
                ]);

            _productRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    productId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            _transactionManagerMock
                .Setup(manager => manager.ExecuteAsync<CreateOrderResponse>(
                    It.IsAny<Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>> operation,
                    CancellationToken cancellationToken) =>
                        operation(cancellationToken));

            Order? capturedOrder = null;

            _orderRepositoryMock
                .Setup(repository => repository.AddAsync(
                    It.IsAny<Order>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Order, CancellationToken>(
                    (order, _) => capturedOrder = order)
                .Returns(Task.CompletedTask);

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            var response = result.Value;

            Assert.NotEqual(Guid.Empty, response.Id);
            Assert.Equal(customerId, response.CustomerId);
            Assert.Equal(Currency, response.Currency);
            Assert.Equal(OrderStatusEnum.Placed, response.Status);
            Assert.Equal(127.50m, response.Total);
            Assert.Equal(_timeProvider.GetUtcNow(), response.CreatedAt);

            Assert.NotNull(capturedOrder);
            Assert.Equal(response.Id, capturedOrder.Id);
            Assert.Equal(Currency, capturedOrder.Currency);
            Assert.Single(capturedOrder.Items);

            Assert.Equal(AuthenticatedUserId, capturedOrder.CreatedBy);
            Assert.Null(capturedOrder.UpdatedAt);
            Assert.Null(capturedOrder.UpdatedBy);

            var item = Assert.Single(capturedOrder.Items);

            Assert.Equal(productId, item.ProductId);
            Assert.Equal(5, item.Quantity);
            Assert.Equal(25.50m, item.UnitPrice);
            Assert.Equal(127.50m, item.Total);

            Assert.Equal(10, product.StockQuantity);

            _productRepositoryMock.Verify(
                repository => repository.GetByIdAsync(
                    productId,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _orderRepositoryMock.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Order>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _transactionManagerMock.Verify(
                manager => manager.ExecuteAsync<CreateOrderResponse>(
                    It.IsAny<Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _productRepositoryMock.VerifyNoOtherCalls();
            _orderRepositoryMock.VerifyNoOtherCalls();
            _transactionManagerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectProductWithDifferentCurrency()
        {
            // Arrange
            var handler = CreateHandler();
            var productId = Guid.NewGuid();

            var product = Product.Create(
                productId,
                "Produto em dólares",
                20m,
                "USD",
                10).Value;

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [new CreateOrderItemData(productId, 1)]);

            _productRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    productId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            // Act
            var result = await handler.HandleAsync(
                request,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.currency_mismatch");
            _orderRepositoryMock.VerifyNoOtherCalls();
            _transactionManagerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldPropagateTransactionFailure()
        {
            // Arrange
            var handler = CreateHandler();

            var productId = Guid.NewGuid();

            var product = Product.Create(
                productId,
                "Produto de teste",
                20m,
                Currency,
                10).Value;

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [new CreateOrderItemData(productId, 2)]);

            _productRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    productId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            var transactionError = new Error(
                "transaction.failed",
                "Não foi possível concluir a transação.");

            _transactionManagerMock
                .Setup(manager => manager.ExecuteAsync<CreateOrderResponse>(
                    It.IsAny<Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    Result<CreateOrderResponse>.Failure(transactionError));

            // Act
            var result = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(transactionError, result.Errors);

            _transactionManagerMock.Verify(
                manager => manager.ExecuteAsync<CreateOrderResponse>(
                    It.IsAny<Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _orderRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldPropagateCancellationToken()
        {
            // Arrange
            var handler = CreateHandler();

            var productId = Guid.NewGuid();

            var product = Product.Create(
                productId,
                "Produto de teste",
                15m,
                Currency,
                10).Value;

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [new CreateOrderItemData(productId, 1)]);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            var cancellationToken = cancellationTokenSource.Token;

            _productRepositoryMock
                .Setup(repository => repository.GetByIdAsync(
                    productId,
                    cancellationToken))
                .ReturnsAsync(product);

            _transactionManagerMock
                .Setup(manager => manager.ExecuteAsync<CreateOrderResponse>(
                    It.IsAny<Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>>>(),
                    cancellationToken))
                .Returns((
                    Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>> operation,
                    CancellationToken token) =>
                        operation(token));

            _orderRepositoryMock
                .Setup(repository => repository.AddAsync(
                    It.IsAny<Order>(),
                    cancellationToken))
                .Returns(Task.CompletedTask);

            // Act
            var result = await handler.HandleAsync(
                request,
                cancellationToken);

            // Assert
            Assert.True(result.IsSuccess);

            _productRepositoryMock.Verify(
                repository => repository.GetByIdAsync(
                    productId,
                    cancellationToken),
                Times.Once);

            _transactionManagerMock.Verify(
                manager => manager.ExecuteAsync<CreateOrderResponse>(
                    It.IsAny<Func<CancellationToken,
                        Task<Result<CreateOrderResponse>>>>(),
                    cancellationToken),
                Times.Once);

            _orderRepositoryMock.Verify(
                repository => repository.AddAsync(
                    It.IsAny<Order>(),
                    cancellationToken),
                Times.Once);

            _productRepositoryMock.VerifyNoOtherCalls();
            _transactionManagerMock.VerifyNoOtherCalls();
            _orderRepositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_ShouldRejectUnauthenticatedUser()
        {
            // Arrange
            var handler = CreateHandler();

            _currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(false);

            var request = new CreateOrderCommand(
                Guid.NewGuid(),
                Currency,
                [
                    new CreateOrderItemData(Guid.NewGuid(), 1)
                ]);

            // Act
            var result = await handler.HandleAsync(
                request,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsFailure);

            Assert.Contains(
                result.Errors,
                error => error.Code == "auth.unauthenticated");

            VerifyNoPersistenceCalls();
        }


        #region [Métodos auxiliares]

        /// <summary>
        /// Cria o handler com dependências simuladas e um
        /// usuário autenticado para os testes.
        /// </summary>
        private CreateOrderHandler CreateHandler()
        {
            _currentUserMock
                .Setup(user => user.IsAuthenticated)
                .Returns(true);

            _currentUserMock
                .Setup(user => user.UserId)
                .Returns(AuthenticatedUserId);

            return new CreateOrderHandler(
                _orderRepositoryMock.Object,
                _productRepositoryMock.Object,
                _transactionManagerMock.Object,
                _timeProvider,
                _currentUserMock.Object);
        }

        /// <summary>
        /// Verifica que entradas inválidas não provocaram
        /// consultas ou operações de persistência.
        /// </summary>
        private void VerifyNoPersistenceCalls()
        {
            _orderRepositoryMock.VerifyNoOtherCalls();
            _productRepositoryMock.VerifyNoOtherCalls();
            _transactionManagerMock.VerifyNoOtherCalls();
        }

        #endregion [Métodos auxiliares]
    }
}
