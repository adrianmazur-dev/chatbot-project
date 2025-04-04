using PdfProcessorApi.Models;

namespace PdfProcessorApi.Services;

public interface IElasticsearchIndexingService
{
    /// <summary>
    /// Indexes a document in Elasticsearch asynchronously.
    /// </summary>
    /// <param name="document">The document to index.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating success or failure.</returns>
    Task<bool> IndexDocumentAsync(ElasticDocument document);
}