using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OrderService.Application.Abstractions.Authentication;

namespace OrderService.Infrastructure.Authentication
{
    /// <summary>
    /// Implementa a validação das credenciais do usuário
    /// de demonstração configurado na aplicação.
    /// </summary>
    public sealed class DemoUserCredentialValidator : ICredentialValidator
    {
        private readonly DemoUserOptions _options;
        private readonly IPasswordHasher<DemoUserOptions> _passwordHasher;

        /// <summary>
        /// Inicializa o validador de credenciais.
        /// </summary>
        /// <param name="options">
        /// Configurações do usuário de demonstração.
        /// </param>
        /// <param name="passwordHasher">
        /// Serviço responsável pela verificação da senha.
        /// </param>
        public DemoUserCredentialValidator(
            IOptions<DemoUserOptions> options,
            IPasswordHasher<DemoUserOptions> passwordHasher)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(passwordHasher);

            _options = options.Value;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Valida o nome de usuário e a senha informados utilizando
        /// as credenciais do usuário de demonstração configurado.
        /// </summary>
        /// <param name="username">
        /// Nome de usuário que será comparado com o valor configurado.
        /// </param>
        /// <param name="password">
        /// Senha em texto claro que será verificada contra o hash armazenado.
        /// </param>
        /// <returns>
        /// Identificador do usuário de demonstração quando as credenciais
        /// são válidas; caso contrário, <see langword="null"/>.
        /// </returns>
        public string? Validate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                return null;
            }

            if (!string.Equals(
                    username,
                    _options.Username,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var result = _passwordHasher.VerifyHashedPassword(
                _options,
                _options.PasswordHash,
                password);

            if (result == PasswordVerificationResult.Failed)
            {
                return null;
            }

            return _options.UserId;
        }
    }
}
