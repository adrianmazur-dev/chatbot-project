using System.Text;
using UglyToad.PdfPig;

namespace PdfProcessorApi.Services;

public class PdfTextExtractorService(ILogger<PdfTextExtractorService> logger) : IPdfTextExtractorService
{
    private readonly ILogger<PdfTextExtractorService> _logger = logger;

    public async Task<string?> ExtractTextAsync(string pdfFilePath)
    {
        _logger.LogInformation("Extracting text from PDF file: {PdfFilePath}", pdfFilePath);

        if (!File.Exists(pdfFilePath))
        {
            _logger.LogWarning("PDF file not found: {PdfFilePath}", pdfFilePath);
            return null;
        }

        var documentTextBuilder = new StringBuilder();

        try
        {
            await Task.Run(() =>
            {
                using (PdfDocument document = PdfDocument.Open(pdfFilePath))
                {
                    int pageCount = document.NumberOfPages;
                    _logger.LogDebug("Number of pages in PDF: {PageCount}", pageCount);

                    for (int i = 1; i <= pageCount; i++)
                    {
                        var page = document.GetPage(i);
                        documentTextBuilder.Append(page.Text);
                        documentTextBuilder.AppendLine();
                    }
                }
            });

            string extractedText = documentTextBuilder.ToString();
            _logger.LogInformation("Text extraction completed successfully from PDF file : {PdfFilePath}", pdfFilePath);

            return extractedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF file: {PdfFilePath}", pdfFilePath);
            return null;
        }
    }
}
