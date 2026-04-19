using API.DTOs;
using Microsoft.AspNetCore.Http;

namespace API.Services.Interfaces;

public interface ISourcingService
{
    Task<SourcingUploadResponseDto> ParseSpreadsheetAsync(IFormFile file);
}