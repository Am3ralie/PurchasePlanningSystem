using Microsoft.AspNetCore.Mvc;

public class PurchaseRequestsController : Controller
{
    public IActionResult Index()
    {
        // Проверка авторизации
        if (HttpContext.Session.GetString("UserId") == null)
            return RedirectToAction("Login", "Auth");

        ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
        return View();
    }
}