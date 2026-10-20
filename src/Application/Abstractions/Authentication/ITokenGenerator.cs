namespace OrderService.Application.Abstractions.Authentication
{
    /// <summary>
    /// Define o contrato para geração de tokens de autenticação.
    /// </summary>
    public interface ITokenGenerator
    {
        /// <summary>
        /// Gera um token de autenticação para o usuário informado.
        /// </summary>
        /// <param name="userId">
        /// Identificador do usuário autenticado.
        /// </param>
        /// <returns>
        /// Token gerado e suas informações de validade.
        /// </returns>
        GeneratedToken Generate(string userId);
    }
}
