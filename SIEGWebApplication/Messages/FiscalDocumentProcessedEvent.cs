namespace SIEGWebApplication.Messages;

public record FiscalDocumentProcessedEvent
{
    public Guid Id { get; init; }
    public string AccessKey { get; init; } = string.Empty;
    public int DocumentType { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string Series { get; init; } = string.Empty;
    public DateTimeOffset EmissionDate { get; init; }
    public string EmitterCnpjCpf { get; init; } = string.Empty;
    public string EmitterName { get; init; } = string.Empty;
    public string EmitterState { get; init; } = string.Empty;
    public string? ReceiverCnpjCpf { get; init; }
    public string ReceiverName { get; init; } = string.Empty;
    public string? ReceiverState { get; init; }
    public decimal TotalValue { get; init; }
    public string RawXml { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public DateTimeOffset EventTimestamp { get; init; }

    // Construtor padrão para o Serializador do MassTransit
    public FiscalDocumentProcessedEvent() { }
}
