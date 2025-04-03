namespace PdfProcessorApi.Services;

public interface IPdfTextExtractorService
{
    /// <summary>
    /// Extracts text from a PDF file asynchronously.
    /// </summary>
    /// <param name="pdfFilePath">The path to the PDF file.</param>
    /// <returns> A task that represents the asynchronous operation. The task result contains the extracted text as a string.</returns>
    Task<string?> ExtractTextAsync(string pdfFilePath);
}
