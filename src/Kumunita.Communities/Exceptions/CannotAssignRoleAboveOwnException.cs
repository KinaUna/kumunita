namespace Kumunita.Communities.Exceptions;

public class CannotAssignRoleAboveOwnException()
    : Exception("You cannot assign a role higher than your own.");