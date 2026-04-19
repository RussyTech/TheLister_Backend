namespace API.Services.Interfaces;

public interface IEbayImageService
{
    Task<string?> UploadImageAsync(string userId, Stream imageStream, string fileName, string contentType);
}