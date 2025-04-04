using System;

namespace PdfProcessorApi.Models;

public class ElasticDocument
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ExtractedText { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }
}