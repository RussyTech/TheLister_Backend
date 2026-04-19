using API.DTOs;

namespace API.Services.Interfaces;

public interface IEbayListingService
{
    Task<CreateListingResultDto> CreateListingAsync(string userId, CreateListingDto dto);
    
}