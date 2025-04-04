using PdfProcessorApi.Models;

namespace PdfProcessorApi.Services;

public interface IStructuredDataExtractorService
{
    Task<InvoiceData?> ExtractInvoiceDataAsync(string rawText);
}