using Abp.Authorization;
using AutoMail.Authorization.Roles;
using AutoMail.Authorization.Users;

namespace AutoMail.Authorization;

public class PermissionChecker : PermissionChecker<Role, User>
{
    public PermissionChecker(UserManager userManager)
        : base(userManager)
    {
    }
}
