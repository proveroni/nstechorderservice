using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Configura a persistência dos itens pertencentes ao agregado Order.
    /// </summary>
    public static class OrderItemConfiguration
    {
        /// <summary>
        /// Configura a tabela, a chave composta, os relacionamentos
        /// e as restrições de integridade dos itens do pedido.
        /// </summary>
        /// <param name="builder">
        /// Construtor da navegação de itens pertencentes ao pedido.
        /// </param>
        public static void Configure(OwnedNavigationBuilder<Order, OrderItem> builder)
        {
            builder.ToTable("order_items");

            builder.WithOwner()
                .HasForeignKey("order_id");

            builder.Property(item => item.ProductId)
                .HasColumnName("product_id")
                .IsRequired();

            builder.HasKey(
                "order_id",
                nameof(OrderItem.ProductId));

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(item => item.Quantity)
                .HasColumnName("quantity")
                .IsRequired();

            builder.Property(item => item.UnitPrice)
                .HasColumnName("unit_price")
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Ignore(item => item.Total);

            builder.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "ck_order_items_quantity_positive",
                    "quantity > 0");

                table.HasCheckConstraint(
                    "ck_order_items_unit_price_non_negative",
                    "unit_price >= 0");
            });
        }
    }
}
