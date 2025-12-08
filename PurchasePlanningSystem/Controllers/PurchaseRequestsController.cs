using Microsoft.AspNetCore.Mvc;
using PurchasePlanningSystem.Utils;
using MySql.Data.MySqlClient;

namespace PurchasePlanningSystem.Controllers
{
    public class PurchaseRequestsController : Controller
    {
        // 1. СПИСОК ЗАЯВОК (просто показывает что есть)
        public IActionResult Index()
        {
            // Проверка входа
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Берём заявки из БД
            var sql = @"
                SELECT pr.Id, pr.Number, pr.Date, pr.Status, u.FullName as CreatedBy
                FROM PurchaseRequests pr
                LEFT JOIN Users u ON pr.CreatedByUserId = u.Id
                ORDER BY pr.Date DESC";

            var table = DatabaseHelper.GetDataTable(sql);
            ViewBag.Requests = table; // Кидаем прямо DataTable во ViewBag
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole");

            return View();
        }

        // 2. ФОРМА СОЗДАНИЯ (просто показывает форму)
        public IActionResult Create()
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Берём продукты
            var products = DatabaseHelper.GetDataTable("SELECT Id, Name, Unit FROM Products");
            ViewBag.Products = products;

            return View();
        }

        // 3. СОХРАНЕНИЕ ЗАЯВКИ (самый тупой вариант - ОДНА строка)
        [HttpPost]
        public IActionResult Create(int productIds, decimal quantities, DateTime dates)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            // Генерируем номер
            var number = "PR-" + DateTime.Now.ToString("yyyyMMdd-HHmm");

            // 1. Вставляем заявку
            var sqlRequest = @"
                INSERT INTO PurchaseRequests (Number, Date, Status, CreatedByUserId) 
                VALUES (@Number, NOW(), 'Draft', @UserId);
                SELECT LAST_INSERT_ID();";

            var requestId = DatabaseHelper.ExecuteScalar(sqlRequest,
                new MySqlParameter("@Number", number),
                new MySqlParameter("@UserId", int.Parse(userId)));

            // 2. Вставляем ОДНУ строку заявки
            var sqlItem = @"
                INSERT INTO PurchaseRequestItems (RequestId, ProductId, Quantity, RequiredDate) 
                VALUES (@RequestId, @ProductId, @Quantity, @Date)";

            DatabaseHelper.ExecuteNonQuery(sqlItem,
                new MySqlParameter("@RequestId", requestId),
                new MySqlParameter("@ProductId", productIds),
                new MySqlParameter("@Quantity", quantities),
                new MySqlParameter("@Date", dates.Date) // Только дата!
            );

            // 3. Переходим к просмотру этой заявки
            return RedirectToAction("Details", new { id = requestId });
        }

        // 4. ПРОСМОТР ЗАЯВКИ (просто показывает)
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

            ViewBag.Request = requestTable.Rows[0];

            // Берём её строки
            var itemsSql = @"
                SELECT pri.*, p.Name as ProductName, p.Unit
                FROM PurchaseRequestItems pri
                LEFT JOIN Products p ON pri.ProductId = p.Id
                WHERE pri.RequestId = @RequestId";

            ViewBag.Items = DatabaseHelper.GetDataTable(itemsSql, new MySqlParameter("@RequestId", id));

            return View();
        }

        // 5. СОЗДАНИЕ ЗАКАЗА (просто создаёт заказ из заявки)
        public IActionResult CreateOrder(int requestId)
        {
            if (HttpContext.Session.GetString("UserId") == null)
                return RedirectToAction("Login", "Auth");

            // Берём заявку
            var requestSql = "SELECT * FROM PurchaseRequests WHERE Id = @Id";
            var request = DatabaseHelper.GetDataTable(requestSql, new MySqlParameter("@Id", requestId));

            if (request.Rows.Count == 0)
                return Content("Заявка не найдена");

            // Берём первого поставщика
            var supplierSql = "SELECT Id FROM Suppliers WHERE IsActive = TRUE LIMIT 1";
            var supplierId = DatabaseHelper.ExecuteScalar(supplierSql);

            if (supplierId == null)
                return Content("Нет активных поставщиков");

            // Создаём номер заказа
            var orderNumber = "PO-" + DateTime.Now.ToString("yyyyMMdd-HHmm");

            // 1. Создаём заказ
            var orderSql = @"
                INSERT INTO PurchaseOrders (Number, Date, Status, SupplierId, RequestId, CreatedByUserId) 
                VALUES (@Number, NOW(), 'Draft', @SupplierId, @RequestId, @UserId);
                SELECT LAST_INSERT_ID();";

            var orderId = DatabaseHelper.ExecuteScalar(orderSql,
                new MySqlParameter("@Number", orderNumber),
                new MySqlParameter("@SupplierId", supplierId),
                new MySqlParameter("@RequestId", requestId),
                new MySqlParameter("@UserId", int.Parse(HttpContext.Session.GetString("UserId")))
            );

            // 2. Копируем строки из заявки в заказ (с ценой = 0)
            var copyItemsSql = @"
                INSERT INTO PurchaseOrderItems (OrderId, ProductId, Quantity, Price)
                SELECT @OrderId, pri.ProductId, pri.Quantity, 0
                FROM PurchaseRequestItems pri
                WHERE pri.RequestId = @RequestId";

            DatabaseHelper.ExecuteNonQuery(copyItemsSql,
                new MySqlParameter("@OrderId", orderId),
                new MySqlParameter("@RequestId", requestId)
            );

            // 3. Меняем статус заявки
            DatabaseHelper.ExecuteNonQuery(
                "UPDATE PurchaseRequests SET Status = 'Processed' WHERE Id = @Id",
                new MySqlParameter("@Id", requestId)
            );

            return Content($"Заказ {orderNumber} создан! ID: {orderId}");
        }

        public IActionResult OrderDetails(int id, string message = "")
        {
            if (!string.IsNullOrEmpty(message)) ViewBag.Message = message;

            var sql = @"
        SELECT po.*, s.Name as SupplierName, u.FullName as CreatedByName, pr.Number as RequestNumber
        FROM PurchaseOrders po
        LEFT JOIN Suppliers s ON po.SupplierId = s.Id
        LEFT JOIN Users u ON po.CreatedByUserId = u.Id
        LEFT JOIN PurchaseRequests pr ON po.RequestId = pr.Id
        WHERE po.Id = @Id";

            var orderTable = DatabaseHelper.GetDataTable(sql, new MySqlParameter("@Id", id));
            if (orderTable.Rows.Count == 0) return Content("Заказ не найден");

            ViewBag.Order = orderTable.Rows[0];

            // Строки заказа
            var itemsSql = @"
        SELECT poi.*, p.Name as ProductName, p.Unit
        FROM PurchaseOrderItems poi
        LEFT JOIN Products p ON poi.ProductId = p.Id
        WHERE poi.OrderId = @OrderId";

            ViewBag.Items = DatabaseHelper.GetDataTable(itemsSql, new MySqlParameter("@OrderId", id));

            return View();
        }
    }
}