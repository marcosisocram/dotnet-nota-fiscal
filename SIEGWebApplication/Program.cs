using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using SIEGWebApplication.Consumers;
using SIEGWebApplication.DTOs;
using SIEGWebApplication.Models;
using SIEGWebApplication.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!builder.Environment.IsEnvironment("Testing") && !string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<FiscalDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddScoped<IFiscalDocumentService, FiscalDocumentService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<FiscalDocumentProcessedConsumer>();
    x.AddConsumer<FiscalDocumentUpdatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitSettings = builder.Configuration.GetSection("RabbitMq");
        cfg.Host(rabbitSettings["Host"] ?? "localhost", "/", h =>
        {
            h.Username(rabbitSettings["Username"] ?? "guest");
            h.Password(rabbitSettings["Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("fiscal-document-summary-queue", e =>
        {
            e.ConfigureConsumer<FiscalDocumentProcessedConsumer>(context);
        });

        cfg.ReceiveEndpoint("fiscal-document-update-log-queue", e =>
        {
            e.ConfigureConsumer<FiscalDocumentUpdatedConsumer>(context);
        });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var fiscalDocsApi = app.MapGroup("/api/v1/fiscal-documents")
    .WithTags("Fiscal Documents");

fiscalDocsApi.MapGet("/", async (
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? cnpj = null,
    [FromQuery] string? uf = null,
    [FromQuery] DateTimeOffset? startDate = null,
    [FromQuery] DateTimeOffset? endDate = null,
    IFiscalDocumentService service = null!) =>
{
    var result = await service.ListAsync(page, pageSize, cnpj, uf, startDate, endDate);
    return Results.Ok(result);
})
.WithName("ListFiscalDocuments");

fiscalDocsApi.MapGet("/{id:guid}", async (Guid id, IFiscalDocumentService service) =>
{
    var doc = await service.GetByIdAsync(id);
    return doc is not null ? Results.Ok(doc) : Results.NotFound(new { message = "Documento não encontrado." });
})
.WithName("GetFiscalDocumentById");

fiscalDocsApi.MapGet("/{id:guid}/xml", async (Guid id, IFiscalDocumentService service) =>
{
    var xml = await service.GetRawXmlByIdAsync(id);
    return xml is not null ? Results.Content(xml, "application/xml") : Results.NotFound(new { message = "Documento não encontrado." });
})
.WithName("GetFiscalDocumentXml");

fiscalDocsApi.MapPost("/xml", async (HttpRequest request, IFiscalDocumentService service) =>
    {
        if (request.ContentType is not ("application/xml" or "text/xml"))
            return Results.BadRequest("O conteúdo deve ser application/xml ou text/xml.");

        using var reader = new StreamReader(request.Body);
        var xmlContent = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(xmlContent))
            return Results.BadRequest("XML não pode estar vazio.");

        var (success, message, id) = await service.ProcessXmlAsync(xmlContent);

        if (!success)
            return message == "Documento já processado." ? Results.Conflict(new { message }) : Results.BadRequest(new { message });

        return Results.CreatedAtRoute("GetFiscalDocumentById", new { id }, new { message, id });
    })
    .Accepts<string>("application/xml")
    .WithName("CreateFiscalDocumentFromXml");

fiscalDocsApi.MapPost("/upload", async (IFormFile file, IFiscalDocumentService service) =>
    {
        if (Path.GetExtension(file.FileName).ToLower() != ".xml")
            return Results.BadRequest("Por favor, envie um arquivo XML válido.");

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var xmlContent = await reader.ReadToEndAsync();

        var (success, message, id) = await service.ProcessXmlAsync(xmlContent);

        if (!success)
            return message == "Documento já processado." ? Results.Conflict(new { message }) : Results.BadRequest(new { message });

        return Results.Ok(new { message, id, fileName = file.FileName });
    })
    .DisableAntiforgery()
    .WithName("UploadFiscalDocument");

fiscalDocsApi.MapPatch("/{id:guid}", async (Guid id, FiscalDocumentUpdateDto updateDto, IFiscalDocumentService service) =>
{
    var (success, message) = await service.UpdateAsync(id, updateDto);
    return success ? Results.Ok(new { message }) : Results.NotFound(new { message });
})
.WithName("PatchFiscalDocument");

fiscalDocsApi.MapPut("/upload", async (IFormFile file, IFiscalDocumentService service) =>
    {
        if (Path.GetExtension(file.FileName).ToLower() != ".xml")
            return Results.BadRequest("Por favor, envie um arquivo XML válido.");

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var xmlContent = await reader.ReadToEndAsync();

        var (success, message, id) = await service.UpdateByXmlAsync(xmlContent);

        if (!success)
            return message == "Documento não encontrado." ? Results.NotFound(new { message }) : Results.BadRequest(new { message });

        return Results.Ok(new { message, id, fileName = file.FileName });
    })
    .DisableAntiforgery()
    .WithName("UpdateFiscalDocumentViaXml");

fiscalDocsApi.MapDelete("/{id:guid}", async (Guid id, IFiscalDocumentService service) =>
{
    var (success, message) = await service.DeleteAsync(id);
    return success ? Results.Ok(new { message }) : Results.NotFound(new { message });
})
.WithName("DeleteFiscalDocument");

app.Run();

public partial class Program { }
