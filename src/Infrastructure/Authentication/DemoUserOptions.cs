namespace OrderService.Infrastructure.Authentication
{
    /// <summary>
    /// Representa as configurações do usuário de demonstração
    /// utilizado na autenticação da API.
    /// </summary>
    public sealed class DemoUserOptions
    {
        /// <summary>
        /// Nome da seção de configuração.
        /// </summary>
        public const string SectionName = "DemoUser";

        /// <summary>
        /// Identificador do usuário.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Nome utilizado na autenticação.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Hash da senha do usuário.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;
    }
}
