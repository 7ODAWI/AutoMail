using Abp.AspNetCore.Mvc.Authorization;
using AutoMail.BulkEmail;
using AutoMail.BulkEmail.Dto;
using AutoMail.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutoMail.Web.Controllers
{
    public class BulkEmailController : AutoMailControllerBase
    {
        private readonly IBulkEmailAppService _bulkEmailAppService;

        public BulkEmailController(IBulkEmailAppService bulkEmailAppService)
        {
            _bulkEmailAppService = bulkEmailAppService;
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> Upload(UploadEmailsInput input)
        {
            var result = await _bulkEmailAppService.UploadAndStoreEmailsAsync(input);
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> Send([FromBody] SendBulkEmailInput input)
        {
            await _bulkEmailAppService.EnqueueSendJobAsync(input);
            return Json(new { message = "Email job has been queued successfully." });
        }

        [HttpGet]
        public async Task<JsonResult> Dashboard()
        {
            var result = await _bulkEmailAppService.GetDashboardStatsAsync();
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFailedEmails()
        {
            var csvBytes = await _bulkEmailAppService.ExportFailedEmailsCsvAsync();
            return File(csvBytes, "text/csv", "failed-emails.csv");
        }

        [HttpPost]
        public async Task<JsonResult> RetryFailedEmails([FromBody] SendBulkEmailInput input)
        {
            await _bulkEmailAppService.RetryFailedEmailsAsync(input);
            return Json(new { message = "Retry job has been queued successfully." });
        }
    }
}
