using OrderService.Domain.Entities;

namespace OrderService.Domain.UnitTests.Entities
{
    /// <summary>
    /// Testes unitários das regras de criação de itens de pedido.
    /// </summary>
    public sealed class OrderItemTests
    {
        [Fact]
        public void Create_ShouldReturnItem_WhenDataIsValid()
        {
            // Arrange
            var productId = Guid.NewGuid();

            // Act
            var result = OrderItem.Create(productId, 2, 15.50m);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);
            Assert.Equal(productId, result.Value.ProductId);
            Assert.Equal(2, result.Value.Quantity);
            Assert.Equal(15.50m, result.Value.UnitPrice);
            Assert.Equal(31.00m, result.Value.Total);
        }

        [Fact]
        public void Create_ShouldRejectEmptyProductId()
        {
            // Act
            var result = OrderItem.Create(Guid.Empty, 1, 10m);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_product");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10)]
        public void Create_ShouldRejectNonPositiveQuantity(int quantity)
        {
            // Act
            var result = OrderItem.Create(Guid.NewGuid(), quantity, 10m);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_quantity");
        }

        [Fact]
        public void Create_ShouldRejectNegativeUnitPrice()
        {
            // Act
            var result = OrderItem.Create(Guid.NewGuid(), 1, -0.01m);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_unit_price");
        }

        [Fact]
        public void Create_ShouldAllowZeroUnitPrice()
        {
            // Act
            var result = OrderItem.Create(Guid.NewGuid(), 1, 0m);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0m, result.Value.Total);
        }

        [Fact]
        public void Create_ShouldAccumulateValidationErrors()
        {
            // Act
            var result = OrderItem.Create(Guid.Empty, 0, -1m);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(3, result.Errors.Count);
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_product");
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_quantity");
            Assert.Contains(result.Errors,
                error => error.Code == "order_item.invalid_unit_price");
        }

        [Theory]
        [InlineData("25.555")]
        [InlineData("0.001")]
        [InlineData("100.999")]
        public void Create_ShouldRejectUnitPriceWithMoreThanTwoSignificantDecimalPlaces(
            string priceText)
        {
            // Arrange
            var unitPrice = decimal.Parse(
                priceText,
                System.Globalization.CultureInfo.InvariantCulture);

            // Act
            var result = OrderItem.Create(
                Guid.NewGuid(),
                2,
                unitPrice);

            // Assert
            Assert.False(result.IsSuccess);

            Assert.Contains(
                result.Errors,
                error => error.Code == "order_item.invalid_unit_price_precision");
        }

        [Fact]
        public void Create_ShouldRejectUnitPriceAboveDatabaseLimit()
        {
            // Act
            var result = OrderItem.Create(
                Guid.NewGuid(),
                2,
                10_000_000_000_000_000m);

            // Assert
            Assert.False(result.IsSuccess);

            Assert.Contains(
                result.Errors,
                error => error.Code == "order_item.unit_price_out_of_range");
        }

        [Theory]
        [InlineData("25")]
        [InlineData("25.50")]
        [InlineData("25.500")]
        [InlineData("9999999999999999.99")]
        public void Create_ShouldAcceptUnitPriceCompatibleWithDatabase(
            string priceText)
        {
            // Arrange
            var unitPrice = decimal.Parse(
                priceText,
                System.Globalization.CultureInfo.InvariantCulture);

            // Act
            var result = OrderItem.Create(
                Guid.NewGuid(),
                1,
                unitPrice);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(unitPrice, result.Value.UnitPrice);
        }

    }
}
