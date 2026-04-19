using API.DTOs;

namespace API.Services.Interfaces;

public interface IAmazonScrapeService
{
    Task<AmazonProductDto> ScrapeAsync(string url);
}