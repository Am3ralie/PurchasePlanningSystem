using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using PurchasePlanningSystem.Models;
using PurchasePlanningSystem.Utils;
using System.Data;

namespace PurchasePlanningSystem.Controllers
{
    public class PurchaseRequestsController : Controller
    {
        public IActionResult Index()
        {
            // Проверка авторизации
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Получаем заявки из БД
            var sql = @"
                SELECT pr.Id, pr.Number, pr.Date, pr.Status, u.FullName as CreatedBy
                FROM PurchaseRequests pr
                LEFT JOIN Users u ON pr.CreatedByUserId = u.Id
                ORDER BY pr.Date DESC
                LIMIT 50";

            var dataTable = DatabaseHelper.GetDataTable(sql);
            var requests = new System.Collections.Generic.List<PurchaseRequestViewModel>();

            foreach (DataRow row in dataTable.Rows)
            {
                requests.Add(new PurchaseRequestViewModel
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Number = row["Number"].ToString(),
                    Date = Convert.ToDateTime(row["Date"]),
                    Status = row["Status"].ToString(),
                    CreatedBy = row["CreatedBy"].ToString()
                });
            }

            ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
            return View(requests);
        }

        // GET: Создание заявки
        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Получаем список продуктов для выпадающего списка
            var products = DatabaseHelper.GetDataTable("SELECT Id, Name, Unit FROM Products");
            ViewBag.Products = products;

            return View();
        }

        // POST: Создание заявки (упрощённая версия - один товар)
        [HttpPost]
        public IActionResult Create(int productId, decimal quantity, DateTime requiredDate)
        {
            var userId = HttpContext.Session.GetString("UserId");
            var number = "PR-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

            // 1. Создаём заявку
            var sqlRequest = @"
                INSERT INTO PurchaseRequests (Number, Date, Status, CreatedByUserId) 
                VALUES (@Number, NOW(), 'Draft', @UserId);
                SELECT LAST_INSERT_ID();";

            var requestId = DatabaseHelper.ExecuteScalar(sqlRequest,
                new MySqlParameter("@Number", number),
                new MySqlParameter("@UserId", Convert.ToInt32(userId)));

            // 2. Добавляем строку в заявку
            if (requestId != null)
            {
                var sqlItem = @"
                    INSERT INTO PurchaseRequestItems (RequestId, ProductId, Quantity, RequiredDate) 
                    VALUES (@RequestId, @ProductId, @Quantity, @RequiredDate)";

                DatabaseHelper.ExecuteNonQuery(sqlItem,
                    new MySqlParameter("@RequestId", requestId),
                    new MySqlParameter("@ProductId", productId),
                    new MySqlParameter("@Quantity", quantity),
                    new MySqlParameter("@RequiredDate", requiredDate));
            }

            // Редирект на список заявок
            return RedirectToAction("Index");
        }

        public IActionResult Details(int id)
        {
            var sql = @"
        SELECT pr.*, u.FullName as CreatedByName 
        FROM PurchaseRequests pr
        LEFT JOIN Users u ON pr.CreatedByUserId = u.Id
        WHERE pr.Id = @Id";

            var dataTable = DatabaseHelper.GetDataTable(sql, new MySqlParameter("@Id", id));

            if (dataTable.Rows.Count == 0)
                return NotFound();

            ViewBag.Request = dataTable.Rows[0];

            // Получаем строки заявки
            var itemsSql = @"
        SELECT pri.*, p.Name as ProductName, p.Unit
        FROM PurchaseRequestItems pri
        LEFT JOIN Products p ON pri.ProductId = p.Id
        WHERE pri.RequestId = @RequestId";

            ViewBag.Items = DatabaseHelper.GetDataTable(itemsSql, new MySqlParameter("@RequestId", id));

            return View();
        }
    }
}