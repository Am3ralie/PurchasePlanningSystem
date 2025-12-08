using Microsoft.AspNetCore.Mvc;
using PurchasePlanningSystem.Utils;
using MySql.Data.MySqlClient;
using System.Data;

namespace PurchasePlanningSystem.Controllers
{
    public class AdminController : Controller
    {
        // Проверка прав админа
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("UserRole") == "Admin";
        }

        // 1. СПИСОК ПОЛЬЗОВАТЕЛЕЙ
        public IActionResult Users()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var users = DatabaseHelper.GetDataTable(
                "SELECT Id, Login, FullName, Role, IsActive FROM Users ORDER BY Id");
            return View(users);
        }

        // 2. ФОРМА СОЗДАНИЯ
        public IActionResult CreateUser()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            return View();
        }

        [HttpPost]
        public IActionResult CreateUser(string login, string password, string fullName, string role, bool isActive = true)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // В реальном приложении - BCrypt!
            DatabaseHelper.ExecuteNonQuery(
                @"INSERT INTO Users (Login, PasswordHash, FullName, Role, IsActive) 
                  VALUES (@Login, @Pass, @Name, @Role, @Active)",
                new MySqlParameter("@Login", login),
                new MySqlParameter("@Pass", password), // ХЭШИРОВАТЬ В ПРОДАКШЕНЕ!
                new MySqlParameter("@Name", fullName),
                new MySqlParameter("@Role", role),
                new MySqlParameter("@Active", isActive)
            );

            return RedirectToAction("Users");
        }

        // 3. ФОРМА РЕДАКТИРОВАНИЯ
        public IActionResult EditUser(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var user = DatabaseHelper.GetDataTable(
                "SELECT * FROM Users WHERE Id = @Id",
                new MySqlParameter("@Id", id));

            if (user.Rows.Count == 0) return NotFound();

            ViewBag.User = user.Rows[0];
            return View();
        }

        [HttpPost]
        public IActionResult EditUser(int id, string login, string fullName, string role, bool isActive)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            DatabaseHelper.ExecuteNonQuery(
                @"UPDATE Users SET 
                    Login = @Login, 
                    FullName = @Name, 
                    Role = @Role, 
                    IsActive = @Active 
                  WHERE Id = @Id",
                new MySqlParameter("@Id", id),
                new MySqlParameter("@Login", login),
                new MySqlParameter("@Name", fullName),
                new MySqlParameter("@Role", role),
                new MySqlParameter("@Active", isActive)
            );

            return RedirectToAction("Users");
        }

        // 4. УДАЛЕНИЕ (с подтверждением)
        public IActionResult DeleteUser(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // Не даём удалить себя
            var currentUserId = HttpContext.Session.GetString("UserId");
            if (currentUserId == id.ToString())
            {
                TempData["Error"] = "Нельзя удалить самого себя!";
                return RedirectToAction("Users");
            }

            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM Users WHERE Id = @Id",
                new MySqlParameter("@Id", id));

            return RedirectToAction("Users");
        }
    }
}