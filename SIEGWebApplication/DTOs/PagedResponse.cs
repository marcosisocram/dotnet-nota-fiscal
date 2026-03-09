namespace SIEGWebApplication.DTOs;

public record FiscalDocumentListItemDto(
    Guid Id,
    string AccessKey,
    string DocumentNumber,
    string Series,
    DateTimeOffset EmissionDate,
    string EmitterCnpjCpf,
    string EmitterName,
    string EmitterState,
    decimal TotalValue
);

public record PagedResponse<T>(
    IEnumerable<T> Items,
    int PageNumber,
    int PageSize,
    int TotalItems,
    int TotalPages
);

public record FiscalDocumentItemDto(
    int ItemNumber,
    string ProductCode,
    string ProductName,
    string? Ncm,
    string Cfop,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public record FiscalDocumentDetailDto(
    Guid Id,
    string AccessKey,
    int DocumentType,
    string DocumentNumber,
    string Series,
    DateTimeOffset EmissionDate,
    string EmitterCnpjCpf,
    string EmitterName,
    string EmitterState,
    string? ReceiverCnpjCpf,
    string ReceiverName,
    string? ReceiverState,
    decimal TotalValue,
    IEnumerable<FiscalDocumentItemDto> Items
);

public record FiscalDocumentUpdateDto(
    string? ReceiverName,
    string? ReceiverState,
    string? ReceiverCnpjCpf
);
