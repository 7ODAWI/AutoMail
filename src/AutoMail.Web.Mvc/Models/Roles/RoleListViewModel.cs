using AutoMail.Roles.Dto;
using System.Collections.Generic;

namespace AutoMail.Web.Models.Roles;

public class RoleListViewModel
{
    public IReadOnlyList<PermissionDto> Permissions { get; set; }
}
