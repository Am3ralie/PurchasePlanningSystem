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
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId) || productIds == null || productIds.Count == 0)
                return RedirectToAction("Login", "Auth");

            var number = "PR-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            int requestId = 0;

            // Используем транзакцию для целостности
            using (var connection = DatabaseHelper.GetOpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // 1. Создаём заявку
                    var sqlRequest = @"
                        INSERT INTO PurchaseRequests (Number, Date, Status, CreatedByUserId) 
                        VALUES (@Number, NOW(), 'Draft', @UserId);
                        SELECT LAST_INSERT_ID();";

                    using (var cmd = new MySqlCommand(sqlRequest, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Number", number);
                        cmd.Parameters.AddWithValue("@UserId", Convert.ToInt32(userId));
                        requestId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 2. Добавляем все строки заявки
                    for (int i = 0; i < productIds.Count; i++)
                    {
                        var sqlItem = @"
                            INSERT INTO PurchaseRequestItems (RequestId, ProductId, Quantity, RequiredDate) 
                            VALUES (@RequestId, @ProductId, @Quantity, @RequiredDate)";

                        using (var cmd = new MySqlCommand(sqlItem, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@RequestId", requestId);
                            cmd.Parameters.AddWithValue("@ProductId", productIds[i]);
                            cmd.Parameters.AddWithValue("@Quantity", quantities[i]);
                            cmd.Parameters.AddWithValue("@RequiredDate", dates[i].ToString("yyyy-MM-dd"));
                            cmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            // РЕДИРЕКТ НА DETAILS С ID НОВОЙ ЗАЯВКИ
            return RedirectToAction("Details", new { id = requestId });
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