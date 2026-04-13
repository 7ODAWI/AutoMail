using AutoMail.BulkEmail;
using AutoMail.BulkEmail.Dto;
using AutoMail.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutoMail.Web.Controllers
{
    public class BulkEmailController : AutoMailControllerBase
    {
        private readonly IEmailOperationAppService _operationAppService;

        public BulkEmailController(IEmailOperationAppService operationAppService)
        {
            _operationAppService = operationAppService;
        }

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> CreateOperation(CreateOperationInput input)
        {
            var result = await _operationAppService.CreateOperationAsync(input);
            return Json(result);
        }

        [HttpGet]
        public async Task<JsonResult> GetAll()
        {
            var result = await _operationAppService.GetAllOperationsAsync();
            return Json(result);
        }

        [HttpGet]
        public async Task<JsonResult> Detail(long id)
        {
            var result = await _operationAppService.GetOperationDetailAsync(id);
            return Json(result);
        }

        public async Task<ActionResult> DetailPage(long id)
        {
            ViewBag.OperationId = id;
            return View("Detail");
        }

        [HttpPost]
        public async Task<JsonResult> Retry([FromBody] RetryOperationInput input)
        {
            await _operationAppService.RetryFailedEmailsAsync(input.Id);
            return Json(new { message = "Retry job has been queued successfully." });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFailed(long id)
        {
            var csvBytes = await _operationAppService.ExportFailedEmailsCsvAsync(id);
            return File(csvBytes, "text/csv", $"failed-emails-operation-{id}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportDistinctEmails()
        {
            var excelBytes = await _operationAppService.ExportDistinctEmailsExcelAsync();
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "distinct-emails.xlsx");
        }
    }

    public class RetryOperationInput
    {
        public long Id { get; set; }
    }
}
