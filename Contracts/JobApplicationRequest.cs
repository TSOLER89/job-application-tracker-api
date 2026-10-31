using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace job_application_tracker_api.Contracts;

public sealed class JobApplicationRequest : IValidatableObject
{
    [Required(ErrorMessage = "Företag måste fyllas i.")]
    [StringLength(200, ErrorMessage = "Företag får vara högst 200 tecken.")]
    public string Company { get; init; } = string.Empty;

    [Required(ErrorMessage = "Tjänst måste fyllas i.")]
    [StringLength(200, ErrorMessage = "Tjänst får vara högst 200 tecken.")]
    public string Position { get; init; } = string.Empty;

    [Required(ErrorMessage = "Plats måste fyllas i.")]
    [StringLength(200, ErrorMessage = "Plats får vara högst 200 tecken.")]
    public string Location { get; init; } = string.Empty;

    public DateOnly? DateApplied { get; init; }

    [Required(ErrorMessage = "Status måste fyllas i.")]
    public string Status { get; init; } = "Ansökt";

    [StringLength(5000, ErrorMessage = "Anteckningar får vara högst 5000 tecken.")]
    public string? Notes { get; init; }

    [StringLength(2048, ErrorMessage = "Bildens adress får vara högst 2048 tecken.")]
    public string? ImageUrl { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        string[] statuses = ["Intresserad", "Ansökt", "Intervju", "Erbjudande", "Avslag"];
        if (!statuses.Contains(Status))
        {
            yield return new ValidationResult("Välj en giltig status.", [nameof(Status)]);
        }

        if (!string.IsNullOrEmpty(ImageUrl) &&
            !Regex.IsMatch(ImageUrl, @"^/uploads/[a-zA-Z0-9_-]+\.(jpg|jpeg|png|webp)$", RegexOptions.IgnoreCase) &&
            !(Uri.TryCreate(ImageUrl, UriKind.Absolute, out var uri) &&
              (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            yield return new ValidationResult(
                "Ange en uppladdad bilds sökväg eller en HTTP/HTTPS-adress.", [nameof(ImageUrl)]);
        }
    }
}
