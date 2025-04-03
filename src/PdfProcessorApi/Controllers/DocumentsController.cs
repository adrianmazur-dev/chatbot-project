using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfProcessorApi.Data;
using PdfProcessorApi.Models;

namespace PdfProcessorApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DocumentsController(ApplicationDbContext context, ILogger<DocumentsController> logger, IConfiguration configuration) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<DocumentsController> _logger = logger;
    private readonly IConfiguration _configuration = configuration;


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


    public class DocumentMetadataInputModel
    {
        [Required(ErrorMessage = "Original file name is required.")]
        [MaxLength(250)]
        public string OriginalFileName { get; set; } = string.Empty;
    }
}