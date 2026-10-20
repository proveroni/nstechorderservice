using OrderService.CrossCutting.Results;

namespace OrderService.CrossCutting.UnitTests.Results
{
    /// <summary>
    /// Testes unitários do resultado genérico.
    /// </summary>
    public sealed class ResultTTests
    {
        [Fact]
        public void Success_ShouldReturnSuccessfulResultWithValue()
        {
            // Arrange
            const int expectedValue = 42;

            // Act
            var result = Result<int>.Success(expectedValue);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Empty(result.Errors);
            Assert.Equal(expectedValue, result.Value);
        }

        [Fact]
        public void Success_ShouldRejectNullValue()
        {
            // Act
            var action = () => Result<string>.Success(null!);

            // Assert
            Assert.Throws<ArgumentNullException>(action);
        }

        [Fact]
        public void Failure_ShouldReturnFailedResultWithError()
        {
            // Arrange
            var error = new Error(
                "test.error",
                "Erro de teste.");

            // Act
            var result = Result<int>.Failure(error);

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
            var result = Result<int>.Failure(errors);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(errors, result.Errors);
        }

        [Fact]
        public void Failure_ShouldRejectEmptyErrorCollection()
        {
            // Act
            var action = () =>
                Result<int>.Failure(Array.Empty<Error>());

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void Failure_ShouldRejectNullError()
        {
            // Act
            var action = () =>
                Result<int>.Failure((Error)null!);

            // Assert
            Assert.Throws<ArgumentNullException>(action);
        }

        [Fact]
        public void Value_ShouldThrowWhenResultIsFailure()
        {
            // Arrange
            var result = Result<int>.Failure(
                new Error("test.error", "Erro de teste."));

            // Act
            Action action = () =>
            {
                _ = result.Value;
            };

            // Assert
            Assert.Throws<InvalidOperationException>(action);
        }

        [Fact]
        public void Failure_ShouldNotBeAffectedByChangesToSourceCollection()
        {
            // Arrange
            var errors = new List<Error>
        {
            new("test.error1", "Primeiro erro.")
        };

            // Act
            var result = Result<int>.Failure(errors);

            errors.Add(new Error("test.error2", "Segundo erro."));

            // Assert
            Assert.Single(result.Errors);
            Assert.Equal("test.error1", result.Errors[0].Code);
        }
    }
}
