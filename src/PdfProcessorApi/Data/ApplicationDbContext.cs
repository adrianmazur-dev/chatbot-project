using Microsoft.EntityFrameworkCore;
using PdfProcessorApi.Models;

namespace PdfProcessorApi.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentMetadata> DocumentMetadataEntries { get; set; }
}
