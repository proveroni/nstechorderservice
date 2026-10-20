using OrderService.CrossCutting.Results;

namespace OrderService.Application.Abstractions.Persistence
{
    /// <summary>
    /// Define a execução de operações de aplicação
    /// dentro de uma transação de persistência.
    /// </summary>
    public interface ITransactionManager
    {
        /// <summary>
        /// Executa uma operação transacional que retorna um resultado de negócio.
        /// </summary>
        /// <param name="operation">
        /// Operação executada dentro da transação.
        /// </param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação.
        /// </param>
        /// <returns>
        /// Resultado da operação executada.
        /// </returns>
        Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executa uma operação transacional que retorna
        /// um resultado de negócio com valor.
        /// </summary>
        /// <typeparam name="T">
        /// Tipo do valor retornado em caso de sucesso.
        /// </typeparam>
        /// <param name="operation">
        /// Operação executada dentro da transação.
        /// </param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação.
        /// </param>
        /// <returns>
        /// Resultado da operação executada.
        /// </returns>
        Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default);

    }
}
