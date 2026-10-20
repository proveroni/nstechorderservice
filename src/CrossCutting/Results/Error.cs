namespace OrderService.CrossCutting.Results
{
    /// <summary>
    /// Representa um erro identificado durante uma operação.
    /// </summary>
    public sealed record Error
    {
        /// <summary>
        /// Código estável que identifica o tipo do erro.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Descrição legível do erro.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Inicializa um erro com código e mensagem obrigatórios.
        /// </summary>
        /// <param name="code">Código que identifica o erro.</param>
        /// <param name="message">Descrição legível do erro.</param>
        /// <exception cref="ArgumentException">
        /// Quando o código ou a mensagem é nulo, vazio ou contém somente espaços.
        /// </exception>
        public Error(string code, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            Code = code;
            Message = message;
        }
    }
}
