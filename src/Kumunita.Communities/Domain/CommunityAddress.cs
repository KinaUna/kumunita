namespace Kumunita.Communities.Domain;

public record CommunityAddress(
    string Street,
    string City,
    string PostalCode,
    string Country);