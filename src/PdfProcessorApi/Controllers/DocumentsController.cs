using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfProcessorApi.Data;
using PdfProcessorApi.Models;

namespace PdfProcessorApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DocumentsController(ApplicationDbContext context, ILogger<DocumentsController> logger)
    : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<DocumentsController> _logger = logger;

    [HttpPost]
    public async Task<ActionResult<DocumentMetadata>> RegisterDocumentMetadata(DocumentMetadataInputModel input)
    {
        var newDocument = new DocumentMetadata()
        {
            OriginalFileName = input.OriginalFileName,
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
            _logger.LogError(ex, "Database error occurred while registering document metadata for file: {FileName}", input.OriginalFileName);
            return StatusCode(StatusCodes.Status500InternalServerError, "A database error occurred. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while registering document metadata for file: {FileName}", input.OriginalFileName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
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