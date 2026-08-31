using Intably.Domain.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Intably.Api.Authentication;

public sealed record ActiveUserRequirement : IAuthorizationRequirement;

public sealed record PermissionRequirement(ApplicationPermission Permission)
    : IAuthorizationRequirement;
