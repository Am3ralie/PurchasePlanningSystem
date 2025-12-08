using System.Data;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;


namespace PurchasePlanningSystem.Utils
{
    public static class DatabaseHelper
    {
        // Строка подключения (простая реализация для прототипа)
        private static string _connectionString;

        static DatabaseHelper()
        {
            // Загружаем конфигурацию из appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _connectionString = config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Не найдена строка подключения в appsettings.json");
            }
        }

        /// <summary>
        /// Создаёт и возвращает открытое подключение к БД
        /// </summary>
        public static MySqlConnection GetOpenConnection()
        {
            var connection = new MySqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Выполняет SQL-команду без возврата данных (INSERT, UPDATE, DELETE)
        /// </summary>
        public static int ExecuteNonQuery(string sql, params MySqlParameter[] parameters)
        {
            using (var connection = GetOpenConnection())
            using (var command = new MySqlCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первое значение первой строки
        /// </summary>
        public static object ExecuteScalar(string sql, params MySqlParameter[] parameters)
        {
            using (var connection = GetOpenConnection())
            using (var command = new MySqlCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает DataTable
        /// </summary>
        public static DataTable GetDataTable(string sql, params MySqlParameter[] parameters)
        {
            var dataTable = new DataTable();

            using (var connection = GetOpenConnection())
            using (var command = new MySqlCommand(sql, connection))
            using (var adapter = new MySqlDataAdapter(command))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }
                adapter.Fill(dataTable);
            }

            return dataTable;
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает список объектов через маппинг
        /// </summary>
        public static List<T> GetList<T>(string sql, Func<MySqlDataReader, T> mapper, params MySqlParameter[] parameters)
        {
            var result = new List<T>();

            using (var connection = GetOpenConnection())
            using (var command = new MySqlCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(mapper(reader));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Проверяет существование записи по условию
        /// </summary>
        public static bool Exists(string tableName, string condition, params MySqlParameter[] parameters)
        {
            string sql = $"SELECT 1 FROM `{tableName}` WHERE {condition} LIMIT 1";
            var result = ExecuteScalar(sql, parameters);
            return result != null && result != DBNull.Value;
        }

        /// <summary>
        /// Начинает транзакцию
        /// </summary>
        public static MySqlTransaction BeginTransaction()
        {
            var connection = GetOpenConnection();
            return connection.BeginTransaction();
        }

        /// <summary>
        /// Выполняет команду в транзакции
        /// </summary>
        public static int ExecuteNonQueryInTransaction(MySqlTransaction transaction, string sql, params MySqlParameter[] parameters)
        {
            using (var command = new MySqlCommand(sql, transaction.Connection, transaction))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteNonQuery();
            }
        }
    }
}