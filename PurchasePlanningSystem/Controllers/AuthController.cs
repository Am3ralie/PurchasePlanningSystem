using Microsoft.AspNetCore.Mvc;

public class AuthController : Controller
{
    public IActionResult Login() => View();

    [HttpPost]
    public IActionResult Login(string login, string password)
    {
        // Упрощённая проверка - прямо по твоим тестовым данным!
        if ((login == "admin" && password == "q1111") ||
            (login == "manager1" && password == "w2222"))
        {
            HttpContext.Session.SetString("UserId", login == "admin" ? "1" : "2");
            HttpContext.Session.SetString("UserRole", login == "admin" ? "Admin" : "Manager");
            return RedirectToAction("Index", "PurchaseRequests");
        }
        ViewBag.Error = "Неверный логин или пароль";
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}