using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Configuração do mapeamento da entidade Product
    /// para o banco de dados PostgreSQL.
    /// </summary>
    public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        /// <summary>
        /// Configura a tabela, a chave primária e as propriedades
        /// persistidas da entidade Product.
        /// </summary>
        /// <param name="builder">
        /// Construtor de configuração da entidade.
        /// </param>
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("products");

            builder.HasKey(product => product.Id);

            builder.Property(product => product.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            builder.Property(product => product.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(product => product.Price)
                .HasColumnName("price")
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(product => product.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(product => product.StockQuantity)
                .HasColumnName("stock_quantity")
                .IsRequired();

            builder.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "ck_products_price_non_negative",
                    "price >= 0");

                table.HasCheckConstraint(
                    "ck_products_stock_non_negative",
                    "stock_quantity >= 0");
            });
        }
    }
}
