
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Operations.CancelOrder.Commands;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Enums;

namespace OrderService.Application.Orders.Operations.CancelOrder.Handlers
{
    /// <summary>
    /// Coordena o caso de uso de cancelamento de pedidos.
    /// </summary>
    public sealed class CancelOrderHandler
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly TimeProvider _timeProvider;
        private readonly IProductRepository _productRepository;
        private readonly ICurrentUser _currentUser;

        /// <summary>
        /// Inicializa o handler de cancelamento de pedidos.
        /// </summary>
        /// <param name="orderRepository">Repositório de pedidos.</param>
        /// <param name="productRepository">Repositório de produtos.</param>
        /// <param name="transactionManager">Gerenciador de transações.</param>
        /// <param name="timeProvider">Provedor de data e hora.</param>
        /// <param name="currentUser">Serviço de identificação do usuário autenticado.</param>
        public CancelOrderHandler(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            ITransactionManager transactionManager,
            TimeProvider timeProvider,
            ICurrentUser currentUser)
        {
            ArgumentNullException.ThrowIfNull(orderRepository);
            ArgumentNullException.ThrowIfNull(transactionManager);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(productRepository);
            ArgumentNullException.ThrowIfNull(currentUser);

            _orderRepository = orderRepository;
            _transactionManager = transactionManager;
            _timeProvider = timeProvider;
            _productRepository = productRepository;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Cancela um pedido, restaurando o estoque quando
        /// o pedido já tiver sido confirmado.
        /// </summary>
        /// <param name="command">Dados da solicitação de cancelamento.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação assíncrona.
        /// </param>
        /// <returns>Resultado da operação de cancelamento.</returns>
        public async Task<Result> HandleAsync(
            CancelOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            if (!_currentUser.IsAuthenticated ||
                string.IsNullOrWhiteSpace(_currentUser.UserId))
            {
                return Result.Failure(
                    new Error(
                        "auth.unauthenticated",
                        "Não foi possível identificar o usuário autenticado."));
            }

            var canceledBy = _currentUser.UserId!;

            if (command.OrderId == Guid.Empty)
            {
                return Result.Failure(new Error(
                    "order.invalid_id",
                    "O identificador do pedido é obrigatório."));
            }

            return await _transactionManager.ExecuteAsync(
                async transactionCancellationToken =>
                {
                    var order = await _orderRepository.GetByIdForUpdateAsync(
                        command.OrderId,
                        transactionCancellationToken);

                    if (order is null)
                    {
                        return Result.Failure(new Error(
                            "order.not_found",
                            "Pedido não encontrado."));
                    }

                    if (order.Status == OrderStatusEnum.Canceled)
                    {
                        return Result.Success();
                    }

                    if (order.Status == OrderStatusEnum.Confirmed)
                    {
                        foreach (var item in order.Items)
                        {
                            var increased = await _productRepository.TryIncreaseStockAsync(
                                item.ProductId,
                                item.Quantity,
                                transactionCancellationToken);

                            if (!increased)
                            {
                                return Result.Failure(new Error(
                                    "product.stock_restoration_failed",
                                    "Não foi possível devolver o estoque ao cancelar o pedido."));
                            }
                        }
                    }

                    return order.Cancel(_timeProvider.GetUtcNow(), canceledBy);
                },
                cancellationToken);
        }
    }
}
