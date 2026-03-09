using System.Xml.Linq;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIEGWebApplication.Models;
using SIEGWebApplication.Services;
using SIEGWebApplication.Messages;
using SIEGWebApplication.DTOs;

namespace SIEGWebApplication.Tests;

[TestFixture]
public class FiscalDocumentServiceTests
{
    private FiscalDbContext _dbContext;
    private Mock<IPublishEndpoint> _publishEndpointMock;
    private FiscalDocumentService _service;

    private const string ValidXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<nfeProc xmlns=""http://www.portalfiscal.inf.br/nfe"" versao=""3.10"">
    <NFe>
        <infNFe Id=""NFe35180407689002000189551200000002051956156304"" versao=""3.10"">
            <ide>
                <mod>55</mod>
                <nNF>205</nNF>
                <serie>120</serie>
                <dhEmi>2018-04-01T00:00:00-03:00</dhEmi>
            </ide>
            <emit>
                <CNPJ>07689002000189</CNPJ>
                <xNome>EMBRAER S/A</xNome>
                <enderEmit><UF>SP</UF></enderEmit>
            </emit>
            <dest>
                <xNome>DESTINATARIO TESTE</xNome>
            </dest>
            <total>
                <ICMSTot><vNF>16000.00</vNF></ICMSTot>
            </total>
            <det nItem=""1"">
                <prod>
                    <cProd>PROD1</cProd>
                    <xProd>PRODUTO TESTE</xProd>
                    <CFOP>7501</CFOP>
                    <uCom>UN</uCom>
                    <qCom>1000.00</qCom>
                    <vUnCom>16.00</vUnCom>
                    <vProd>16000.00</vProd>
                </prod>
            </det>
        </infNFe>
    </NFe>
</nfeProc>";

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<FiscalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FiscalDbContext(options);
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _service = new FiscalDocumentService(_dbContext, _publishEndpointMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task ProcessXmlAsync_ValidXml_ShouldSaveAndPublishEvent()
    {
        var result = await _service.ProcessXmlAsync(ValidXml);

        result.Success.Should().BeTrue();
        result.DocumentId.Should().NotBeNull();
        
        var docInDb = await _dbContext.FiscalDocuments.Include(d => d.Items).FirstOrDefaultAsync();
        docInDb.Should().NotBeNull();
        docInDb!.AccessKey.Should().Be("35180407689002000189551200000002051956156304");
        
        _publishEndpointMock.Verify(p => p.Publish(It.IsAny<FiscalDocumentProcessedEvent>(), default), Times.Once);
    }

    [Test]
    public async Task ProcessXmlAsync_DuplicateXml_ShouldReturnError()
    {
        var accessKey = "35180407689002000189551200000002051956156304";
        _dbContext.FiscalDocuments.Add(new FiscalDocument 
        { 
            Id = Guid.NewGuid(), 
            AccessKey = accessKey,
            EmitterName = "Teste",
            RawXml = ValidXml
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.ProcessXmlAsync(ValidXml);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Documento já processado.");
    }

    [Test]
    public async Task UpdateByXmlAsync_ExistingDocument_ShouldUpdate()
    {
        await _service.ProcessXmlAsync(ValidXml);
        var updatedXml = ValidXml.Replace("16000.00", "25000.00");

        var result = await _service.UpdateByXmlAsync(updatedXml);

        result.Success.Should().BeTrue();
        var docInDb = await _dbContext.FiscalDocuments.FirstAsync();
        docInDb.TotalValue.Should().Be(25000.00m);
        
        _publishEndpointMock.Verify(p => p.Publish(It.IsAny<FiscalDocumentUpdatedEvent>(), default), Times.Once);
    }

    [Test]
    public async Task ListAsync_DateRangeFilter_ShouldWork()
    {
        var baseDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _dbContext.FiscalDocuments.Add(new FiscalDocument { Id = Guid.NewGuid(), AccessKey = "K1", EmissionDate = baseDate, EmitterName = "E1", RawXml = "<x/>" });
        await _dbContext.SaveChangesAsync();

        var result = await _service.ListAsync(1, 10, startDate: baseDate.AddDays(-1), endDate: baseDate.AddDays(1));

        result.Items.Should().HaveCount(1);
    }

    [Test]
    public async Task DeleteAsync_ExistingId_ShouldRemove()
    {
        var id = Guid.NewGuid();
        _dbContext.FiscalDocuments.Add(new FiscalDocument { Id = id, AccessKey = "123", EmitterName = "T", RawXml = "<x/>" });
        await _dbContext.SaveChangesAsync();

        var result = await _service.DeleteAsync(id);

        result.Success.Should().BeTrue();
        _dbContext.FiscalDocuments.Should().BeEmpty();
    }
}
