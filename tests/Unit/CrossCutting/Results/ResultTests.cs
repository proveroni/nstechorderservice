using OrderService.CrossCutting.Results;

namespace OrderService.CrossCutting.UnitTests.Results
{
    /// <summary>
    /// Testes unitários do resultado não genérico.
    /// </summary>
    public sealed class ResultTests
    {
        [Fact]
        public void Success_ShouldReturnSuccessfulResultWithoutErrors()
        {
            // Act
            var result = Result.Success();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Failure_ShouldReturnFailedResultWithError()
        {
            // Arrange
            var error = new Error(
                "test.error",
                "Erro de teste.");

            // Act
            var result = Result.Failure(error);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.Single(result.Errors);
            Assert.Equal(error, result.Errors[0]);
        }

        [Fact]
        public void Failure_ShouldPreserveMultipleErrors()
        {
            // Arrange
            var errors = new[]
            {
            new Error("test.error1", "Primeiro erro."),
            new Error("test.error2", "Segundo erro.")
        };

            // Act
            var result = Result.Failure(errors);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(2, result.Errors.Count);
            Assert.Equal(errors, result.Errors);
        }

        [Fact]
        public void Failure_ShouldRejectEmptyErrorCollection()
        {
            // Act
            var action = () => Result.Failure(Array.Empty<Error>());

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void Failure_ShouldRejectNullError()
        {
            // Act
            var action = () => Result.Failure((Error)null!);

            // Assert
            Assert.Throws<ArgumentNullException>(action);
        }
    }
}
