using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using Azure.Storage.Queues;
using MongoDB.Driver;
using Serilog;
using Serilog.Core;
using FunctionAppProcessarAcoes.IntegrationTests.Documents;
using FunctionAppProcessarAcoes.IntegrationTests.Models;

namespace FunctionAppProcessarAcoes.IntegrationTests;

public class TestesFunctionAppProcessarAcoes
{
    private const string COD_CORRETORA = "00000";
    private const string NOME_CORRETORA = "Corretora Testes";
    private static IConfiguration Configuration { get; }
    private static Logger Logger { get; }

    static TestesFunctionAppProcessarAcoes()
    {
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile($"appsettings.json")
            .AddEnvironmentVariables().Build();

        Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }

    [Theory]
    [InlineData("ABCD", 100.98)]
    [InlineData("EFGH", 200.9)]
    [InlineData("IJKL", 1_400.978)]
    public void TestarFunctionApp(string codigo, double valor)
    {
        var queueName = Configuration["QueueName"];
        Logger.Information($"Tópico: {queueName}");

        var cotacaoAcao = new Acao()
        {
            Codigo = codigo,
            Valor = valor,
            CodCorretora = COD_CORRETORA,
            NomeCorretora = NOME_CORRETORA
        };
        var conteudoAcao = JsonSerializer.Serialize(cotacaoAcao);
        Logger.Information($"Dados: {conteudoAcao}");

        var queueClient = new QueueClient(
            Configuration["AzureWebJobsStorage"], queueName,
            new QueueClientOptions()
            {
                MessageEncoding = QueueMessageEncoding.Base64
            });
        if (queueClient.CreateIfNotExists() is not null)
            Logger.Information($"Criada a fila {queueName} no Azure Storage");
        
        queueClient.SendMessage(conteudoAcao);
        Logger.Information(
           $"Azure Storage Queue - Envio para a fila {queueName} concluído | " +
            conteudoAcao);

        Logger.Information("Aguardando o processamento da Function App...");
        Thread.Sleep(
            Convert.ToInt32(Configuration["IntervaloProcessamento"]));

        var mongoDBConnection = Configuration["MongoDB_Connection"];
        Logger.Information($"MongoDB Connection: {mongoDBConnection}");

        var mongoDatabase = Configuration["MongoDB_Database"];
        Logger.Information($"MongoDB Database: {mongoDatabase}");

        var mongoCollection = Configuration["MongoDB_Collection"];
        Logger.Information($"MongoDB Collection: {mongoCollection}");

        var acaoDocument = new MongoClient(mongoDBConnection)
            .GetDatabase(mongoDatabase)
            .GetCollection<AcaoDocument>(mongoCollection)
            .Find(h => h.Codigo == codigo).SingleOrDefault();

        acaoDocument.Should().NotBeNull();
        acaoDocument.Codigo.Should().Be(codigo);
        acaoDocument.Valor.Should().Be(valor);
        acaoDocument.DataReferencia.Should().NotBeNullOrWhiteSpace();
        acaoDocument.CorretoraResponsavel.Should().NotBeNull();
        acaoDocument.CorretoraResponsavel?.Codigo.Should().Be(COD_CORRETORA);
        acaoDocument.CorretoraResponsavel?.Nome.Should().Be(NOME_CORRETORA);
    }
}