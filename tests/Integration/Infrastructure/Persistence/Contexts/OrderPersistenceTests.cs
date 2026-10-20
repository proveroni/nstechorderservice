using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Infrastructure.IntegrationTests.Persistence.Contexts
{
    /// <summary>
    /// Testes de integração da persistência de pedidos
    /// e seus itens utilizando PostgreSQL real.
    /// </summary>
    public sealed class OrderPersistenceTests
    {
        private const string CreatedBy = "test-user-001";

        [Fact]
        public async Task Order_ShouldBePersistedAndRetrievedWithItems()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            var createdAt = new DateTimeOffset(
                now.Ticks - (now.Ticks % 10),
                TimeSpan.Zero);

            var productResult = Product.Create(
                productId,
                "Produto para pedido",
                125.50m,
                "BRL",
                10);

            Assert.True(productResult.IsSuccess);

            var itemResult = OrderItem.Create(
                productId,
                2,
                125.50m);

            Assert.True(itemResult.IsSuccess);

            var orderResult = Order.Create(
                customerId,
                "BRL",
                [itemResult.Value],
                createdAt,
                CreatedBy);

            Assert.True(orderResult.IsSuccess);

            var order = orderResult.Value;

            try
            {
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    context.Products.Add(productResult.Value);
                    context.Orders.Add(order);

                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                // Act
                await using (var context = IntegrationTestDatabase.CreateContext())
                {
                    var persistedOrder = await context.Orders
                        .AsNoTracking()
                        .Include(candidate => candidate.Items)
                        .SingleAsync(candidate => candidate.Id == order.Id, TestContext.Current.CancellationToken);

                    // Assert
                    Assert.Equal(order.Id, persistedOrder.Id);
                    Assert.Equal(customerId, persistedOrder.CustomerId);
                    Assert.Equal("BRL", persistedOrder.Currency);
                    Assert.Equal(OrderStatusEnum.Placed, persistedOrder.Status);
                    Assert.Equal(createdAt, persistedOrder.CreatedAt);
                    Assert.Equal(CreatedBy, persistedOrder.CreatedBy);
                    Assert.Null(persistedOrder.UpdatedAt);
                    Assert.Null(persistedOrder.UpdatedBy);
                    Assert.Null(persistedOrder.ConfirmedBy);
                    Assert.Null(persistedOrder.CanceledBy);

                    var persistedItem = Assert.Single(persistedOrder.Items);

                    Assert.Equal(productId, persistedItem.ProductId);
                    Assert.Equal(2, persistedItem.Quantity);
                    Assert.Equal(125.50m, persistedItem.UnitPrice);
                    Assert.Equal(251.00m, persistedOrder.Total);
                }
            }
            finally
            {
                await using var context = IntegrationTestDatabase.CreateContext();

                var persistedOrder = await context.Orders
                    .SingleOrDefaultAsync(candidate => candidate.Id == order.Id, TestContext.Current.CancellationToken);

                if (persistedOrder is not null)
                {
                    context.Orders.Remove(persistedOrder);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }

                var persistedProduct = await context.Products
                    .SingleOrDefaultAsync(candidate => candidate.Id == productId, TestContext.Current.CancellationToken);

                if (persistedProduct is not null)
                {
                    context.Products.Remove(persistedProduct);
                    await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }
        }
    }
}
