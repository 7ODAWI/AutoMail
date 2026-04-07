using Abp.AspNetCore.Mvc.Authorization;
using AutoMail.BulkEmail;
using AutoMail.BulkEmail.Dto;
using AutoMail.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutoMail.Web.Controllers
{
    /// <summary>
    /// Handles the Bulk Email sending page.
    /// All AJAX endpoints return JSON; the page itself is a single Razor view.
    /// </summary>
    [AbpMvcAuthorize]
    public class BulkEmailController : AutoMailControllerBase
    {
        private readonly IBulkEmailAppService _bulkEmailAppService;

        public BulkEmailController(IBulkEmailAppService bulkEmailAppService)
        {
            _bulkEmailAppService = bulkEmailAppService;
        }

        // GET /BulkEmail
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> EmailSettings()
        {
            var result = await _bulkEmailAppService.GetEmailSettingsAsync();
            return Json(result);
        }

        // POST /BulkEmail/Upload
        [HttpPost]
        public async Task<JsonResult> Upload(UploadEmailsInput input)
        {
            var result = await _bulkEmailAppService.UploadAndStoreEmailsAsync(input);
            return Json(result);
        }

        // POST /BulkEmail/Send
        [HttpPost]
        public async Task<JsonResult> Send([FromBody] SendBulkEmailInput input)
        {
            await _bulkEmailAppService.EnqueueSendJobAsync(input);
            return Json(new { message = "Email job has been queued successfully." });
        }

        [HttpPost]
        public async Task<JsonResult> SaveEmailSettings([FromBody] UpdateGmailEmailSettingsInput input)
        {
            await _bulkEmailAppService.UpdateEmailSettingsAsync(input);
            return Json(new { message = "Gmail SMTP settings saved successfully." });
        }
    }
}
