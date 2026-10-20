namespace OrderService.Application.Abstractions.Authentication
{
    /// <summary>
    /// Representa um token de autenticação gerado
    /// e suas informações de validade.
    /// </summary>
    public sealed class GeneratedToken
    {
        /// <summary>
        /// Token de acesso gerado.
        /// </summary>
        public required string AccessToken { get; init; }

        /// <summary>
        /// Tempo de validade do token, em segundos.
        /// </summary>
        public required int ExpiresIn { get; init; }
    }
}
