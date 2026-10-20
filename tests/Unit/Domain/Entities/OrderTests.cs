
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Domain.UnitTests.Entities
{
    /// <summary>
    /// Testes unitários das regras de criação, transição de estado
    /// e auditoria de pedidos.
    /// </summary>
    public sealed class OrderTests
    {
        private const string Currency = "BRL";
        private const string CreatedBy = "user-creator";
        private const string ConfirmedBy = "user-confirmer";
        private const string CanceledBy = "user-canceler";

        [Fact]
        public void Create_ShouldReturnPlacedOrder_WhenDataIsValid()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var createdAt = new DateTimeOffset(
                2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

            var item = OrderItem.Create(
                Guid.NewGuid(), 2, 15.50m).Value;

            // Act
            var result = Order.Create(
                customerId,
                Currency,
                new[] { item },
                createdAt,
                CreatedBy);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);

            var order = result.Value;

            Assert.NotEqual(Guid.Empty, order.Id);
            Assert.Equal(customerId, order.CustomerId);
            Assert.Equal(Currency, order.Currency);
            Assert.Equal(OrderStatusEnum.Placed, order.Status);
            Assert.Equal(createdAt, order.CreatedAt);
            Assert.Equal(CreatedBy, order.CreatedBy);
            Assert.Null(order.UpdatedAt);
            Assert.Null(order.UpdatedBy);
            Assert.Null(order.ConfirmedAt);
            Assert.Null(order.ConfirmedBy);
            Assert.Null(order.CanceledAt);
            Assert.Null(order.CanceledBy);
            Assert.Single(order.Items);
            Assert.Equal(31.00m, order.Total);
        }

        [Theory]
        [InlineData("BRL")]
        [InlineData("brl")]
        [InlineData("BrL")]
        public void Create_ShouldNormalizeCurrencyToUppercase(string currency)
        {
            // Arrange
            var item = OrderItem.Create(Guid.NewGuid(), 1, 10m).Value;

            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                currency,
                new[] { item },
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("BRL", result.Value.Currency);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("BR")]
        [InlineData("BRLL")]
        [InlineData("B1L")]
        [InlineData("R$L")]
        [InlineData("ÁRL")]
        public void Create_ShouldRejectInvalidCurrency(string? currency)
        {
            // Arrange
            var item = OrderItem.Create(Guid.NewGuid(), 1, 10m).Value;

            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                currency!,
                new[] { item },
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_currency");
        }

        [Fact]
        public void Create_ShouldRejectEmptyCustomerId()
        {
            // Arrange
            var item = OrderItem.Create(
                Guid.NewGuid(), 1, 10m).Value;

            // Act
            var result = Order.Create(
                Guid.Empty,
                Currency,
                new[] { item },
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_customer");
        }

        [Fact]
        public void Create_ShouldRejectNullItems()
        {
            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                Currency,
                null!,
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_items");
        }

        [Fact]
        public void Create_ShouldRejectEmptyItems()
        {
            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                Currency,
                Array.Empty<OrderItem>(),
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.empty_items");
        }

        [Fact]
        public void Create_ShouldRejectNullItem()
        {
            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                Currency,
                new OrderItem[] { null! },
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_item");
        }

        [Fact]
        public void Create_ShouldCalculateTotalFromAllItems()
        {
            // Arrange
            var first = OrderItem.Create(
                Guid.NewGuid(), 2, 10m).Value;

            var second = OrderItem.Create(
                Guid.NewGuid(), 3, 5m).Value;

            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                Currency,
                new[] { first, second },
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(35m, result.Value.Total);
            Assert.Equal(2, result.Value.Items.Count);
        }

        [Fact]
        public void Create_ShouldRejectTotalOverflow()
        {
            // Arrange
            const decimal unitPrice = 9_999_999_999_999_999.99m;

            var items = Enumerable.Range(0, 3690)
                .Select(_ => OrderItem.Create(
                    Guid.NewGuid(),
                    int.MaxValue,
                    unitPrice).Value)
                .ToArray();

            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                Currency,
                items,
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(
                result.Errors,
                error => error.Code == "order.total_overflow");
        }

        [Fact]
        public void Create_ShouldNotBeAffectedByChangesToSourceCollection()
        {
            // Arrange
            var first = OrderItem.Create(
                Guid.NewGuid(), 1, 10m).Value;

            var second = OrderItem.Create(
                Guid.NewGuid(), 1, 20m).Value;

            var items = new List<OrderItem> { first };

            // Act
            var result = Order.Create(
                Guid.NewGuid(),
                Currency,
                items,
                DateTimeOffset.UtcNow,
                CreatedBy);

            items.Add(second);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Single(result.Value.Items);
            Assert.Equal(10m, result.Value.Total);
        }

        [Fact]
        public void Create_ShouldAccumulateValidationErrors()
        {
            // Act
            var result = Order.Create(
                Guid.Empty,
                Currency,
                Array.Empty<OrderItem>(),
                DateTimeOffset.UtcNow,
                CreatedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains(result.Errors,
                error => error.Code == "order.invalid_customer");
            Assert.Contains(result.Errors,
                error => error.Code == "order.empty_items");
        }

        [Fact]
        public void Confirm_ShouldChangePlacedOrderToConfirmed()
        {
            // Arrange
            var order = CreateValidOrder();
            var confirmedAt = new DateTimeOffset(
                2026, 10, 15, 14, 0, 0, TimeSpan.Zero);

            // Act
            var result = order.Confirm(confirmedAt, ConfirmedBy);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Confirmed, order.Status);
            Assert.Equal(confirmedAt, order.ConfirmedAt);
            Assert.Equal(ConfirmedBy, order.ConfirmedBy);
            Assert.Equal(confirmedAt, order.UpdatedAt);
            Assert.Equal(ConfirmedBy, order.UpdatedBy);
            Assert.Equal(CreatedBy, order.CreatedBy);
            Assert.Null(order.CanceledAt);
            Assert.Null(order.CanceledBy);
        }

        [Fact]
        public void Confirm_ShouldBeIdempotent_WhenAlreadyConfirmed()
        {
            // Arrange
            var order = CreateValidOrder();

            var firstConfirmation = new DateTimeOffset(
                2026, 10, 15, 14, 0, 0, TimeSpan.Zero);

            var secondConfirmation = firstConfirmation.AddMinutes(10);

            order.Confirm(firstConfirmation, ConfirmedBy);

            // Act
            var result = order.Confirm(
                secondConfirmation,
                "another-confirmer");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Confirmed, order.Status);
            Assert.Equal(firstConfirmation, order.ConfirmedAt);
            Assert.Equal(ConfirmedBy, order.ConfirmedBy);
            Assert.Equal(firstConfirmation, order.UpdatedAt);
            Assert.Equal(ConfirmedBy, order.UpdatedBy);
        }

        [Fact]
        public void Confirm_ShouldFail_WhenOrderIsCanceled()
        {
            // Arrange
            var order = CreateValidOrder();
            var canceledAt = new DateTimeOffset(
                2026, 10, 15, 15, 0, 0, TimeSpan.Zero);

            order.Cancel(canceledAt, CanceledBy);

            // Act
            var result = order.Confirm(
                canceledAt.AddMinutes(10),
                ConfirmedBy);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order.cannot_confirm");
            Assert.Equal(OrderStatusEnum.Canceled, order.Status);
            Assert.Null(order.ConfirmedAt);
            Assert.Null(order.ConfirmedBy);
            Assert.Equal(canceledAt, order.UpdatedAt);
            Assert.Equal(CanceledBy, order.UpdatedBy);
        }

        [Fact]
        public void Cancel_ShouldChangePlacedOrderToCanceled()
        {
            // Arrange
            var order = CreateValidOrder();
            var canceledAt = new DateTimeOffset(
                2026, 10, 15, 15, 0, 0, TimeSpan.Zero);

            // Act
            var result = order.Cancel(canceledAt, CanceledBy);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Canceled, order.Status);
            Assert.Equal(canceledAt, order.CanceledAt);
            Assert.Equal(CanceledBy, order.CanceledBy);
            Assert.Equal(canceledAt, order.UpdatedAt);
            Assert.Equal(CanceledBy, order.UpdatedBy);
            Assert.Equal(CreatedBy, order.CreatedBy);
            Assert.Null(order.ConfirmedAt);
            Assert.Null(order.ConfirmedBy);
        }

        [Fact]
        public void Cancel_ShouldChangeConfirmedOrderToCanceled()
        {
            // Arrange
            var order = CreateValidOrder();

            var confirmedAt = new DateTimeOffset(
                2026, 10, 15, 14, 0, 0, TimeSpan.Zero);

            var canceledAt = confirmedAt.AddHours(1);

            order.Confirm(confirmedAt, ConfirmedBy);

            // Act
            var result = order.Cancel(canceledAt, CanceledBy);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Canceled, order.Status);
            Assert.Equal(confirmedAt, order.ConfirmedAt);
            Assert.Equal(ConfirmedBy, order.ConfirmedBy);
            Assert.Equal(canceledAt, order.CanceledAt);
            Assert.Equal(CanceledBy, order.CanceledBy);
            Assert.Equal(canceledAt, order.UpdatedAt);
            Assert.Equal(CanceledBy, order.UpdatedBy);
            Assert.Equal(CreatedBy, order.CreatedBy);
        }

        [Fact]
        public void Cancel_ShouldBeIdempotent_WhenAlreadyCanceled()
        {
            // Arrange
            var order = CreateValidOrder();

            var firstCancellation = new DateTimeOffset(
                2026, 10, 15, 15, 0, 0, TimeSpan.Zero);

            var secondCancellation = firstCancellation.AddMinutes(10);

            order.Cancel(firstCancellation, CanceledBy);

            // Act
            var result = order.Cancel(
                secondCancellation,
                "another-canceler");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatusEnum.Canceled, order.Status);
            Assert.Equal(firstCancellation, order.CanceledAt);
            Assert.Equal(CanceledBy, order.CanceledBy);
            Assert.Equal(firstCancellation, order.UpdatedAt);
            Assert.Equal(CanceledBy, order.UpdatedBy);
        }

        #region [ Métodos auxiliares ]

        /// <summary>
        /// Cria um pedido válido para os testes de transição de estado.
        /// </summary>
        private static Order CreateValidOrder()
        {
            var item = OrderItem.Create(
                Guid.NewGuid(),
                1,
                10m).Value;

            return Order.Create(
                Guid.NewGuid(),
                Currency,
                new[] { item },
                new DateTimeOffset(
                    2026, 10, 15, 12, 0, 0, TimeSpan.Zero),
                CreatedBy).Value;
        }

        #endregion [ Métodos auxiliares ]
    }
}
