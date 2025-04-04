using System.ComponentModel.DataAnnotations;
using Elastic.Clients.Elasticsearch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfProcessorApi.Data;
using PdfProcessorApi.Models;
using PdfProcessorApi.Services;

namespace PdfProcessorApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IPdfTextExtractorService _pdfTextExtractor;
    private readonly IElasticsearchIndexingService _indexingService;
    private readonly ElasticsearchClient _elasticClient;

    public DocumentsController(
        ApplicationDbContext context, 
        ILogger<DocumentsController> logger, 
        IConfiguration configuration, 
        IPdfTextExtractorService pdfTextExtractor,
        IElasticsearchIndexingService indexingService,
        ElasticsearchClient elasticClient)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _pdfTextExtractor = pdfTextExtractor;
        _indexingService = indexingService;
        _elasticClient = elasticClient;
    }


    [HttpPost]
    public async Task<ActionResult<DocumentMetadata>> UploadAndRegisterDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("File upload failed: No file provided.");
            return BadRequest("No file provided.");
        }   

        const string allowedExtension = ".pdf";
        var fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();

        if (string.IsNullOrEmpty(fileExtension))
        {
            _logger.LogWarning("File upload failed: No file extension found.");
            return BadRequest("No file extension found.");
        }

        if (fileExtension != allowedExtension)
        {
            _logger.LogWarning("File upload failed: Invalid file type. Expected {ExpectedType}, got {ActualType}", allowedExtension, fileExtension);
            return BadRequest($"Invalid file type. Expected {allowedExtension}, got {fileExtension}");
        }

        var maxFileSize = _configuration.GetValue<long?>("FileStorageSettings:MaxFileSizeMB") * 1024 * 1024 ?? (10 * 1024 * 1024);
        if (file.Length > maxFileSize)
        {
            _logger.LogWarning("File upload failed: File size exceeds the limit of {MaxFileSize} MB.", maxFileSize / (1024 * 1024));
            return BadRequest($"File size exceeds the limit of {maxFileSize / (1024 * 1024)} MB.");
        }

        string destinationFilePath;
        try
        {
            var storagePath = GetStoragePath();
            Directory.CreateDirectory(storagePath);

            var uniqueFileName = Guid.NewGuid() + fileExtension;
            destinationFilePath = Path.Combine(storagePath, uniqueFileName);

            _logger.LogInformation("Uploading file: {FileName} to {DestinationPath}", file.FileName, destinationFilePath);

            await using (var fileStream = new FileStream(destinationFilePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            _logger.LogInformation("File uploaded successfully: {FileName}", file.FileName);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File upload failed: {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, "File upload failed. Please try again later.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "File upload failed: {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status403Forbidden, "Permission denied. Please check your access rights.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload failed: {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }

        string? extractedText = null;
        _logger.LogInformation("Extracting text from PDF file: {FileName}", file.FileName);
        try
        {
            extractedText = await _pdfTextExtractor.ExtractTextAsync(destinationFilePath);
            if (extractedText != null)
            {
                _logger.LogInformation("Successfully extracted text from file: {FileName}. Length: {Length}. Preview: '{Preview}...' (max 200 characters)",
                    file.FileName,
                    extractedText.Length,
                    extractedText.Substring(0, Math.Min(extractedText.Length, 200)).Replace("\n", " ").Replace("\r", " ")
                );
            }
            else
            {
                _logger.LogWarning("Text extraction returned null for file: {FileName}", file.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text extraction failed for file: {FileName}", file.FileName);
        }

        var newDocument = new DocumentMetadata()
        {
            OriginalFileName = SanitizeFileName(file.FileName),
            FilePath = destinationFilePath,
        };

        try
        {
            _context.DocumentMetadataEntries.Add(newDocument);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully registered new document metadata. ID: {DocumentId}, FileName: {FileName}", newDocument.Id, newDocument.OriginalFileName);

            if (extractedText != null)
            {
                _logger.LogInformation("Indexing document in Elasticsearch. ID: {DocumentId}", newDocument.Id);

                var elasticDoc = new ElasticDocument
                {
                    Id = newDocument.Id,
                    OriginalFileName = newDocument.OriginalFileName,
                    UploadedAt = newDocument.UploadedAt,
                    ExtractedText = extractedText,
                };

                try
                {
                    bool indexingSuccess = await _indexingService.IndexDocumentAsync(elasticDoc);
                    if (indexingSuccess)
                    {
                        _logger.LogInformation("Document indexed successfully in Elasticsearch. ID: {DocumentId}", newDocument.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Indexing in Elasticsearch encountered an issue for document ID: {DocumentId}", elasticDoc.Id);
                    }
                }
                catch (Exception indexEx)
                {
                    _logger.LogError(indexEx, "Exception occurred while indexing document in Elasticsearch. ID: {DocumentId}", newDocument.Id);
                }
            }
            else
            {
                _logger.LogWarning("No text extracted for document ID: {DocumentId}. Skipping indexing.", newDocument.Id);
            }

            return CreatedAtAction(nameof(GetDocumentMetadataById), new { id = newDocument.Id }, newDocument);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update failed while registering document metadata for file: {FileName}", file.FileName);
            
            try
            {
                if (System.IO.File.Exists(destinationFilePath))
                {
                    System.IO.File.Delete(destinationFilePath);
                    _logger.LogInformation("Deleted file: {FileName} after database update failure.", destinationFilePath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Failed to delete file after database update failure: {FileName}", destinationFilePath);
            }

            return StatusCode(StatusCodes.Status500InternalServerError, "Database update failed. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while registering document metadata for file: {FileName}", file.FileName);

            try
            {
                if (System.IO.File.Exists(destinationFilePath))
                {
                    System.IO.File.Delete(destinationFilePath);
                    _logger.LogInformation("Deleted file: {FileName} after unexpected error.", destinationFilePath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Failed to delete file after unexpected error: {FileName}", destinationFilePath);
            }

            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    private string GetStoragePath()
    {
        var configuredPath = _configuration["FileStorageSettings:BasePath"];

        if (!string.IsNullOrEmpty(configuredPath)) 
            return Path.GetFullPath(configuredPath);

        _logger.LogWarning("File storage path is not configured.");
        throw new InvalidOperationException("File storage path is not configured.");

    }

    private static string SanitizeFileName(string fileName)
    {
        fileName = Path.GetInvalidFileNameChars()
            .Aggregate(fileName, (current, invalidChar) => current.Replace(invalidChar.ToString(), "_"));

        const int maxLen = 250;
        if (fileName.Length <= maxLen) 
            return fileName;

        var ext = Path.GetExtension(fileName);
        fileName = fileName[..(maxLen - ext.Length)] + ext;
        return fileName;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentMetadata>> GetDocumentMetadataById(Guid id)
    {
        var document = await _context.DocumentMetadataEntries.FindAsync(id);

        if (document == null)
        {
            _logger.LogWarning("Document metadata requested but not found. ID: {DocumentId}", id);
            return NotFound();
        }

        return Ok(document);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ElasticDocument>>> SearchDocuments([FromQuery] string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return BadRequest("Search term cannot be empty.");
        }

        _logger.LogInformation("Searching documents in Elasticsearch with term: {SearchTerm}", term);

        try
        {
            var searchResponse = await _elasticClient.SearchAsync<ElasticDocument>(s => s
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.ExtractedText)
                        .Query(term)
                    )
                )
                .Size(20)
            );

            if (searchResponse.IsValidResponse)
            {
                _logger.LogInformation("Elasticsearch search completed successfully. Found {TotalHits} documents for term: '{SearchTerm}'", searchResponse.Total, term);
                return Ok(searchResponse.Documents);
            }
            else
            {
                _logger.LogWarning("Elasticsearch search failed for term: '{SearchTerm}'. DebugInfo: {DebugInfo}", term, searchResponse.DebugInformation);
                if (searchResponse.ElasticsearchServerError != null)
                {
                    _logger.LogError("Elasticsearch server error: {ErrorType} - {ErrorReason}",
                        searchResponse.ElasticsearchServerError.Error?.Type, searchResponse.ElasticsearchServerError.Error?.Reason);
                }
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while searching.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while searching in Elasticsearch for term: '{SearchTerm}'", term);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while searching.");
        }
    }

    public class DocumentMetadataInputModel
    {
        [Required(ErrorMessage = "Original file name is required.")]
        [MaxLength(250)]
        public string OriginalFileName { get; set; } = string.Empty;
    }
}