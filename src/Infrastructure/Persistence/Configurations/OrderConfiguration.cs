using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Configura o mapeamento do agregado Order e de seus itens.
    /// </summary>
    public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        /// <summary>
        /// Configura a persistência do pedido e da coleção
        /// de itens pertencentes ao agregado.
        /// </summary>
        /// <param name="builder">
        /// Construtor de configuração da entidade.
        /// </param>
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("orders");

            builder.HasKey(order => order.Id);

            builder.Property(order => order.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            builder.Property(order => order.CustomerId)
                .HasColumnName("customer_id")
                .IsRequired();

            builder.Property(order => order.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(order => order.Status)
                .HasColumnName("status")
                .HasConversion<int>()
                .IsRequired();

            builder.Property(order => order.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(order => order.CreatedBy)
                .HasColumnName("created_by")
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(order => order.UpdatedAt)
                .HasColumnName("updated_at");

            builder.Property(order => order.UpdatedBy)
                .HasColumnName("updated_by")
                .HasMaxLength(256);

            builder.Property(order => order.ConfirmedBy)
                .HasColumnName("confirmed_by")
                .HasMaxLength(256);

            builder.Property(order => order.CanceledBy)
                .HasColumnName("canceled_by")
                .HasMaxLength(256);

            builder.Property(order => order.ConfirmedAt)
                .HasColumnName("confirmed_at");

            builder.Property(order => order.CanceledAt)
                .HasColumnName("canceled_at");

            builder.Ignore(order => order.Total);

            builder.HasIndex(order => order.CustomerId);

            builder.HasIndex(order => order.Status);

            builder.HasIndex(order => order.CreatedAt);


            builder.OwnsMany(order => order.Items, OrderItemConfiguration.Configure);


            builder.Navigation(order => order.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
