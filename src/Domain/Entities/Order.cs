using OrderService.CrossCutting.Results;
using OrderService.Domain.Entities.Base;
using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities
{
    /// <summary>
    /// Representa um pedido e controla suas regras de negócio.
    /// </summary>
    public sealed class Order : AuditableEntityBase
    {
        private readonly List<OrderItem> _items = new();

        /// <summary>
        /// Identificador único do pedido.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Identificador do cliente responsável pelo pedido.
        /// </summary>
        public Guid CustomerId { get; }

        /// <summary>
        /// Código ISO 4217 da moeda utilizada no pedido.
        /// </summary>
        public string Currency { get; private set; } = string.Empty;

        /// <summary>
        /// Estado atual do pedido.
        /// </summary>
        public OrderStatusEnum Status { get; private set; }

        /// <summary>
        /// Data e hora de confirmação do pedido.
        /// </summary>
        public DateTimeOffset? ConfirmedAt { get; private set; }

        /// <summary>
        /// Identificador do usuário que confirmou o pedido.
        /// </summary>
        public string? ConfirmedBy { get; private set; }

        /// <summary>
        /// Data e hora de cancelamento do pedido.
        /// </summary>
        public DateTimeOffset? CanceledAt { get; private set; }

        /// <summary>
        /// Identificador do usuário que cancelou o pedido.
        /// </summary>
        public string? CanceledBy { get; private set; }

        /// <summary>
        /// Itens associados ao pedido.
        /// </summary>
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

        /// <summary>
        /// Valor total do pedido.
        /// </summary>
        public decimal Total => _items.Sum(item => item.Total);

        /// <summary>
        /// Construtor utilizado exclusivamente para materialização
        /// da entidade pelo Entity Framework Core.
        /// </summary>
        private Order()
        {
        }

        /// <summary>
        /// Inicializa um pedido previamente validado.
        /// </summary>
        private Order(
            Guid id,
            Guid customerId,
            string currency,
            DateTimeOffset createdAt,
            string createdBy,
            List<OrderItem> items)
        {
            Id = id;
            CustomerId = customerId;
            Currency = currency;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            Status = OrderStatusEnum.Placed;
            _items = items;
        }

        /// <summary>
        /// Cria um pedido com seus itens.
        /// </summary>
        /// <param name="customerId">Identificador do cliente.</param>
        /// <param name="currency">Código ISO 4217 da moeda do pedido.</param>
        /// <param name="items">Itens previamente validados.</param>
        /// <param name="createdAt">Instante de criação do pedido.</param>
        /// <param name="createdBy">Identificador do usuário responsável pela criação.</param>
        /// <returns>
        /// Resultado contendo o pedido criado ou os erros de validação.
        /// </returns>
        public static Result<Order> Create(
            Guid customerId,
            string currency,
            IEnumerable<OrderItem> items,
            DateTimeOffset createdAt,
            string createdBy)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(currency) ||
                currency.Length != 3 ||
                !currency.All(character =>
                    character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            {
                errors.Add(new Error(
                    "order.invalid_currency",
                    "A moeda deve ser informada por um código de três letras."));
            }

            if (customerId == Guid.Empty)
            {
                errors.Add(new Error(
                    "order.invalid_customer",
                    "O identificador do cliente é obrigatório."));
            }

            if (string.IsNullOrWhiteSpace(createdBy))
            {
                errors.Add(new Error(
                    "order.invalid_created_by",
                    "O identificador do usuário criador é obrigatório."));
            }

            if (items is null)
            {
                errors.Add(new Error(
                    "order.invalid_items",
                    "A coleção de itens é obrigatória."));

                return Result<Order>.Failure(errors);
            }

            var itemList = items.ToList();

            if (itemList.Count == 0)
            {
                errors.Add(new Error(
                    "order.empty_items",
                    "O pedido deve conter pelo menos um item."));
            }

            if (itemList.Any(item => item is null))
            {
                errors.Add(new Error(
                    "order.invalid_item",
                    "O pedido não pode conter itens nulos."));
            }

            if (errors.Count > 0)
            {
                return Result<Order>.Failure(errors);
            }

            decimal total;

            try
            {
                total = itemList.Sum(item => item.Total);
            }
            catch (OverflowException)
            {
                return Result<Order>.Failure(new Error(
                    "order.total_overflow",
                    "O valor total do pedido excede o limite permitido."));
            }

            _ = total;

            var order = new Order(
                Guid.NewGuid(),
                customerId,
                currency.ToUpperInvariant(),
                createdAt,
                createdBy,
                itemList);

            return Result<Order>.Success(order);
        }

        /// <summary>
        /// Confirma o pedido, quando seu estado permite a operação.
        /// </summary>
        /// <param name="confirmedAt">Instante da confirmação.</param>
        /// <param name="confirmedBy">Identificador do usuário responsável.</param>
        /// <returns>
        /// Resultado da operação, incluindo eventuais erros de negócio.
        /// </returns>
        public Result Confirm(DateTimeOffset confirmedAt, string confirmedBy)
        {
            if (Status == OrderStatusEnum.Confirmed)
            {
                return Result.Success();
            }

            if (Status != OrderStatusEnum.Placed)
            {
                return Result.Failure(new Error(
                    "order.cannot_confirm",
                    "O pedido não pode ser confirmado no estado atual."));
            }

            if (string.IsNullOrWhiteSpace(confirmedBy))
            {
                return Result.Failure(new Error(
                    "order.invalid_confirmed_by",
                    "O identificador do usuário confirmador é obrigatório."));
            }

            Status = OrderStatusEnum.Confirmed;
            ConfirmedAt = confirmedAt;
            ConfirmedBy = confirmedBy;

            UpdatedAt = confirmedAt;
            UpdatedBy = confirmedBy;

            return Result.Success();
        }

        /// <summary>
        /// Cancela o pedido, quando seu estado permite a operação.
        /// </summary>
        /// <param name="canceledAt">Instante do cancelamento.</param>
        /// <param name="canceledBy">Identificador do usuário responsável.</param>
        /// <returns>
        /// Resultado da operação, incluindo eventuais erros de negócio.
        /// </returns>
        public Result Cancel(DateTimeOffset canceledAt, string canceledBy)
        {
            if (Status == OrderStatusEnum.Canceled)
            {
                return Result.Success();
            }

            if (Status != OrderStatusEnum.Placed &&
                Status != OrderStatusEnum.Confirmed)
            {
                return Result.Failure(new Error(
                    "order.cannot_cancel",
                    "O pedido não pode ser cancelado no estado atual."));
            }

            if (string.IsNullOrWhiteSpace(canceledBy))
            {
                return Result.Failure(new Error(
                    "order.invalid_canceled_by",
                    "O identificador do usuário cancelador é obrigatório."));
            }

            Status = OrderStatusEnum.Canceled;
            CanceledAt = canceledAt;
            CanceledBy = canceledBy;

            UpdatedAt = canceledAt;
            UpdatedBy = canceledBy;

            return Result.Success();
        }
    }
}
