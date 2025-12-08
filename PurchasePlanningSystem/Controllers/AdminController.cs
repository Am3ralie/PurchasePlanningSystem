using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using PurchasePlanningSystem.Utils;

public class AdminController : Controller
{
    public IActionResult Users()
    {
        // Проверка, что вошёл админ
        if (HttpContext.Session.GetString("UserRole") != "Admin")
            return RedirectToAction("Login", "Auth");

        var users = DatabaseHelper.GetDataTable("SELECT * FROM Users ORDER BY Id");
        return View(users);
    }

    public IActionResult CreateUser()
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
            return RedirectToAction("Login", "Auth");
        return View();
    }

    [HttpPost]
    public IActionResult CreateUser(string login, string password, string fullName, string role)
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin")
            return RedirectToAction("Login", "Auth");

        // В реальном приложении здесь хэширование пароля!
        DatabaseHelper.ExecuteNonQuery(
            "INSERT INTO Users (Login, PasswordHash, FullName, Role) VALUES (@Login, @Pass, @Name, @Role)",
            new MySqlParameter("@Login", login),
            new MySqlParameter("@Pass", password),
            new MySqlParameter("@Name", fullName),
            new MySqlParameter("@Role", role)
        );

        return RedirectToAction("Users");
    }
}