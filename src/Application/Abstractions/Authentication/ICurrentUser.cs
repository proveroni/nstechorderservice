namespace OrderService.Application.Abstractions.Authentication
{
    /// <summary>
    /// Disponibiliza informações sobre o usuário
    /// autenticado no contexto da operação atual.
    /// </summary>
    public interface ICurrentUser
    {
        /// <summary>
        /// Obtém o identificador do usuário autenticado.
        /// Retorna nulo quando não existe uma identidade
        /// autenticada ou um identificador válido.
        /// </summary>
        string? UserId { get; }

        /// <summary>
        /// Indica se existe um usuário autenticado.
        /// </summary>
        bool IsAuthenticated { get; }
    }
}
