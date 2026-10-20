using OrderService.Application.Abstractions.Authentication;
using OrderService.Application.Abstractions.Persistence;
using OrderService.Application.Orders.Operations.CreateOrder.Commands;
using OrderService.Application.Orders.Operations.CreateOrder.DTOs.Responses;
using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities;

namespace OrderService.Application.Orders.Operations.CreateOrder.Handlers
{
    /// <summary>
    /// Coordena o caso de uso de criação de pedidos.
    /// </summary>
    public sealed class CreateOrderHandler
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly TimeProvider _timeProvider;
        private readonly ICurrentUser _currentUser;

        /// <summary>
        /// Inicializa o handler com suas dependências.
        /// </summary>
        public CreateOrderHandler(
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
        /// Executa o caso de uso de criação de pedidos.
        /// </summary>
        /// <param name="request">Dados de entrada do pedido.</param>
        /// <param name="cancellationToken">
        /// Token para cancelamento da operação.
        /// </param>
        /// <returns>
        /// Resultado contendo os dados do pedido criado
        /// ou os erros de negócio encontrados.
        /// </returns>
        public async Task<Result<CreateOrderResponse>> HandleAsync(
            CreateOrderCommand request,
            CancellationToken cancellationToken = default)
        {
            var errors = new List<Error>();

            if (request is null)
            {
                return Result<CreateOrderResponse>.Failure(
                    new Error(
                        "order.invalid_request",
                        "Os dados do pedido são obrigatórios."));
            }

            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            {
                return Result<CreateOrderResponse>.Failure(
                    new Error(
                        "auth.unauthenticated",
                        "Não foi possível identificar o usuário autenticado."));
            }

            if (request.CustomerId == Guid.Empty)
            {
                errors.Add(new Error(
                    "order.invalid_customer",
                    "O identificador do cliente é obrigatório."));
            }

            if (string.IsNullOrWhiteSpace(request.Currency))
            {
                errors.Add(new Error(
                    "order.invalid_currency",
                    "A moeda do pedido é obrigatória."));
            }
            else if (request.Currency.Length != 3 ||
                     !request.Currency.All(character =>
                         character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            {
                errors.Add(new Error(
                    "order.invalid_currency",
                    "A moeda do pedido deve conter exatamente três letras."));
            }

            if (request.Items is null)
            {
                errors.Add(new Error(
                    "order.invalid_items",
                    "A coleção de itens é obrigatória."));
            }
            else
            {
                if (request.Items.Count == 0)
                {
                    errors.Add(new Error(
                        "order.empty_items",
                        "O pedido deve conter pelo menos um item."));
                }

                if (request.Items.Any(item => item is null))
                {
                    errors.Add(new Error(
                        "order.invalid_item",
                        "O pedido não pode conter itens nulos."));
                }
            }

            if (errors.Count > 0 || request.Items is null)
            {
                return Result<CreateOrderResponse>.Failure(errors);
            }

            var requestedItems = request.Items.ToList();
            var orderItems = new List<OrderItem>();

            foreach (var group in requestedItems.GroupBy(item => item.ProductId))
            {
                if (group.Key == Guid.Empty)
                {
                    errors.Add(new Error(
                        "order_item.invalid_product",
                        "O identificador do produto é obrigatório."));

                    continue;
                }

                if (group.Any(item => item.Quantity <= 0))
                {
                    errors.Add(new Error(
                        "order_item.invalid_quantity",
                        "A quantidade deve ser maior que zero."));

                    continue;
                }

                var totalQuantity = group.Sum(item => (long)item.Quantity);

                if (totalQuantity > int.MaxValue)
                {
                    errors.Add(new Error(
                        "order_item.quantity_overflow",
                        "A quantidade total solicitada excede o limite permitido."));

                    continue;
                }

                var product = await _productRepository.GetByIdAsync(group.Key, cancellationToken);

                if (product is null)
                {
                    errors.Add(new Error(
                        "order.product_not_found",
                        $"O produto {group.Key} não foi encontrado."));

                    continue;
                }

                if (!string.Equals(
                    product.Currency,
                    request.Currency,
                    StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new Error(
                        "order.currency_mismatch",
                        $"A moeda do produto {group.Key} ({product.Currency}) " +
                        $"é diferente da moeda do pedido ({request.Currency})."));

                    continue;
                }

                if (product.StockQuantity < totalQuantity)
                {
                    errors.Add(new Error(
                        "order.insufficient_stock",
                        $"O produto {group.Key} não possui estoque suficiente."));

                    continue;
                }

                var itemResult = OrderItem.Create(
                    product.Id,
                    (int)totalQuantity,
                    product.Price);

                if (itemResult.IsFailure)
                {
                    errors.AddRange(itemResult.Errors);
                    continue;
                }

                orderItems.Add(itemResult.Value);
            }

            if (errors.Count > 0)
            {
                return Result<CreateOrderResponse>.Failure(errors);
            }

            var orderResult = Order.Create(
                request.CustomerId,
                request.Currency,
                orderItems,
                _timeProvider.GetUtcNow(),
                _currentUser.UserId!);

            if (orderResult.IsFailure)
            {
                return Result<CreateOrderResponse>.Failure(orderResult.Errors);
            }

            var order = orderResult.Value;

            return await _transactionManager.ExecuteAsync<CreateOrderResponse>(
                async token =>
                {
                    await _orderRepository.AddAsync(order, token);

                    var response = new CreateOrderResponse(
                        order.Id,
                        order.CustomerId,
                        order.Currency,
                        order.Status,
                        order.Total,
                        order.CreatedAt);

                    return Result<CreateOrderResponse>.Success(response);
                },
                cancellationToken);

        }


    }
}
