using OrderService.CrossCutting.Results;

namespace OrderService.CrossCutting.UnitTests.Results
{
    /// <summary>
    /// Testes unitários das invariantes e da igualdade de Error.
    /// </summary>
    public sealed class ErrorTests
    {
        [Fact]
        public void Constructor_ShouldCreateErrorWithValidValues()
        {
            // Arrange
            const string code = "order.not_found";
            const string message = "Pedido não encontrado.";

            // Act
            var error = new Error(code, message);

            // Assert
            Assert.Equal(code, error.Code);
            Assert.Equal(message, error.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_ShouldRejectEmptyOrWhitespaceCode(string code)
        {
            // Act
            Action action = () =>
            {
                _ = new Error(code, "Mensagem válida.");
            };

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void Constructor_ShouldRejectNullCode()
        {
            // Act
            Action action = () =>
            {
                _ = new Error(null!, "Mensagem válida.");
            };

            // Assert
            Assert.Throws<ArgumentNullException>(action);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_ShouldRejectEmptyOrWhitespaceMessage(string message)
        {
            // Act
            Action action = () =>
            {
                _ = new Error("test.error", message);
            };

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void Constructor_ShouldRejectNullMessage()
        {
            // Act
            Action action = () =>
            {
                _ = new Error("test.error", null!);
            };

            // Assert
            Assert.Throws<ArgumentNullException>(action);
        }

        [Fact]
        public void ErrorsWithSameValues_ShouldBeEqual()
        {
            // Arrange
            var first = new Error("test.error", "Mensagem.");
            var second = new Error("test.error", "Mensagem.");

            // Assert
            Assert.Equal(first, second);
        }

        [Fact]
        public void ErrorsWithDifferentCodes_ShouldNotBeEqual()
        {
            // Arrange
            var first = new Error("test.error1", "Mensagem.");
            var second = new Error("test.error2", "Mensagem.");

            // Assert
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void ErrorsWithDifferentMessages_ShouldNotBeEqual()
        {
            // Arrange
            var first = new Error("test.error", "Primeira mensagem.");
            var second = new Error("test.error", "Segunda mensagem.");

            // Assert
            Assert.NotEqual(first, second);
        }
    }
}
