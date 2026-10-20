namespace OrderService.Domain.Enums
{
    /// <summary>
    /// Representa os estados possíveis de um pedido.
    /// </summary>
    public enum OrderStatusEnum
    {
        /// <summary>
        /// Pedido criado e aguardando confirmação ou cancelamento.
        /// </summary>
        Placed = 1,

        /// <summary>
        /// Pedido confirmado.
        /// </summary>
        Confirmed = 2,

        /// <summary>
        /// Pedido cancelado.
        /// </summary>
        Canceled = 3
    }

}
