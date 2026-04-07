using Abp.AspNetCore.Mvc.Authorization;
using AutoMail.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace AutoMail.Web.Controllers;

[AbpMvcAuthorize]
public class HomeController : AutoMailControllerBase
{
    public ActionResult Index()
    {
        return View();
    }
}
