namespace OrderService.CrossCutting.Results
{
    /// <summary>
    /// Representa o resultado de uma operação sem valor de retorno.
    /// </summary>
    public sealed class Result
    {
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
        /// Inicializa um resultado, garantindo a consistência
        /// entre o estado da operação e os erros informados.
        /// </summary>
        private Result(bool isSuccess, IEnumerable<Error> errors)
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

            IsSuccess = isSuccess;
            _errors = Array.AsReadOnly(errorList);
        }

        /// <summary>
        /// Cria um resultado bem-sucedido.
        /// </summary>
        public static Result Success()
        {
            return new Result(true, Array.Empty<Error>());
        }

        /// <summary>
        /// Cria um resultado de falha contendo um erro.
        /// </summary>
        public static Result Failure(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);

            return new Result(false, new[] { error });
        }

        /// <summary>
        /// Cria um resultado de falha contendo múltiplos erros.
        /// </summary>
        public static Result Failure(IEnumerable<Error> errors)
        {
            return new Result(false, errors);
        }
    }
}
