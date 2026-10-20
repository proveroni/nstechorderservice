namespace OrderService.Application.Auth.Operations.GenerateToken.Commands
{
    /// <summary>
    /// Representa os dados necessários para executar
    /// a operação de autenticação e geração de token.
    /// </summary>
    public sealed class GenerateTokenCommand
    {
        /// <summary>
        /// Nome do usuário que solicita a autenticação.
        /// </summary>
        public required string Username { get; init; }

        /// <summary>
        /// Senha informada pelo usuário.
        /// </summary>
        public required string Password { get; init; }
    }
}
