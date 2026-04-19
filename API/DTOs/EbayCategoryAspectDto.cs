namespace API.DTOs;

public class EbayCategoryAspectDto
{
    public string Name { get; set; } = "";
    public bool Required { get; set; }
    public List<string> AllowedValues { get; set; } = [];
}