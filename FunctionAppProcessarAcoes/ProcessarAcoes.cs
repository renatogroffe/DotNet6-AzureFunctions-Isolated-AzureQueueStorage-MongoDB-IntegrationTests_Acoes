using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FunctionAppProcessarAcoes.Data;
using FunctionAppProcessarAcoes.Models;
using FunctionAppProcessarAcoes.Validators;

namespace FunctionAppProcessarAcoes;

public class ProcessarAcoes
{
    private readonly ILogger _logger;
    private readonly AcoesRepository _repository;

    public ProcessarAcoes(ILoggerFactory loggerFactory,
        AcoesRepository repository)
    {
        _logger = loggerFactory.CreateLogger<ProcessarAcoes>();
        _repository = repository;
    }

    [Function(nameof(ProcessarAcoes))]
    public void Run([QueueTrigger("queue-acoes",
        Connection = "AzureWebJobsStorage")] DadosAcao dados)
    {
        _logger.LogInformation(
            $"Dados recebidos: {JsonSerializer.Serialize(dados)}");

        var validationResult = new DadosAcaoValidator().Validate(dados);
        if (validationResult.IsValid)
        {
            _repository.Save(dados);
            _logger.LogInformation("Acao registrada com sucesso!");
        }
        else
        {
            _logger.LogError("Dados invalidos para a Acao");
            foreach (var error in validationResult.Errors)
                _logger.LogError($" ## {error.ErrorMessage}");
        }
    }
}