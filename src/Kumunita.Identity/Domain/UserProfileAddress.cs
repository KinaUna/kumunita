namespace Kumunita.Identity.Domain;

public record UserProfileAddress(
    string Street,
    string City,
    string PostalCode,
    string Country);