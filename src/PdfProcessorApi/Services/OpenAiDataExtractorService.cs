using System;
using System.ClientModel;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using PdfProcessorApi.Models;


namespace PdfProcessorApi.Services;

public class OpenAiDataExtractorService : IStructuredDataExtractorService
{
    private readonly ILogger<OpenAiDataExtractorService> _logger;
    private readonly OpenAIClient _openAIClient;
    private readonly string _modelName;

    public OpenAiDataExtractorService(ILogger<OpenAiDataExtractorService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var apiKey = configuration["OpenAISettings:ApiKey"];
        _modelName = configuration["OpenAISettings:ModelName"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogCritical("BRAK KLUCZA API OpenAI! Ustaw sekret 'OpenAISettings:ApiKey'.");
            throw new InvalidOperationException("Nie skonfigurowano klucza API OpenAI.");
        }

        _openAIClient = new OpenAIClient(apiKey);
        _logger.LogInformation("Inicjalizacja klienta OpenAI dla modelu: {ModelName}", _modelName);
    }

    public async Task<InvoiceData?> ExtractInvoiceDataAsync(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            _logger.LogWarning("Próba ekstrakcji danych ze pustego tekstu.");
            return null;
        }

        // Ograniczenie długości tekstu wysyłanego do LLM (opcjonalne, ale dobre dla kosztów/wydajności)
        const int maxTextLength = 15000; // Dostosuj limit znaków (tokenów)
        if (rawText.Length > maxTextLength)
        {
            _logger.LogWarning("Tekst wejściowy ({Length}) przekracza limit {Limit} znaków. Przycinanie.", rawText.Length, maxTextLength);
            rawText = rawText.Substring(0, maxTextLength);
        }


        _logger.LogInformation("Próba ekstrakcji danych strukturalnych z faktury za pomocą modelu OpenAI: {ModelName}", _modelName);

        string systemPrompt = @"Jesteś ekspertem w automatycznej ekstrakcji danych z dokumentów.
Analizuj dostarczony tekst pochodzący z faktury VAT.
Twoim zadaniem jest dokładne wyekstrahowanie następujących pól:

InvoiceNumber (string): Główny numer faktury.
InvoiceDate (string, format YYYY-MM-DD): Data wystawienia faktury. Jeśli nie ma pełnej daty, spróbuj ustalić najlepszą możliwą (np. YYYY-MM-01).
VendorName (string): Nazwa firmy wystawiającej fakturę (sprzedawcy).
CustomerName (string): Nazwa firmy lub osoby otrzymującej fakturę (nabywcy).
NetAmount (liczba): Całkowita kwota netto (przed podatkiem). Użyj '.' jako separatora dziesiętnego.
TaxAmount (liczba): Całkowita kwota podatku (VAT). Użyj '.' jako separatora dziesiętnego.
GrossAmount (liczba): Całkowita kwota brutto do zapłaty (netto + podatek). Użyj '.' jako separatora dziesiętnego.
Odpowiedź zwróć WYŁĄCZNIE w formacie JSON, używając dokładnie tych nazw pól.
Jeśli jakiejś wartości nie można znaleźć lub określić, użyj wartości null dla tego pola.
Nie dodawaj żadnych wyjaśnień, wstępów ani opisów poza strukturą JSON.

Przykład poprawnej odpowiedzi JSON:
```json
{
  """"InvoiceNumber"""": """"FV/123/2024"""",
  """"InvoiceDate"""": """"2024-03-15"""",
  """"VendorName"""": """"Firma Sprzedająca Sp. z o.o."""",
  """"CustomerName"""": """"Firma Kupująca S.A."""",
  """"NetAmount"""": 1500.75,
  """"TaxAmount"""": 345.17,
  """"GrossAmount"""": 1845.92
}
```";
        string userPrompt = $"Oto tekst wyekstrahowany z faktury:\n\n---\n{rawText}\n---";

        try
        {
            var chatClient = _openAIClient.GetChatClient(_modelName);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var response = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions()
            {
                Temperature = 0.1f,
                FrequencyPenalty = 0f,
                PresencePenalty = 0f,
                MaxOutputTokenCount = 500,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            });

            var message = response.Value.Content.Last();
            if (message.Text != null)
            {
                string jsonResponse = message.Text.Trim();
                _logger.LogInformation("Otrzymano odpowiedź JSON z OpenAI: {JsonResponse}", jsonResponse);

                // --- Parsowanie Odpowiedzi JSON ---
                try
                {
                    // Deserializacja JSONa do naszego obiektu InvoiceData
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; // Ignoruj wielkość liter w nazwach pól JSON
                    InvoiceData? extractedData = JsonSerializer.Deserialize<InvoiceData>(jsonResponse, options);

                    if (extractedData != null)
                    {
                        _logger.LogInformation("Pomyślnie sparsowano dane strukturalne z odpowiedzi OpenAI.");
                        return extractedData;
                    }
                    else
                    {
                        _logger.LogError("Deserializacja JSON z OpenAI do obiektu InvoiceData zwróciła null. JSON: {JsonResponse}", jsonResponse);
                        return null;
                    }
                }
                catch (JsonException jsonEx)
                {
                    // Błąd podczas parsowania JSONa (np. LLM zwrócił nieprawidłowy format)
                    _logger.LogError(jsonEx, "Błąd parsowania JSON z odpowiedzi OpenAI: {JsonResponse}", jsonResponse);
                    return null;
                }
            }
            else { _logger.LogWarning("Odpowiedź z OpenAI nie zawiera treści (content był null)."); return null; }
        }
        catch (Exception ex) // Złapanie błędów API, sieciowych itp.
        {
            _logger.LogError(ex, "Wyjątek podczas wywoływania API OpenAI.");
            return null;
        }

        
    }
}