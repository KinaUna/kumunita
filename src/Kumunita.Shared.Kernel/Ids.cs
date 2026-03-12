namespace Kumunita.Shared.Kernel;

// Identity module
[StronglyTypedId] public partial struct UserId { }
[StronglyTypedId] public partial struct RoleId { }
[StronglyTypedId] public partial struct GroupId { }
[StronglyTypedId] public partial struct PermissionId { }

// Authorization module
[StronglyTypedId] public partial struct CapabilityTokenId { }

// Localization module
[StronglyTypedId] public partial struct TranslationKeyId { }
[StronglyTypedId] public partial struct TranslationId { }

// Communities module
[StronglyTypedId(Template.Guid, "guid-efcore")]
public partial struct CommunityId { }

[StronglyTypedId(Template.Guid, "guid-efcore")]
public partial struct CommunityMembershipId { }

[StronglyTypedId(Template.Guid, "guid-efcore")]
public partial struct CommunityInvitationId { }

// Announcements module
[StronglyTypedId] public partial struct AnnouncementId { }