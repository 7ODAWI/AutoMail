using AutoMail.BulkEmail;
using AutoMail.BulkEmail.Dto;
using AutoMail.Controllers;
using Abp.UI;
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

        [HttpPost]
        public async Task<JsonResult> Pause([FromBody] OperationControlInput input)
        {
            await _operationAppService.PauseOperationAsync(GetRequiredOperationId(input));
            return Json(new { message = "Operation paused." });
        }

        [HttpPost]
        public async Task<JsonResult> Stop([FromBody] OperationControlInput input)
        {
            await _operationAppService.StopOperationAsync(GetRequiredOperationId(input));
            return Json(new { message = "Operation stopped." });
        }

        [HttpPost]
        public async Task<JsonResult> Reactivate([FromBody] OperationControlInput input)
        {
            await _operationAppService.ReactivateOperationAsync(GetRequiredOperationId(input));
            return Json(new { message = "Operation reactivated and job re-queued." });
        }

        [HttpPost]
        public async Task<JsonResult> CompleteSend([FromBody] OperationControlInput input)
        {
            await _operationAppService.CompleteUnsentEmailsAsync(GetRequiredOperationId(input));
            return Json(new { message = "Unsent emails were queued for completion." });
        }

        [HttpPost]
        public async Task<JsonResult> CloneOperation([FromBody] OperationControlInput input)
        {
            var result = await _operationAppService.CloneOperationAsync(GetRequiredOperationId(input));
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> Start([FromBody] OperationControlInput input)
        {
            await _operationAppService.StartOperationAsync(GetRequiredOperationId(input));
            return Json(new { message = "Operation started." });
        }

        [HttpPost]
        public async Task<JsonResult> DeleteOperation([FromBody] OperationControlInput input)
        {
            await _operationAppService.DeleteOperationAsync(GetRequiredOperationId(input));
            return Json(new { message = "Operation deleted." });
        }

        private static long GetRequiredOperationId(OperationControlInput input)
        {
            if (input == null || input.Id <= 0)
                throw new UserFriendlyException("Invalid operation id.");

            return input.Id;
        }

        public ActionResult EditPage(long id)
        {
            ViewBag.OperationId = id;
            return View("Edit");
        }

        [HttpPost]
        public async Task<JsonResult> UpdateOperation(UpdateOperationInput input)
        {
            await _operationAppService.UpdateOperationAsync(input);
            return Json(new { message = "Operation updated successfully." });
        }

        [HttpPost]
        public async Task<JsonResult> AddTemplate([FromBody] CreateTemplateInput input)
        {
            var result = await _operationAppService.AddTemplateAsync(input);
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> UpdateTemplate([FromBody] UpdateTemplateInput input)
        {
            var result = await _operationAppService.UpdateTemplateAsync(input);
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> DeleteTemplate([FromBody] DeleteTemplateInput input)
        {
            await _operationAppService.DeleteTemplateAsync(input.Id);
            return Json(new { message = "Template deleted." });
        }

        [HttpPost]
        public async Task<JsonResult> GenerateAiTemplates([FromBody] GenerateAiTemplatesInput input)
        {
            var result = await _operationAppService.GenerateAiTemplatesAsync(input);
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> StopAiGeneration([FromBody] OperationControlInput input)
        {
            await _operationAppService.StopAiGenerationAsync(GetRequiredOperationId(input));
            return Json(new { message = "AI generation stopped. You can start sending now." });
        }
    }

    public class RetryOperationInput
    {
        public long Id { get; set; }
    }

    public class OperationControlInput
    {
        public long Id { get; set; }
    }

    public class DeleteTemplateInput
    {
        public long Id { get; set; }
    }
}
