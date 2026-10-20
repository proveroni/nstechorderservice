
using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Operations.ConfirmOrder.Commands;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Enums;

namespace OrderService.Application.Orders.Operations.ConfirmOrder.Handlers
{
    /// <summary>
    /// Coordena o caso de uso de confirmação de pedidos.
    /// </summary>
    public sealed class ConfirmOrderHandler
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly TimeProvider _timeProvider;
        private readonly ICurrentUser _currentUser;

        /// <summary>
        /// Inicializa o caso de uso de confirmação de pedidos.
        /// </summary>
        /// <param name="orderRepository">Repositório de pedidos.</param>
        /// <param name="productRepository">Repositório de produtos.</param>
        /// <param name="transactionManager">Gerenciador de transações.</param>
        /// <param name="timeProvider">Provedor de data e hora.</param>
        /// <param name="currentUser">Serviço de identificação do usuário autenticado.</param>
        public ConfirmOrderHandler(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            ITransactionManager transactionManager,
            TimeProvider timeProvider,
            ICurrentUser currentUser)
        {
            ArgumentNullException.ThrowIfNull(orderRepository);
            ArgumentNullException.ThrowIfNull(productRepository);
            ArgumentNullException.ThrowIfNull(transactionManager);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(currentUser);

            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _transactionManager = transactionManager;
            _timeProvider = timeProvider;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Processa uma solicitação de confirmação de pedido.
        /// </summary>
        /// <param name="command">Dados da solicitação.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação assíncrona.
        /// </param>
        /// <returns>Resultado da operação de confirmação.</returns>
        public async Task<Result> HandleAsync(
            ConfirmOrderCommand command,
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

            var confirmedBy = _currentUser.UserId!;

            if (command.OrderId == Guid.Empty)
            {
                return Result.Failure(
                    new Error(
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
                        return Result.Failure(
                            new Error(
                                "order.not_found",
                                "Pedido não encontrado."));
                    }

                    if (order.Status == OrderStatusEnum.Confirmed)
                    {
                        return Result.Success();
                    }

                    if (order.Status == OrderStatusEnum.Canceled)
                    {
                        return Result.Failure(
                            new Error(
                                "order.already_canceled",
                                "Um pedido cancelado não pode ser confirmado."));
                    }

                    foreach (var item in order.Items)
                    {
                        var stockDecreased = await _productRepository.TryDecreaseStockAsync(
                            item.ProductId,
                            item.Quantity,
                            transactionCancellationToken);

                        if (!stockDecreased)
                        {
                            return Result.Failure(
                                new Error(
                                    "order.insufficient_stock",
                                    "Estoque insuficiente para confirmar o pedido."));
                        }
                    }

                    return order.Confirm(_timeProvider.GetUtcNow(), confirmedBy);
                },
                cancellationToken);
        }
    }
}
