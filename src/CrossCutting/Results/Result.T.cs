namespace OrderService.CrossCutting.Results
{
    /// <summary>
    /// Representa o resultado de uma operação que retorna um valor.
    /// </summary>
    /// <typeparam name="T">
    /// Tipo do valor retornado pela operação.
    /// </typeparam>
    public sealed class Result<T>
    {
        private readonly T? _value;
        private readonly IReadOnlyList<Error> _errors;

        /// <summary>
        /// Indica se a operação foi concluída com sucesso.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Indica se a operação falhou.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Erros identificados durante a operação.
        /// </summary>
        public IReadOnlyList<Error> Errors => _errors;

        /// <summary>
        /// Valor retornado quando a operação é bem-sucedida.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Quando o resultado representa uma falha.
        /// </exception>
        public T Value =>
            IsSuccess
                ? _value!
                : throw new InvalidOperationException("Não é possível acessar o valor de um resultado de falha.");

        /// <summary>
        /// Inicializa um resultado com valor ou erros.
        /// </summary>
        private Result(
            T? value,
            bool isSuccess,
            IEnumerable<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var errorList = errors.ToArray();

            if (isSuccess && errorList.Length > 0)
            {
                throw new ArgumentException("Um resultado bem-sucedido não pode conter erros.", nameof(errors));
            }

            if (!isSuccess && errorList.Length == 0)
            {
                throw new ArgumentException("Um resultado de falha deve conter pelo menos um erro.", nameof(errors));
            }

            if (isSuccess && value is null)
            {
                throw new ArgumentNullException(nameof(value), "Um resultado bem-sucedido deve conter um valor.");
            }

            IsSuccess = isSuccess;
            _value = value;
            _errors = Array.AsReadOnly(errorList);
        }

        /// <summary>
        /// Cria um resultado bem-sucedido contendo um valor.
        /// </summary>
        public static Result<T> Success(T value)
        {
            return new Result<T>(
                value,
                true,
                Array.Empty<Error>());
        }

        /// <summary>
        /// Cria um resultado de falha contendo um erro.
        /// </summary>
        public static Result<T> Failure(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);

            return new Result<T>(
                default,
                false,
                new[] { error });
        }

        /// <summary>
        /// Cria um resultado de falha contendo múltiplos erros.
        /// </summary>
        public static Result<T> Failure(IEnumerable<Error> errors)
        {
            return new Result<T>(
                default,
                false,
                errors);
        }
    }
}
