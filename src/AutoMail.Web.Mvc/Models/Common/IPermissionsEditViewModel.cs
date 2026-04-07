using AutoMail.Roles.Dto;
using System.Collections.Generic;

namespace AutoMail.Web.Models.Common;

public interface IPermissionsEditViewModel
{
    List<FlatPermissionDto> Permissions { get; set; }
}