using MassTransit;
using SIEGWebApplication.Messages;

namespace SIEGWebApplication.Consumers;

public class FiscalDocumentUpdatedConsumer : IConsumer<FiscalDocumentUpdatedEvent>
{
    private readonly ILogger<FiscalDocumentUpdatedConsumer> _logger;

    public FiscalDocumentUpdatedConsumer(ILogger<FiscalDocumentUpdatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<FiscalDocumentUpdatedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "| DOCUMENTO ATUALIZADO VIA XML |\n" +
            "ID: {Id}\n" +
            "Chave de Acesso: {AccessKey}\n" +
            "Novo Valor Total: {TotalValue:C2}\n" +
            "Atualizado em: {UpdatedAt}\n" +
            "---------------------------------",
            message.Id,
            message.AccessKey,
            message.TotalValue,
            message.UpdatedAt);

        return Task.CompletedTask;
    }
}
