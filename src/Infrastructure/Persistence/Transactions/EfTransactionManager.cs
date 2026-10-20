using Microsoft.Extensions.Logging;
using OrderService.Application.Abstractions.Persistence;
using OrderService.CrossCutting.Results;
using OrderService.Infrastructure.Persistence.Contexts;

namespace OrderService.Infrastructure.Persistence.Transactions
{
    /// <summary>
    /// Gerencia transações do Entity Framework Core,
    /// garantindo a atomicidade das operações de persistência.
    /// </summary>
    public sealed class EfTransactionManager : ITransactionManager
    {
        private readonly OrderServiceDbContext _context;
        private readonly ILogger<EfTransactionManager> _logger;

        /// <summary>
        /// Inicializa o gerenciador de transações.
        /// </summary>
        /// <param name="context">Contexto de persistência.</param>
        /// <param name="logger">Serviço de registro de eventos.</param>
        public EfTransactionManager
            (
                OrderServiceDbContext context,
                ILogger<EfTransactionManager> logger
            )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _context = context;
            _logger = logger;
        }


        public Task<Result> ExecuteAsync
            (
                Func<CancellationToken, Task<Result>> operation,
                CancellationToken cancellationToken = default
            )
        {
            return ExecuteInternalAsync
                (
                    operation,
                    result => result.IsSuccess,
                    cancellationToken
                );
        }

        public Task<Result<T>> ExecuteAsync<T>
            (
                Func<CancellationToken, Task<Result<T>>> operation,
                CancellationToken cancellationToken = default
            )
        {
            return ExecuteInternalAsync
                (
                    operation,
                    result => result.IsSuccess,
                    cancellationToken
                );
        }

        /// <summary>
        /// Executa uma operação dentro de uma transação,
        /// confirmando ou revertendo as alterações conforme o resultado.
        /// </summary>
        private async Task<TResult> ExecuteInternalAsync<TResult>
            (
                Func<CancellationToken, Task<TResult>> operation,
                Func<TResult, bool> isSuccess,
                CancellationToken cancellationToken
            )
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            TResult result;

            try
            {
                result = await operation(cancellationToken);

                if (isSuccess(result))
                {
                    await _context.SaveChangesAsync(cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(rollbackException, "Falha ao executar rollback da transação do banco de dados.");
                }

                throw;
            }

            if (!isSuccess(result))
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            return result;
        }
    }
}
