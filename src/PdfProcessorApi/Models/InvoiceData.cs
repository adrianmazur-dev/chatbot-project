using System;
using System.Text.Json.Serialization;

namespace PdfProcessorApi.Models;

public class InvoiceData
{
    [JsonPropertyName("InvoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("InvoiceDate")]
    public string? InvoiceDate { get; set; }

    [JsonPropertyName("VendorName")]
    public string? VendorName { get; set; }

    [JsonPropertyName("CustomerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("NetAmount")]
    public decimal? NetAmount { get; set; }

    [JsonPropertyName("TaxAmount")]
    public decimal? TaxAmount { get; set; }

    [JsonPropertyName("GrossAmount")]
    public decimal? GrossAmount { get; set; }
}