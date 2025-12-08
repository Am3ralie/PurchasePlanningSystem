using Microsoft.AspNetCore.Mvc;
using PurchasePlanningSystem.Utils;
using MySql.Data.MySqlClient;
using System.Data;

namespace PurchasePlanningSystem.Controllers
{
    public class PurchaseRequestsController : Controller
    {
        // 1. СПИСОК ЗАЯВОК
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            var sql = @"
                SELECT pr.Id, pr.Number, pr.Date, pr.Status, 
                       u.FullName as CreatedBy,
                       pr.CreatedByUserId
                FROM PurchaseRequests pr
                LEFT JOIN Users u ON pr.CreatedByUserId = u.Id
                ORDER BY pr.Date DESC";

            ViewBag.Requests = DatabaseHelper.GetDataTable(sql);
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
            ViewBag.UserId = HttpContext.Session.GetString("UserId");

            return View();
        }

        // 2. ФОРМА СОЗДАНИЯ
        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            ViewBag.Products = DatabaseHelper.GetDataTable("SELECT Id, Name, Unit FROM Products");
            return View();
        }

        // 3. СОХРАНЕНИЕ ЗАЯВКИ
        [HttpPost]
        public IActionResult Create(int productIds, decimal quantities, DateTime dates)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            var number = "PR-" + DateTime.Now.ToString("yyyyMMdd-HHmm");

            // 1. Вставляем заявку
            var sqlRequest = @"
                INSERT INTO PurchaseRequests (Number, Date, Status, CreatedByUserId) 
                VALUES (@Number, NOW(), 'Draft', @UserId);
                SELECT LAST_INSERT_ID();";

            var requestId = DatabaseHelper.ExecuteScalar(sqlRequest,
                new MySqlParameter("@Number", number),
                new MySqlParameter("@UserId", int.Parse(userId)));

            // 2. Вставляем строку
            var sqlItem = @"
                INSERT INTO PurchaseRequestItems (RequestId, ProductId, Quantity, RequiredDate) 
                VALUES (@RequestId, @ProductId, @Quantity, @Date)";

            DatabaseHelper.ExecuteNonQuery(sqlItem,
                new MySqlParameter("@RequestId", requestId),
                new MySqlParameter("@ProductId", productIds),
                new MySqlParameter("@Quantity", quantities),
                new MySqlParameter("@Date", dates.Date));

            return RedirectToAction("Details", new { id = requestId });
        }

        // 4. ПРОСМОТР ЗАЯВКИ (ТУПОЙ ВАРИАНТ)
        public IActionResult Details(int id)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Берём заявку
            var sql = @"
                SELECT pr.*, u.FullName as CreatedByName 
                FROM PurchaseRequests pr
                LEFT JOIN Users u ON pr.CreatedByUserId = u.Id
                WHERE pr.Id = @Id";

            var requestTable = DatabaseHelper.GetDataTable(sql, new MySqlParameter("@Id", id));
            if (requestTable.Rows.Count == 0)
                return Content("Заявка не найдена");

            var row = requestTable.Rows[0];

            // Всё в ViewBag (тупо, но понятно)
            ViewBag.Id = row["Id"];
            ViewBag.Number = row["Number"];
            ViewBag.Status = row["Status"].ToString();
            ViewBag.CreatedByName = row["CreatedByName"];
            ViewBag.CreatedDate = Convert.ToDateTime(row["Date"]).ToString("dd.MM.yyyy HH:mm");
            ViewBag.Comments = string.IsNullOrEmpty(row["Comments"]?.ToString()) ? "нет" : row["Comments"];
            ViewBag.CreatedByUserId = row["CreatedByUserId"];

            // Логика кнопок (в контроллере, а не в View)
            var currentUserId = HttpContext.Session.GetString("UserId");
            var currentUserRole = HttpContext.Session.GetString("UserRole");
            var isOwner = row["CreatedByUserId"].ToString() == currentUserId;

            ViewBag.ShowEditButtons = (ViewBag.Status == "Draft" && (isOwner || currentUserRole == "Admin"));
            ViewBag.ShowReopenButton = (ViewBag.Status == "Completed" && currentUserRole == "Admin");

            // Строки заявки
            var itemsSql = @"
                SELECT pri.*, p.Name as ProductName, p.Unit
                FROM PurchaseRequestItems pri
                LEFT JOIN Products p ON pri.ProductId = p.Id
                WHERE pri.RequestId = @RequestId";

            var itemsTable = DatabaseHelper.GetDataTable(itemsSql, new MySqlParameter("@RequestId", id));
            ViewBag.HasItems = (itemsTable.Rows.Count > 0);

            // Делаем простой список
            var itemsList = new List<object>();
            foreach (DataRow itemRow in itemsTable.Rows)
            {
                itemsList.Add(new
                {
                    ProductName = itemRow["ProductName"],
                    Quantity = itemRow["Quantity"],
                    Unit = itemRow["Unit"],
                    RequiredDate = Convert.ToDateTime(itemRow["RequiredDate"]).ToString("dd.MM.yyyy")
                });
            }
            ViewBag.Items = itemsList;

            return View();
        }

        // 5. ИЗМЕНЕНИЕ СТАТУСА
        [HttpPost]
        public IActionResult UpdateStatus(int id, string newStatus)
        {
            try
            {
                // 1. Проверка авторизации
                if (HttpContext.Session.GetString("UserId") == null)
                    return RedirectToAction("Login", "Auth");

                var currentUserId = HttpContext.Session.GetString("UserId");
                var currentUserRole = HttpContext.Session.GetString("UserRole");

                // 2. Получаем данные заявки
                var checkSql = "SELECT Status, CreatedByUserId FROM PurchaseRequests WHERE Id = @Id";
                var request = DatabaseHelper.GetDataTable(checkSql,
                    new MySqlParameter("@Id", id));

                if (request.Rows.Count == 0)
                {
                    TempData["Error"] = "Заявка не найдена";
                    return RedirectToAction("Index");
                }

                var currentStatus = request.Rows[0]["Status"].ToString();
                var createdByUserId = request.Rows[0]["CreatedByUserId"].ToString();

                // 3. Проверяем права
                bool canChange = false;

                if (newStatus == "Completed" && currentStatus == "Draft")
                {
                    // Завершать может владелец или админ
                    canChange = (createdByUserId == currentUserId) || (currentUserRole == "Admin");
                }
                else if (newStatus == "Draft" && currentStatus == "Completed")
                {
                    // Возвращать в работу может только админ
                    canChange = (currentUserRole == "Admin");
                }

                if (!canChange)
                {
                    TempData["Error"] = "Нет прав для изменения статуса";
                    return RedirectToAction("Details", new { id });
                }

                // 4. Меняем статус
                var updateSql = "UPDATE PurchaseRequests SET Status = @Status WHERE Id = @Id";
                DatabaseHelper.ExecuteNonQuery(updateSql,
                    new MySqlParameter("@Status", newStatus),
                    new MySqlParameter("@Id", id));

                TempData["Message"] = $"Статус заявки изменён на '{newStatus}'";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                // Простейшая обработка ошибки
                TempData["Error"] = $"Ошибка: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }

        // 6. РЕДАКТИРОВАНИЕ
        public IActionResult Edit(int id)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Просто показываем форму редактирования
            var request = DatabaseHelper.GetDataTable(
                "SELECT * FROM PurchaseRequests WHERE Id = @Id",
                new MySqlParameter("@Id", id));

            if (request.Rows.Count == 0) return NotFound();

            ViewBag.Request = request.Rows[0];
            ViewBag.Products = DatabaseHelper.GetDataTable("SELECT Id, Name, Unit FROM Products");

            var items = DatabaseHelper.GetDataTable(
                "SELECT * FROM PurchaseRequestItems WHERE RequestId = @Id",
                new MySqlParameter("@Id", id));
            ViewBag.Items = items;

            return View();
        }

        [HttpPost]
        public IActionResult Edit(int id, List<int> productIds, List<decimal> quantities, List<DateTime> dates)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Удаляем старые строки
            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM PurchaseRequestItems WHERE RequestId = @Id",
                new MySqlParameter("@Id", id));

            // Добавляем новые
            for (int i = 0; i < productIds.Count; i++)
            {
                DatabaseHelper.ExecuteNonQuery(
                    @"INSERT INTO PurchaseRequestItems (RequestId, ProductId, Quantity, RequiredDate) 
                      VALUES (@RequestId, @ProductId, @Quantity, @Date)",
                    new MySqlParameter("@RequestId", id),
                    new MySqlParameter("@ProductId", productIds[i]),
                    new MySqlParameter("@Quantity", quantities[i]),
                    new MySqlParameter("@Date", dates[i].Date)
                );
            }

            return RedirectToAction("Details", new { id });
        }

        // 7. УДАЛЕНИЕ
        [HttpPost]
        public IActionResult Delete(int id)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            var currentUserId = HttpContext.Session.GetString("UserId");
            var currentUserRole = HttpContext.Session.GetString("UserRole");

            // Проверяем права
            var request = DatabaseHelper.GetDataTable(
                "SELECT Status, CreatedByUserId FROM PurchaseRequests WHERE Id = @Id",
                new MySqlParameter("@Id", id));

            if (request.Rows.Count == 0) return NotFound();

            var status = request.Rows[0]["Status"].ToString();
            var createdBy = request.Rows[0]["CreatedByUserId"].ToString();

            if (status != "Draft" || (createdBy != currentUserId && currentUserRole != "Admin"))
                return RedirectToAction("Details", new { id });

            // Удаляем
            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM PurchaseRequests WHERE Id = @Id",
                new MySqlParameter("@Id", id));

            return RedirectToAction("Index");
        }
    }
}