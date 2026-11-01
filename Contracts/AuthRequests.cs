using System.ComponentModel.DataAnnotations;

namespace job_application_tracker_api.Contracts;

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Fyll i mejladressen."), EmailAddress(ErrorMessage = "Ange en giltig mejladress."), StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Fyll i lösenordet."), StringLength(128, MinimumLength = 10,
        ErrorMessage = "Lösenordet ska vara 10–128 tecken.")]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Fyll i mejladressen."), EmailAddress(ErrorMessage = "Ange en giltig mejladress."), StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Fyll i lösenordet."), StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed record AccountResponse(string Id, string Email);
