using Microsoft.AspNetCore.Identity;

namespace job_application_tracker_api.Auth;

public sealed class SwedishIdentityErrors : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => Error("DuplicateEmail", "Ett konto med de här uppgifterna kan inte skapas.");
    public override IdentityError DuplicateUserName(string userName) => Error("DuplicateUserName", "Ett konto med de här uppgifterna kan inte skapas.");
    public override IdentityError InvalidEmail(string? email) => Error("InvalidEmail", "Ange en giltig mejladress.");
    public override IdentityError PasswordTooShort(int length) => Error("PasswordTooShort", $"Lösenordet måste vara minst {length} tecken.");
    public override IdentityError PasswordRequiresDigit() => Error("PasswordRequiresDigit", "Lösenordet måste innehålla en siffra.");
    public override IdentityError PasswordRequiresLower() => Error("PasswordRequiresLower", "Lösenordet måste innehålla en liten bokstav.");
    public override IdentityError PasswordRequiresUpper() => Error("PasswordRequiresUpper", "Lösenordet måste innehålla en stor bokstav.");
    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
