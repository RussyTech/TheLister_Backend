using API.DTOs;

namespace API.Services.Interfaces;

public interface IEbayInventoryService
{
    Task<EbayInventoryResultDto> GetInventoryAsync(string userId, int limit, int offset);
}