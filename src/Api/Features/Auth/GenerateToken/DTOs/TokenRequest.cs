using System.ComponentModel.DataAnnotations;

namespace OrderService.Api.Features.Auth.GenerateToken.DTOs
{
    /// <summary>
    /// Representa os dados necessários para solicitar
    /// um token de autenticação.
    /// </summary>
    public sealed class TokenRequest
    {
        /// <summary>
        /// Nome do usuário que solicita a autenticação.
        /// </summary>
        [Required]
        public required string Username { get; init; }

        /// <summary>
        /// Senha do usuário que solicita a autenticação.
        /// </summary>
        [Required]
        public required string Password { get; init; }
    }
}
