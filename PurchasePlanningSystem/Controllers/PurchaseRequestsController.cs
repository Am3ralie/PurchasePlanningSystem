using Microsoft.AspNetCore.Mvc;
using PurchasePlanningSystem.Utils;
using System.Data;
using MySql.Data.MySqlClient;
using PurchasePlanningSystem.Models;

namespace PurchasePlanningSystem.Controllers
{
    public class PurchaseRequestsController : Controller
    {
        // GET: /PurchaseRequests
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            var sql = @"
                SELECT pr.Id, pr.Number, pr.Date, pr.Status, u.FullName as CreatedBy
                FROM PurchaseRequests pr
                LEFT JOIN Users u ON pr.CreatedByUserId = u.Id
                ORDER BY pr.Date DESC
                LIMIT 50";

            var dataTable = DatabaseHelper.GetDataTable(sql);
            var requests = new List<PurchaseRequestViewModel>();

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

        // GET: /PurchaseRequests/Create
        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            var products = DatabaseHelper.GetDataTable("SELECT Id, Name, Unit FROM Products");
            ViewBag.Products = products;

            return View();
        }

        // POST: /PurchaseRequests/Create
        [HttpPost]
        public IActionResult Create(List<int> productIds, List<decimal> quantities, List<DateTime> dates)
        {
            try
            {
                // 1. Проверка авторизации
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Auth");
                }

                // 2. Получаем продукты из БД
                string sql = "SELECT Id, Name, Unit FROM Products WHERE 1";
                var productsTable = DatabaseHelper.GetDataTable(sql);

                // 3. Проверяем, что данные получены
                if (productsTable == null || productsTable.Rows.Count == 0)
                {
                    ViewBag.Error = "В базе нет продуктов. Обратитесь к администратору.";
                }

                ViewBag.Products = productsTable;
                return View();
            }
            catch (Exception ex)
            {
                // В режиме отладки показываем ошибку
                return Content($"Ошибка при загрузке формы: {ex.Message}<br>Убедитесь, что БД запущена и таблица Products существует.");
            }
        }

        // GET: /PurchaseRequests/Details/{id}
        public IActionResult Details(int id)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

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

        // GET: /PurchaseRequests/CreateOrder?requestId=5
        public IActionResult CreateOrder(int requestId)
        {
            // Заглушка - вернёмся к этому позже
            return Content($"Заказ из заявки {requestId} будет создан здесь");
        }
    }
}