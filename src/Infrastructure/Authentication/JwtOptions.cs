namespace OrderService.Infrastructure.Authentication
{
    /// <summary>
    /// Representa as configurações utilizadas na
    /// geração e validação dos tokens JWT.
    /// </summary>
    public sealed class JwtOptions
    {
        /// <summary>
        /// Nome da seção de configuração.
        /// </summary>
        public const string SectionName = "Jwt";

        /// <summary>
        /// Identificador do emissor do token.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// Identificador da audiência autorizada.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Chave de assinatura codificada em Base64.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// Tempo de validade do token, em minutos.
        /// </summary>
        public int ExpirationMinutes { get; set; } = 60;
    }
}
