using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.IntegrationTests.Persistence.Contexts
{
    /// <summary>
    /// Testes de configuração do modelo de persistência.
    /// </summary>
    public sealed class OrderServiceDbContextTests
    {
        [Fact]
        public void Model_ShouldConfigureOrderAndProductEntities()
        {
            // Arrange
            using var context = IntegrationTestDatabase.CreateContext();

            // Act
            var model = context.Model;

            // Assert
            Assert.NotNull(model.FindEntityType(typeof(Order)));
            Assert.NotNull(model.FindEntityType(typeof(Product)));

            var orderItemEntityType = model
                .GetEntityTypes()
                .Single(entityType =>
                    entityType.ClrType == typeof(OrderItem));

            Assert.True(orderItemEntityType.IsOwned());

            var primaryKey = orderItemEntityType.FindPrimaryKey();

            Assert.NotNull(primaryKey);

            Assert.Equal(
                ["order_id", nameof(OrderItem.ProductId)],
                primaryKey.Properties.Select(property => property.Name));
        }
    }
}
