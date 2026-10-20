namespace OrderService.Application.Abstractions.Authentication
{
    /// <summary>
    /// Define o contrato para validação de credenciais
    /// de autenticação.
    /// </summary>
    public interface ICredentialValidator
    {
        /// <summary>
        /// Valida as credenciais informadas.
        /// </summary>
        /// <param name="username">Nome do usuário.</param>
        /// <param name="password">Senha informada.</param>
        /// <returns>
        /// Identificador do usuário autenticado ou null
        /// quando as credenciais forem inválidas.
        /// </returns>
        string? Validate(string username, string password);
    }
}