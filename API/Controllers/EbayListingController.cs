using System.Security.Claims;
using API.DTOs;
using API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/ebay")]
[Authorize]
public class EbayListingController : ControllerBase
{
    private readonly IEbayListingService _listingService;
    private readonly IEbayPolicyService _policyService;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public EbayListingController(IEbayListingService listingService, IEbayPolicyService policyService)
    {
        _listingService = listingService;
        _policyService = policyService;
    }

    [HttpGet("listings/policies")]
    public async Task<IActionResult> GetPolicies()
    {
        var result = await _policyService.GetPoliciesAsync(UserId);
        return Ok(result);
    }

    [HttpPost("listings/policies/setup")]
    public async Task<IActionResult> SetupDefaultPolicies()
    {
        var result = await _policyService.SetupDefaultPoliciesAsync(UserId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("listings")]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _listingService.CreateListingAsync(UserId, dto);
        if (!result.Success) return BadRequest(new { error = result.Error });
        return Ok(result);
    }

    [HttpGet("categories/suggest")]
    public async Task<IActionResult> SuggestCategories([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new { categorySuggestions = Array.Empty<object>() });
        var result = await _policyService.GetCategorySuggestionsAsync(UserId, q);
        return Ok(result);
    }

    [HttpGet("categories/{categoryId}/aspects")]
    public async Task<IActionResult> GetCategoryAspects(string categoryId)
    {
        var aspects = await _policyService.GetCategoryAspectsAsync(UserId, categoryId);
        return Ok(aspects);
    }

    [HttpGet("categories/{categoryId}/conditions")]
    public async Task<IActionResult> GetCategoryConditions(string categoryId)
    {
        var conditions = await _policyService.GetCategoryConditionsAsync(UserId, categoryId);
        return Ok(conditions);
    }
}