using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Features.Auth.GenerateToken.DTOs;
using OrderService.Application.Auth.Operations.GenerateToken.Commands;
using OrderService.Application.Auth.Operations.GenerateToken.Handlers;

namespace OrderService.Api.Features.Auth.Controllers
{
    /// <summary>
    /// Disponibiliza os endpoints de autenticação da API.
    /// </summary>
    [ApiController]
    [Route("auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly GenerateTokenHandler _handler;

        /// <summary>
        /// Inicializa o controller de autenticação.
        /// </summary>
        /// <param name="handler">
        /// Manipulador da operação de geração de token.
        /// </param>
        public AuthController(GenerateTokenHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);

            _handler = handler;
        }

        /// <summary>
        /// Autentica um usuário e emite um token de acesso JWT.
        /// </summary>
        /// <param name="request">
        /// Credenciais informadas pelo usuário.
        /// </param>
        /// <returns>
        /// Token de acesso quando a autenticação é bem-sucedida.
        /// </returns>
        /// <response code="200">
        /// Autenticação realizada com sucesso.
        /// </response>
        /// <response code="400">
        /// Dados da requisição inválidos.
        /// </response>
        /// <response code="401">
        /// Credenciais inválidas.
        /// </response>
        [HttpPost("token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<TokenResponse> GenerateToken(
            [FromBody] TokenRequest request)
        {
            var command = new GenerateTokenCommand
            {
                Username = request.Username,
                Password = request.Password
            };

            var result = _handler.Handle(command);

            if (result is null)
            {
                return Unauthorized();
            }

            return Ok(new TokenResponse
            {
                AccessToken = result.AccessToken,
                ExpiresIn = result.ExpiresIn
            });
        }
    }
}
