namespace API.DTOs;

public class EbayPolicySetupResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool NeedsManualEnrollment { get; set; }
    public string? FulfillmentPolicyId { get; set; }
    public string? ReturnPolicyId { get; set; }
}