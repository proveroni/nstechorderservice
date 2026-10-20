using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Contexts
{
    /// <summary>
    /// Contexto de persistência do serviço de pedidos.
    /// </summary>
    public sealed class OrderServiceDbContext : DbContext
    {
        /// <summary>
        /// Inicializa o contexto utilizando as opções
        /// configuradas pela aplicação.
        /// </summary>
        /// <param name="options">
        /// Opções de configuração do Entity Framework Core.
        /// </param>
        public OrderServiceDbContext(
            DbContextOptions<OrderServiceDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Pedidos armazenados no banco de dados.
        /// </summary>
        public DbSet<Order> Orders => Set<Order>();

        /// <summary>
        /// Produtos armazenados no banco de dados.
        /// </summary>
        public DbSet<Product> Products => Set<Product>();

        /// <summary>
        /// Aplica os mapeamentos das entidades definidos
        /// no assembly da Infrastructure.
        /// </summary>
        /// <param name="modelBuilder">
        /// Construtor do modelo do Entity Framework Core.
        /// </param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(OrderServiceDbContext).Assembly);
        }
    }
}
