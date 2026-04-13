using AutoMail.BulkEmail;
using AutoMail.BulkEmail.Dto;
using AutoMail.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutoMail.Web.Controllers
{
    public class EmailSenderController : AutoMailControllerBase
    {
        private readonly IEmailSenderAppService _emailSenderAppService;

        public EmailSenderController(IEmailSenderAppService emailSenderAppService)
        {
            _emailSenderAppService = emailSenderAppService;
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetAll()
        {
            var result = await _emailSenderAppService.GetAllAsync();
            return Json(result);
        }

        [HttpGet]
        public async Task<JsonResult> Get(int id)
        {
            var result = await _emailSenderAppService.GetAsync(id);
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> Create([FromBody] CreateEmailSenderInput input)
        {
            var result = await _emailSenderAppService.CreateAsync(input);
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> Update([FromBody] UpdateEmailSenderInput input)
        {
            var result = await _emailSenderAppService.UpdateAsync(input);
            return Json(result);
        }

        [HttpPost]
        public async Task<JsonResult> Delete([FromBody] DeleteSenderInput input)
        {
            await _emailSenderAppService.DeleteAsync(input.Id);
            return Json(new { message = "Sender deleted successfully." });
        }
    }

    public class DeleteSenderInput
    {
        public int Id { get; set; }
    }
}
