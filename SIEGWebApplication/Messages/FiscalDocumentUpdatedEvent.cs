namespace SIEGWebApplication.Messages;

public record FiscalDocumentUpdatedEvent
{
    public Guid Id { get; init; }
    public string AccessKey { get; init; } = string.Empty;
    public string DocumentNumber { get; init; } = string.Empty;
    public decimal TotalValue { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset EventTimestamp { get; init; }

    public FiscalDocumentUpdatedEvent() { }
}
