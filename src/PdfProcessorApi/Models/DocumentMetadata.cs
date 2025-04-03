using System.ComponentModel.DataAnnotations;

namespace PdfProcessorApi.Models;

public class DocumentMetadata
{
    [Key] 
    public Guid Id { get; set; }
    
    [Required] 
    [MaxLength(255)] 
    public string OriginalFileName { get; set; } = null!;

    public string? DetectedDocumentType { get; set; }

    public DateTime UploadedAt { get; set; }

    public string? FilePath { get; set; }

    public ProcessingStatus Status { get; set; }

    public DocumentMetadata()
    {
        Id = Guid.NewGuid();
        UploadedAt = DateTime.UtcNow;
        Status = ProcessingStatus.Received;
        OriginalFileName = string.Empty;
    }

    /// <summary>
    /// Document processing status
    /// </summary>
    public enum ProcessingStatus
    {
        /// <summary>
        /// Document added to the queue
        /// </summary>
        Received = 0,

        /// <summary>
        /// Document is being processed
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Document has been processed successfully
        /// </summary>
        Processed = 2,

        /// <summary>
        /// Document processing failed
        /// </summary>
        Failed = 3
    }
}
