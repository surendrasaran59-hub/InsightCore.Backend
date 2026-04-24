using Microsoft.AspNetCore.Mvc;

namespace InsightCore.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
