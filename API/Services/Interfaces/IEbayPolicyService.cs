using API.DTOs;

namespace API.Services.Interfaces;

public interface IEbayPolicyService
{
    Task<EbayPoliciesDto> GetPoliciesAsync(string userId);
    Task<EbayPolicySetupResultDto> SetupDefaultPoliciesAsync(string userId);
    Task<List<EbayCategorySuggestionDto>> GetCategorySuggestionsAsync(string userId, string query);
    Task<List<EbayCategoryAspectDto>> GetCategoryAspectsAsync(string userId, string categoryId);
    Task<List<EbayCategoryConditionDto>> GetCategoryConditionsAsync(string userId, string categoryId);
}