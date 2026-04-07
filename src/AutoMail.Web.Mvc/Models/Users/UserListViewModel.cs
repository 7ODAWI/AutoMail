using AutoMail.Roles.Dto;
using System.Collections.Generic;

namespace AutoMail.Web.Models.Users;

public class UserListViewModel
{
    public IReadOnlyList<RoleDto> Roles { get; set; }
}
