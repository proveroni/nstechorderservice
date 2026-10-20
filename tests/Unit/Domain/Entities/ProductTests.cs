using OrderService.Domain.Entities;

namespace OrderService.Domain.UnitTests.Entities
{
    /// <summary>
    /// Testes unitários das regras de criação de produtos.
    /// </summary>
    public sealed class ProductTests
    {
        [Fact]
        public void Create_ShouldReturnProduct_WhenDataIsValid()
        {
            // Arrange
            var productId = Guid.NewGuid();

            // Act
            var result = Product.Create(
                productId,
                "Produto de teste",
                25.50m,
                "BRL",
                10);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);

            var product = result.Value;

            Assert.Equal(productId, product.Id);
            Assert.Equal("Produto de teste", product.Name);
            Assert.Equal(25.50m, product.Price);
            Assert.Equal("BRL", product.Currency);
            Assert.Equal(10, product.StockQuantity);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("BR")]
        [InlineData("BRLL")]
        [InlineData("B1L")]
        [InlineData("R$L")]
        public void Create_ShouldRejectInvalidCurrency(string? currency)
        {
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto de teste",
                10m,
                currency!,
                5);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_currency");
        }

        [Theory]
        [InlineData("brl")]
        [InlineData("BrL")]
        [InlineData("BRL")]
        public void Create_ShouldNormalizeCurrencyToUppercase(string currency)
        {
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto de teste",
                10m,
                currency,
                5);

            Assert.True(result.IsSuccess);
            Assert.Equal("BRL", result.Value.Currency);
        }

        [Fact]
        public void Create_ShouldRejectEmptyId()
        {
            // Act
            var result = Product.Create(
                Guid.Empty,
                "Produto",
                10m,
                "BRL",
                5);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_id");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldRejectInvalidName(string? name)
        {
            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                name!,
                10m,
                "BRL",
                5);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_name");
        }

        [Fact]
        public void Create_ShouldRejectNegativePrice()
        {
            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto",
                -0.01m,
                "BRL",
                5);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_price");
        }

        [Fact]
        public void Create_ShouldAllowZeroPrice()
        {
            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto gratuito",
                0m,
                "BRL",
                5);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0m, result.Value.Price);
        }

        [Fact]
        public void Create_ShouldRejectNegativeStock()
        {
            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto",
                10m,
                "BRL",
                -1);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_stock");
        }

        [Fact]
        public void Create_ShouldAllowZeroStock()
        {
            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto sem estoque",
                10m,
                "BRL",
                0);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value.StockQuantity);
        }

        [Fact]
        public void Create_ShouldAccumulateValidationErrors()
        {
            // Act
            var result = Product.Create(
                Guid.Empty,
                "",
                -1m,
                "BRL",
                -1);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(4, result.Errors.Count);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_id");
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_name");
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_price");
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_stock");
        }


        [Fact]
        public void DecreaseStock_ShouldReduceStock_WhenQuantityIsAvailable()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 10);

            // Act
            var result = product.DecreaseStock(4);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);
            Assert.Equal(6, product.StockQuantity);
        }

        [Fact]
        public void DecreaseStock_ShouldAllowRemovingAllAvailableStock()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 10);

            // Act
            var result = product.DecreaseStock(10);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0, product.StockQuantity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void DecreaseStock_ShouldRejectNonPositiveQuantity(int quantity)
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 10);

            // Act
            var result = product.DecreaseStock(quantity);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_stock_quantity");
            Assert.Equal(10, product.StockQuantity);
        }

        [Fact]
        public void DecreaseStock_ShouldRejectInsufficientStock()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 3);

            // Act
            var result = product.DecreaseStock(4);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.insufficient_stock");
            Assert.Equal(3, product.StockQuantity);
        }

        [Fact]
        public void IncreaseStock_ShouldAddQuantity()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 10);

            // Act
            var result = product.IncreaseStock(5);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Errors);
            Assert.Equal(15, product.StockQuantity);
        }

        [Fact]
        public void IncreaseStock_ShouldAllowReplenishingEmptyStock()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 0);

            // Act
            var result = product.IncreaseStock(5);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(5, product.StockQuantity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void IncreaseStock_ShouldRejectNonPositiveQuantity(int quantity)
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 10);

            // Act
            var result = product.IncreaseStock(quantity);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.invalid_stock_quantity");
            Assert.Equal(10, product.StockQuantity);
        }

        [Fact]
        public void IncreaseStock_ShouldRejectOverflow()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: int.MaxValue);

            // Act
            var result = product.IncreaseStock(1);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors,
                error => error.Code == "product.stock_overflow");
            Assert.Equal(int.MaxValue, product.StockQuantity);
        }

        [Fact]
        public void IncreaseStock_ShouldAllowReachingMaximumStock()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: int.MaxValue - 1);

            // Act
            var result = product.IncreaseStock(1);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(int.MaxValue, product.StockQuantity);
        }

        [Fact]
        public void StockMovements_ShouldPreserveQuantityAcrossDecreaseAndIncrease()
        {
            // Arrange
            var product = CreateProduct(stockQuantity: 10);

            // Act
            var decreaseResult = product.DecreaseStock(4);
            var increaseResult = product.IncreaseStock(4);

            // Assert
            Assert.True(decreaseResult.IsSuccess);
            Assert.True(increaseResult.IsSuccess);
            Assert.Equal(10, product.StockQuantity);
        }

        [Theory]
        [InlineData("25.555")]
        [InlineData("0.001")]
        [InlineData("100.999")]
        public void Create_ShouldRejectPriceWithMoreThanTwoSignificantDecimalPlaces(
            string priceText)
        {
            // Arrange
            var price = decimal.Parse(
                priceText,
                System.Globalization.CultureInfo.InvariantCulture);

            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto de teste",
                price,
                "BRL",
                10);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(
                result.Errors,
                error => error.Code == "product.invalid_price_precision");
        }

        [Fact]
        public void Create_ShouldRejectPriceAboveDatabaseLimit()
        {
            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto de teste",
                10_000_000_000_000_000m,
                "BRL",
                10);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(
                result.Errors,
                error => error.Code == "product.price_out_of_range");
        }

        [Theory]
        [InlineData("25")]
        [InlineData("25.50")]
        [InlineData("25.500")]
        [InlineData("9999999999999999.99")]
        public void Create_ShouldAcceptPriceCompatibleWithDatabase(string priceText)
        {
            // Arrange
            var price = decimal.Parse(
                priceText,
                System.Globalization.CultureInfo.InvariantCulture);

            // Act
            var result = Product.Create(
                Guid.NewGuid(),
                "Produto de teste",
                price,
                "BRL",
                10);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(price, result.Value.Price);
        }

        #region [ Métodos auxiliares ]

        /// <summary>
        /// Cria um produto válido com a quantidade de estoque informada.
        /// </summary>
        /// <param name="stockQuantity">Quantidade inicial disponível.</param>
        /// <returns>Produto válido para utilização nos testes.</returns>
        private static Product CreateProduct(int stockQuantity)
        {
            return Product.Create(
                Guid.NewGuid(),
                "Produto de teste",
                10m,
                "BRL",
                stockQuantity).Value;
        }

        #endregion [ Métodos auxiliares ]
    }
}
