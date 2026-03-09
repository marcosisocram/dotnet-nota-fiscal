using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SIEGWebApplication.DTOs;
using SIEGWebApplication.Messages;
using SIEGWebApplication.Models;

namespace SIEGWebApplication.Services;

public interface IFiscalDocumentService
{
    Task<(bool Success, string Message, Guid? DocumentId)> ProcessXmlAsync(string xmlContent);
    Task<(bool Success, string Message, Guid? DocumentId)> UpdateByXmlAsync(string xmlContent);
    Task<PagedResponse<FiscalDocumentListItemDto>> ListAsync(
        int page, 
        int pageSize, 
        string? cnpj = null, 
        string? uf = null, 
        DateTimeOffset? startDate = null, 
        DateTimeOffset? endDate = null);
    Task<FiscalDocumentDetailDto?> GetByIdAsync(Guid id);
    Task<string?> GetRawXmlByIdAsync(Guid id);
    Task<(bool Success, string Message)> UpdateAsync(Guid id, FiscalDocumentUpdateDto updateDto);
    Task<(bool Success, string Message)> DeleteAsync(Guid id);
}

public class FiscalDocumentService : IFiscalDocumentService
{
    private readonly FiscalDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";

    public FiscalDocumentService(FiscalDbContext dbContext, IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<(bool Success, string Message, Guid? DocumentId)> ProcessXmlAsync(string xmlContent)
    {
        try
        {
            var fiscalDoc = ParseFiscalDocument(xmlContent);
            if (fiscalDoc == null) return (false, "XML malformado.", null);

            var existing = await _dbContext.FiscalDocuments.AnyAsync(d => d.AccessKey == fiscalDoc.AccessKey);
            if (existing) return (false, "Documento já processado.", null);

            _dbContext.FiscalDocuments.Add(fiscalDoc);
            await _dbContext.SaveChangesAsync();

            await _publishEndpoint.Publish(new FiscalDocumentProcessedEvent
            {
                Id = fiscalDoc.Id,
                AccessKey = fiscalDoc.AccessKey,
                DocumentType = fiscalDoc.DocumentType,
                DocumentNumber = fiscalDoc.DocumentNumber,
                Series = fiscalDoc.Series,
                EmissionDate = fiscalDoc.EmissionDate,
                EmitterCnpjCpf = fiscalDoc.EmitterCnpjCpf,
                EmitterName = fiscalDoc.EmitterName,
                EmitterState = fiscalDoc.EmitterState,
                ReceiverCnpjCpf = fiscalDoc.ReceiverCnpjCpf,
                ReceiverName = fiscalDoc.ReceiverName,
                ReceiverState = fiscalDoc.ReceiverState,
                TotalValue = fiscalDoc.TotalValue,
                RawXml = fiscalDoc.RawXml,
                CreatedAt = fiscalDoc.CreatedAt,
                UpdatedAt = fiscalDoc.UpdatedAt,
                EventTimestamp = DateTimeOffset.UtcNow
            });

            return (true, "Documento processado.", fiscalDoc.Id);
        }
        catch (Exception ex)
        {
            return (false, $"Falha no processamento: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message, Guid? DocumentId)> UpdateByXmlAsync(string xmlContent)
    {
        try
        {
            var newDocData = ParseFiscalDocument(xmlContent);
            if (newDocData == null) return (false, "XML malformado.", null);

            var existingDoc = await _dbContext.FiscalDocuments
                .Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.AccessKey == newDocData.AccessKey);

            if (existingDoc == null)
                return (false, "Documento não encontrado.", null);

            existingDoc.DocumentNumber = newDocData.DocumentNumber;
            existingDoc.Series = newDocData.Series;
            existingDoc.EmissionDate = newDocData.EmissionDate;
            existingDoc.EmitterCnpjCpf = newDocData.EmitterCnpjCpf;
            existingDoc.EmitterName = newDocData.EmitterName;
            existingDoc.EmitterState = newDocData.EmitterState;
            existingDoc.ReceiverCnpjCpf = newDocData.ReceiverCnpjCpf;
            existingDoc.ReceiverName = newDocData.ReceiverName;
            existingDoc.ReceiverState = newDocData.ReceiverState;
            existingDoc.TotalValue = newDocData.TotalValue;
            existingDoc.RawXml = xmlContent;
            existingDoc.UpdatedAt = DateTimeOffset.UtcNow;

            _dbContext.FiscalDocumentItems.RemoveRange(existingDoc.Items);
            foreach (var item in newDocData.Items)
            {
                item.FiscalDocumentId = existingDoc.Id;
                existingDoc.Items.Add(item);
            }

            await _dbContext.SaveChangesAsync();

            await _publishEndpoint.Publish(new FiscalDocumentUpdatedEvent
            {
                Id = existingDoc.Id,
                AccessKey = existingDoc.AccessKey,
                DocumentNumber = existingDoc.DocumentNumber,
                TotalValue = existingDoc.TotalValue,
                UpdatedAt = existingDoc.UpdatedAt.Value,
                EventTimestamp = DateTimeOffset.UtcNow
            });

            return (true, "Documento atualizado.", existingDoc.Id);
        }
        catch (Exception ex)
        {
            return (false, $"Falha na atualização: {ex.Message}", null);
        }
    }

    private FiscalDocument? ParseFiscalDocument(string xmlContent)
    {
        xmlContent = Regex.Replace(xmlContent, @"<\/\s+", "</");
        xmlContent = Regex.Replace(xmlContent, @"\s+>", ">");
        xmlContent = Regex.Replace(xmlContent, @"<\s+", "<");

        var doc = XDocument.Parse(xmlContent);
        var infNFe = doc.Descendants(Ns + "infNFe").FirstOrDefault();

        if (infNFe == null) return null;

        var accessKey = infNFe.Attribute("Id")?.Value?.Replace("NFe", "").Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accessKey)) return null;

        var ide = infNFe.Element(Ns + "ide")!;
        var emit = infNFe.Element(Ns + "emit")!;
        var dest = infNFe.Element(Ns + "dest");
        var total = infNFe.Element(Ns + "total")?.Element(Ns + "ICMSTot");

        var fiscalDoc = new FiscalDocument
        {
            AccessKey = accessKey,
            DocumentType = int.Parse(GetCleanValue(ide.Element(Ns + "mod")) ?? "55"),
            DocumentNumber = GetCleanValue(ide.Element(Ns + "nNF")) ?? string.Empty,
            Series = GetCleanValue(ide.Element(Ns + "serie")) ?? string.Empty,
            EmissionDate = DateTimeOffset.Parse(GetCleanDateValue(ide.Element(Ns + "dhEmi")) ?? DateTimeOffset.UtcNow.ToString()).ToUniversalTime(),
            EmitterCnpjCpf = GetCleanValue(emit.Element(Ns + "CNPJ") ?? emit.Element(Ns + "CPF")) ?? string.Empty,
            EmitterName = GetCleanValue(emit.Element(Ns + "xNome")) ?? string.Empty,
            EmitterState = GetCleanValue(emit.Element(Ns + "enderEmit")?.Element(Ns + "UF")) ?? string.Empty,
            ReceiverCnpjCpf = GetCleanValue(dest?.Element(Ns + "CNPJ") ?? dest?.Element(Ns + "CPF")),
            ReceiverName = GetCleanValue(dest?.Element(Ns + "xNome")) ?? "NÃO INFORMADO",
            ReceiverState = GetCleanValue(dest?.Element(Ns + "enderDest")?.Element(Ns + "UF")),
            TotalValue = ParseDecimal(GetCleanValue(total?.Element(Ns + "vNF"))),
            RawXml = xmlContent,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var det in infNFe.Elements(Ns + "det"))
        {
            var prod = det.Element(Ns + "prod")!;
            fiscalDoc.Items.Add(new FiscalDocumentItem
            {
                ItemNumber = int.Parse(det.Attribute("nItem")?.Value ?? "0"),
                ProductCode = GetCleanValue(prod.Element(Ns + "cProd")) ?? string.Empty,
                ProductName = GetCleanValue(prod.Element(Ns + "xProd")) ?? string.Empty,
                Ncm = GetCleanValue(prod.Element(Ns + "NCM")),
                Cfop = GetCleanValue(prod.Element(Ns + "CFOP")) ?? string.Empty,
                Unit = GetCleanValue(prod.Element(Ns + "uCom")) ?? string.Empty,
                Quantity = ParseDecimal(GetCleanValue(prod.Element(Ns + "qCom"))),
                UnitPrice = ParseDecimal(GetCleanValue(prod.Element(Ns + "vUnCom"))),
                TotalPrice = ParseDecimal(GetCleanValue(prod.Element(Ns + "vProd")))
            });
        }

        return fiscalDoc;
    }

    public async Task<(bool Success, string Message)> DeleteAsync(Guid id)
    {
        var doc = await _dbContext.FiscalDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return (false, "Documento não encontrado.");
        _dbContext.FiscalDocuments.Remove(doc);
        await _dbContext.SaveChangesAsync();
        return (true, "Documento excluído.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(Guid id, FiscalDocumentUpdateDto updateDto)
    {
        var doc = await _dbContext.FiscalDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return (false, "Documento não encontrado.");
        if (!string.IsNullOrWhiteSpace(updateDto.ReceiverName)) doc.ReceiverName = updateDto.ReceiverName;
        if (!string.IsNullOrWhiteSpace(updateDto.ReceiverState)) doc.ReceiverState = updateDto.ReceiverState;
        if (!string.IsNullOrWhiteSpace(updateDto.ReceiverCnpjCpf)) doc.ReceiverCnpjCpf = updateDto.ReceiverCnpjCpf;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();
        return (true, "Documento atualizado.");
    }

    public async Task<string?> GetRawXmlByIdAsync(Guid id)
    {
        var doc = await _dbContext.FiscalDocuments.AsNoTracking().Select(d => new { d.Id, d.RawXml }).FirstOrDefaultAsync(d => d.Id == id);
        return doc?.RawXml;
    }

    public async Task<FiscalDocumentDetailDto?> GetByIdAsync(Guid id)
    {
        var doc = await _dbContext.FiscalDocuments.AsNoTracking().Include(d => d.Items).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return null;
        return new FiscalDocumentDetailDto(doc.Id, doc.AccessKey, doc.DocumentType, doc.DocumentNumber, doc.Series, doc.EmissionDate, doc.EmitterCnpjCpf, doc.EmitterName, doc.EmitterState, doc.ReceiverCnpjCpf, doc.ReceiverName, doc.ReceiverState, doc.TotalValue, doc.Items.Select(i => new FiscalDocumentItemDto(i.ItemNumber, i.ProductCode, i.ProductName, i.Ncm, i.Cfop, i.Unit, i.Quantity, i.UnitPrice, i.TotalPrice)));
    }

    public async Task<PagedResponse<FiscalDocumentListItemDto>> ListAsync(int page, int pageSize, string? cnpj = null, string? uf = null, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
    {
        var query = _dbContext.FiscalDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(cnpj)) query = query.Where(d => d.EmitterCnpjCpf == cnpj || d.ReceiverCnpjCpf == cnpj);
        if (!string.IsNullOrWhiteSpace(uf)) query = query.Where(d => d.EmitterState == uf || d.ReceiverState == uf);
        if (startDate.HasValue) query = query.Where(d => d.EmissionDate >= startDate.Value.ToUniversalTime());
        if (endDate.HasValue) query = query.Where(d => d.EmissionDate <= endDate.Value.ToUniversalTime());
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = await query.OrderByDescending(d => d.EmissionDate).Skip((page - 1) * pageSize).Take(pageSize).Select(d => new FiscalDocumentListItemDto(d.Id, d.AccessKey, d.DocumentNumber, d.Series, d.EmissionDate, d.EmitterCnpjCpf, d.EmitterName, d.EmitterState, d.TotalValue)).ToListAsync();
        return new PagedResponse<FiscalDocumentListItemDto>(items, page, pageSize, totalItems, totalPages);
    }

    private string? GetCleanValue(XElement? element)
    {
        if (element == null) return null;
        return Regex.Replace(element.Value, @"\s+", " ").Trim();
    }

    private string? GetCleanDateValue(XElement? element)
    {
        if (element == null) return null;
        return Regex.Replace(element.Value, @"\s+", "").Trim();
    }

    private decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var cleanValue = value.Replace(" ", "").Replace(",", ".");
        return decimal.Parse(cleanValue, CultureInfo.InvariantCulture);
    }
}
