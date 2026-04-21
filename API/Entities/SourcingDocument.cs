namespace API.Entities;

public class SourcingDocument
{
    public int    Id           { get; set; }
    public required string UserId       { get; set; }
    public required string FileName     { get; set; }
    public required byte[] FileContent  { get; set; }
    public DateTime UploadedAt  { get; set; } = DateTime.UtcNow;
    public int    ProductCount { get; set; }

    public ApplicationUser User { get; set; } = null!;
}