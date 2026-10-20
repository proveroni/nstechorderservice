namespace OrderService.Domain.Entities.Base
{
    public abstract class AuditableEntityBase
    {
        public DateTimeOffset CreatedAt { get; protected set; }

        public string CreatedBy { get; protected set; } = string.Empty;

        public DateTimeOffset? UpdatedAt { get; protected set; }

        public string? UpdatedBy { get; protected set; }
    }
}
