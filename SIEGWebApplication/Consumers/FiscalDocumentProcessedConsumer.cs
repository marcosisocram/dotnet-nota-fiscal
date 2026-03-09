using MassTransit;
using SIEGWebApplication.Messages;

namespace SIEGWebApplication.Consumers;

public class FiscalDocumentProcessedConsumer : IConsumer<FiscalDocumentProcessedEvent>
{
    private readonly ILogger<FiscalDocumentProcessedConsumer> _logger;

    public FiscalDocumentProcessedConsumer(ILogger<FiscalDocumentProcessedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<FiscalDocumentProcessedEvent> context)
    {
        var message = context.Message;
        
        var cnpj = message.EmitterCnpjCpf ?? string.Empty;
        var cnpjOfuscado = cnpj.Length == 14 
            ? $"{cnpj[..2]}.***.***/{cnpj[8..12]}-{cnpj[12..]}" 
            : cnpj;

        _logger.LogInformation(
            "| RESUMO DO DOCUMENTO PROCESSADO |\n" +
            "ID: {Id}\n" +
            "Chave de Acesso: {AccessKey}\n" +
            "Valor Total: {TotalValue:C2}\n" +
            "CNPJ Emitente: {Cnpj}\n" +
            "---------------------------------",
            message.Id,
            message.AccessKey,
            message.TotalValue,
            cnpjOfuscado);

        return Task.CompletedTask;
    }
}
