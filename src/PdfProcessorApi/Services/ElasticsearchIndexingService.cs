using Elastic.Clients.Elasticsearch;
using PdfProcessorApi.Models;

namespace PdfProcessorApi.Services;

public class ElasticsearchIndexingService(
    ElasticsearchClient elasticClient,
    ILogger<ElasticsearchIndexingService> logger)
    : IElasticsearchIndexingService
{
    private readonly ElasticsearchClient _elasticClient = elasticClient;
    private readonly ILogger<ElasticsearchIndexingService> _logger = logger;


    public async Task<bool> IndexDocumentAsync(ElasticDocument document)
    {
        if (document == null || document.Id == Guid.Empty)
        {
            _logger.LogWarning("Document is null or has an empty ID.");
            return false;
        }

        _logger.LogInformation("Indexing document with ID: {DocumentId}", document.Id);

        try
        {
            var response = await _elasticClient.IndexAsync(document, document.Id);

            if (response.IsValidResponse)
            {
                _logger.LogInformation("Document indexed successfully with ID: {DocumentId}", document.Id);
                return true;
            }
            else
            {
                _logger.LogError("Failed to index document in Elasticsearch. ID: {DocumentId}. DebugInfo: {DebugInfo}",
                    document.Id, response.DebugInformation);

                if (response.ElasticsearchServerError != null)
                {
                    _logger.LogError("Elasticsearch server error: {ErrorType} - {ErrorReason}",
                        response.ElasticsearchServerError.Error?.Type, response.ElasticsearchServerError.Error?.Reason);
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while indexing document in Elasticsearch. ID: {DocumentId}", document.Id);
            return false;
        }
    }
}