global using StronglyTypedIds;

// Set the default template for all IDs in this assembly:
// Template.Guid = built-in Guid backing
// "guid-efcore" = EF Core ValueConverter from StronglyTypedId.Templates
[assembly: StronglyTypedIdDefaults(Template.Guid, "guid-efcore")]