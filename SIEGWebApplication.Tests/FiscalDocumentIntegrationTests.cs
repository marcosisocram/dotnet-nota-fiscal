using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIEGWebApplication.DTOs;
using SIEGWebApplication.Models;

namespace SIEGWebApplication.Tests;

[TestFixture]
public class FiscalDocumentIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    private const string ValidXml = @"<nfeProc xmlns=""http://www.portalfiscal.inf.br/nfe"">
        <NFe><infNFe Id=""NFe35180407689002000189551200000002051956156304"">
            <ide><mod>55</mod><nNF>205</nNF><serie>120</serie><dhEmi>2018-04-01T00:00:00-03:00</dhEmi></ide>
            <emit><CNPJ>07689002000189</CNPJ><xNome>EMBRAER</xNome><enderEmit><UF>SP</UF></enderEmit></emit>
            <dest><xNome>DEST</xNome></dest>
            <total><ICMSTot><vNF>16000.00</vNF></ICMSTot></total>
            <det nItem=""1""><prod><cProd>P1</cProd><xProd>PROD</xProd><CFOP>7501</CFOP><uCom>UN</uCom><qCom>1000</qCom><vUnCom>16.00</vUnCom><vProd>16000</vProd></prod></det>
        </infNFe></NFe>
    </nfeProc>";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(FiscalDbContext));
                if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

                var optionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FiscalDbContext>));
                if (optionsDescriptor != null) services.Remove(optionsDescriptor);

                services.AddDbContext<FiscalDbContext>(options => options.UseInMemoryDatabase("IntegrationTestsDb"));
                services.AddMassTransitTestHarness();
            });
        });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test, Order(1)]
    public async Task PostXml_ShouldCreate()
    {
        var content = new StringContent(ValidXml);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

        var response = await _client.PostAsync("/api/v1/fiscal-documents/xml", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test, Order(2)]
    public async Task GetFiscalDocuments_ShouldReturnPagedResponse()
    {
        var response = await _client.GetAsync("/api/v1/fiscal-documents");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test, Order(3)]
    public async Task GetDetails_And_Xml_ShouldWork()
    {
        var listResponse = await _client.GetAsync("/api/v1/fiscal-documents");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResponse<FiscalDocumentListItemDto>>();
        var id = list!.Items.First().Id;

        var detailResponse = await _client.GetAsync($"/api/v1/fiscal-documents/{id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var xmlResponse = await _client.GetAsync($"/api/v1/fiscal-documents/{id}/xml");
        xmlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test, Order(4)]
    public async Task Delete_ShouldRemoveDocument()
    {
        var listResponse = await _client.GetAsync("/api/v1/fiscal-documents");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResponse<FiscalDocumentListItemDto>>();
        var id = list!.Items.First().Id;

        var response = await _client.DeleteAsync($"/api/v1/fiscal-documents/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
