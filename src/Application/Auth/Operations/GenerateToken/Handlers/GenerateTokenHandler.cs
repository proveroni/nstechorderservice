using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Auth.Operations.GenerateToken.Commands;

namespace OrderService.Application.Auth.Operations.GenerateToken.Handlers
{
    /// <summary>
    /// Executa a operação de autenticação do usuário
    /// e geração do token de acesso.
    /// </summary>
    public sealed class GenerateTokenHandler
    {
        private readonly ICredentialValidator _credentialValidator;
        private readonly ITokenGenerator _tokenGenerator;

        /// <summary>
        /// Inicializa o manipulador da operação.
        /// </summary>
        /// <param name="credentialValidator">
        /// Serviço responsável pela validação das credenciais.
        /// </param>
        /// <param name="tokenGenerator">
        /// Serviço responsável pela geração do token.
        /// </param>
        public GenerateTokenHandler(
            ICredentialValidator credentialValidator,
            ITokenGenerator tokenGenerator)
        {
            ArgumentNullException.ThrowIfNull(credentialValidator);
            ArgumentNullException.ThrowIfNull(tokenGenerator);

            _credentialValidator = credentialValidator;
            _tokenGenerator = tokenGenerator;
        }

        /// <summary>
        /// Valida as credenciais e gera um token de acesso
        /// quando a autenticação é bem-sucedida.
        /// </summary>
        /// <param name="command">
        /// Dados necessários para a autenticação.
        /// </param>
        /// <returns>
        /// Token gerado quando as credenciais forem válidas;
        /// caso contrário, null.
        /// </returns>
        public GeneratedToken? Handle(GenerateTokenCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var userId = _credentialValidator.Validate(
                command.Username,
                command.Password);

            if (userId is null)
            {
                return null;
            }

            return _tokenGenerator.Generate(userId);
        }
    }
}
