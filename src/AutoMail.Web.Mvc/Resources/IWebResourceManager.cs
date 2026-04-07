using Microsoft.AspNetCore.Mvc.Razor;
using System.Collections.Generic;

namespace AutoMail.Web.Resources;

public interface IWebResourceManager
{
    void AddScript(string url, bool addMinifiedOnProd = true);

    IReadOnlyList<string> GetScripts();

    HelperResult RenderScripts();
}
