using OrderService.CrossCutting.Results;

namespace OrderService.Domain.Entities
{
    /// <summary>
    /// Representa um produto disponível para inclusão em pedidos.
    /// </summary>
    public sealed class Product
    {
        /// <summary>
        /// Identificador único do produto.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Nome do produto.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Preço atual do produto.
        /// </summary>
        public decimal Price { get; }

        /// <summary>
        /// Código de três letras da moeda em que o preço do produto está expresso.
        /// </summary>
        public string Currency { get; }

        /// <summary>
        /// Quantidade atualmente disponível em estoque.
        /// </summary>
        public int StockQuantity { get; private set; }

        /// <summary>
        /// Inicializa um produto previamente validado.
        /// </summary>
        private Product(
            Guid id,
            string name,
            decimal price,
            string currency,
            int stockQuantity)
        {
            Id = id;
            Name = name;
            Price = price;
            Currency = currency;
            StockQuantity = stockQuantity;
        }

        /// <summary>
        /// Cria um produto após validar seus dados.
        /// </summary>
        /// <param name="id">Identificador do produto.</param>
        /// <param name="name">Nome do produto.</param>
        /// <param name="price">Preço atual do produto.</param>
        /// <param name="currency">Moeda em que o preço do produto está expresso.</param>
        /// <param name="stockQuantity">Quantidade disponível.</param>
        /// <returns>
        /// Resultado contendo o produto ou os erros de validação.
        /// </returns>
        public static Result<Product> Create(
            Guid id,
            string name,
            decimal price,
            string currency,
            int stockQuantity)
        {
            var errors = new List<Error>();

            if (id == Guid.Empty)
            {
                errors.Add(new Error(
                    "product.invalid_id",
                    "O identificador do produto é obrigatório."));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error(
                    "product.invalid_name",
                    "O nome do produto é obrigatório."));
            }

            if (price < 0)
            {
                errors.Add(new Error(
                    "product.invalid_price",
                    "O preço do produto não pode ser negativo."));
            }
            else if (price > 9_999_999_999_999_999.99m)
            {
                errors.Add(new Error(
                    "product.price_out_of_range",
                    "O preço do produto excede o limite permitido."));
            }
            else if (price != decimal.Truncate(price * 100m) / 100m)
            {
                errors.Add(new Error(
                    "product.invalid_price_precision",
                    "O preço do produto deve possuir no máximo duas casas decimais."));
            }

            if (stockQuantity < 0)
            {
                errors.Add(new Error(
                    "product.invalid_stock",
                    "A quantidade em estoque não pode ser negativa."));
            }

            if (string.IsNullOrWhiteSpace(currency) ||
                currency.Length != 3 ||
                !currency.All(character =>
                    character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
                        {
                            errors.Add(new Error(
                                "product.invalid_currency",
                                "A moeda do produto deve ser informada por um código de três letras."));
            }

            if (errors.Count > 0)
            {
                return Result<Product>.Failure(errors);
            }

            var product = new Product(
                id,
                name,
                price,
                currency.ToUpperInvariant(),
                stockQuantity);

            return Result<Product>.Success(product);
        }


        /// <summary>
        /// Reduz a quantidade disponível em estoque.
        /// </summary>
        /// <param name="quantity">Quantidade a ser retirada.</param>
        /// <returns>
        /// Resultado da operação ou erro de validação.
        /// </returns>
        public Result DecreaseStock(int quantity)
        {
            if (quantity <= 0)
            {
                return Result.Failure(new Error(
                    "product.invalid_stock_quantity",
                    "A quantidade movimentada deve ser maior que zero."));
            }

            if (StockQuantity < quantity)
            {
                return Result.Failure(new Error(
                    "product.insufficient_stock",
                    "A quantidade disponível em estoque é insuficiente."));
            }

            StockQuantity -= quantity;

            return Result.Success();
        }

        /// <summary>
        /// Aumenta a quantidade disponível em estoque.
        /// </summary>
        /// <param name="quantity">Quantidade a ser adicionada.</param>
        /// <returns>
        /// Resultado da operação ou erro de validação.
        /// </returns>
        public Result IncreaseStock(int quantity)
        {
            if (quantity <= 0)
            {
                return Result.Failure(new Error(
                    "product.invalid_stock_quantity",
                    "A quantidade movimentada deve ser maior que zero."));
            }

            if (StockQuantity > int.MaxValue - quantity)
            {
                return Result.Failure(new Error(
                    "product.stock_overflow",
                    "A reposição excede o limite permitido para o estoque."));
            }

            StockQuantity += quantity;

            return Result.Success();
        }

    }

}
