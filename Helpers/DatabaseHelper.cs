using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notea.Helpers
{
    public static class DatabaseHelper
    {
        private static readonly string dbPath = "data/notea.db";
        private static readonly string connectionString = $"Data Source={dbPath};Version=3;";

        // 연결 테스트용 메서드
        public static bool TestConnection()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB 연결 실패: " + ex.Message);
                return false;
            }
        }

        // SELECT 쿼리 실행 (결과 반환)
        public static DataTable ExecuteSelect(string query)
        {
            var dt = new DataTable();

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(query, connection))
                using (var adapter = new SQLiteDataAdapter(command))
                {
                    adapter.Fill(dt);
                }
            }

            return dt;
        }

        // INSERT, UPDATE, DELETE 쿼리 실행
        public static int ExecuteNonQuery(string query)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(query, connection))
                {
                    return command.ExecuteNonQuery();
                }
            }
        }
    }
}
