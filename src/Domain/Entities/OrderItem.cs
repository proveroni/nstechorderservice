using OrderService.CrossCutting.Results;

namespace OrderService.Domain.Entities
{
    /// <summary>
    /// Representa um item de pedido com preço unitário
    /// registrado no momento de sua criação.
    /// </summary>
    public sealed class OrderItem
    {
        /// <summary>
        /// Identificador do produto associado ao item.
        /// </summary>
        public Guid ProductId { get; }

        /// <summary>
        /// Quantidade solicitada do produto.
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// Preço unitário registrado para o item.
        /// </summary>
        public decimal UnitPrice { get; }

        /// <summary>
        /// Valor total do item.
        /// </summary>
        public decimal Total => UnitPrice * Quantity;

        /// <summary>
        /// Inicializa um item de pedido previamente validado.
        /// </summary>
        private OrderItem(Guid productId, int quantity, decimal unitPrice)
        {
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }

        /// <summary>
        /// Cria um item de pedido após validar seus dados.
        /// </summary>
        /// <param name="productId">Identificador do produto.</param>
        /// <param name="quantity">Quantidade solicitada.</param>
        /// <param name="unitPrice">Preço unitário do produto.</param>
        /// <returns>
        /// Resultado contendo o item criado ou os erros de validação.
        /// </returns>
        public static Result<OrderItem> Create(Guid productId, int quantity, decimal unitPrice)
        {
            var errors = new List<Error>();

            if (productId == Guid.Empty)
            {
                errors.Add(new Error(
                    "order_item.invalid_product",
                    "O identificador do produto é obrigatório."));
            }

            if (quantity <= 0)
            {
                errors.Add(new Error(
                    "order_item.invalid_quantity",
                    "A quantidade deve ser maior que zero."));
            }

            if (unitPrice < 0)
            {
                errors.Add(new Error(
                    "order_item.invalid_unit_price",
                    "O preço unitário não pode ser negativo."));
            }
            else if (unitPrice > 9_999_999_999_999_999.99m)
            {
                errors.Add(new Error(
                    "order_item.unit_price_out_of_range",
                    "O preço unitário excede o limite permitido."));
            }
            else if (unitPrice != decimal.Truncate(unitPrice * 100m) / 100m)
            {
                errors.Add(new Error(
                    "order_item.invalid_unit_price_precision",
                    "O preço unitário deve possuir no máximo duas casas decimais."));
            }

            if (quantity > 0 && unitPrice >= 0)
            {
                try
                {
                    _ = checked(unitPrice * quantity);
                }
                catch (OverflowException)
                {
                    errors.Add(new Error(
                        "order_item.total_overflow",
                        "O valor total do item excede o limite permitido."));
                }
            }

            if (errors.Count > 0)
            {
                return Result<OrderItem>.Failure(errors);
            }

            var item = new OrderItem(productId, quantity, unitPrice);

            return Result<OrderItem>.Success(item);
        }
    }
}
