using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Notea.Helpers
{
    public static class DatabaseHelper
    {
        // 절대 경로 사용하여 DB 위치 고정
        private static readonly string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "notea.db");
        private static readonly string connectionString;

        static DatabaseHelper()
        {
            // data 폴더가 없으면 생성
            var dataDir = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            connectionString = $"Data Source={dbPath};Version=3;";

            // DB 파일이 없으면 생성
            if (!File.Exists(dbPath))
            {
                InitializeDatabase();
            }
        }

        // 데이터베이스 초기화
        private static void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS category
                    (
                        categoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                        title      VARCHAR NOT NULL,
                        subJectId  INTEGER NOT NULL,
                        timeId     INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS subject
                    (
                        subJectId INTEGER PRIMARY KEY AUTOINCREMENT,
                        title     VARCHAR NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS time
                    (
                        timeId     INTEGER PRIMARY KEY AUTOINCREMENT,
                        createDate DATETIME NOT NULL,
                        record     INT      NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS noteContent
                    (
                        TextId     INTEGER PRIMARY KEY AUTOINCREMENT,
                        content    VARCHAR NULL,
                        categoryId INTEGER NOT NULL,
                        subJectId  INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS monthlyEvent
                    (
                        planId      INTEGER PRIMARY KEY AUTOINCREMENT,
                        title       VARCHAR  NOT NULL,
                        description VARCHAR  NULL,
                        isDday      BOOLEAN  NOT NULL,
                        startDate   DATETIME NOT NULL,
                        endDate     DATETIME NOT NULL,
                        color       VARCHAR  NULL
                    );

                    -- 기본 데이터 삽입
                    INSERT OR IGNORE INTO subject (subJectId, title) VALUES (1, '윈도우즈 프로그래밍');
                    INSERT OR IGNORE INTO time (timeId, createDate, record) VALUES (1, datetime('now'), 1);
                    INSERT OR IGNORE INTO category (categoryId, title, subJectId, timeId) VALUES (1, '# 기본 카테고리', 1, 1);
                ";

                command.ExecuteNonQuery();

                Console.WriteLine($"데이터베이스 초기화 완료: {dbPath}");
            }
        }

        // 연결 테스트용 메서드
        public static bool TestConnection()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    Console.WriteLine($"DB 연결 성공: {dbPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB 연결 실패: {ex.Message}");
                Console.WriteLine($"시도한 경로: {dbPath}");
                return false;
            }
        }

        // SELECT 쿼리 실행 (결과 반환)
        public static DataTable ExecuteSelect(string query)
        {
            var dt = new DataTable();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(query, connection))
                    using (var adapter = new SQLiteDataAdapter(command))
                    {
                        adapter.Fill(dt);
                    }
                }

                Console.WriteLine($"SELECT 쿼리 실행 성공. 반환된 행: {dt.Rows.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SELECT 쿼리 실행 실패: {ex.Message}");
                Console.WriteLine($"쿼리: {query}");
            }

            return dt;
        }

        // INSERT, UPDATE, DELETE 쿼리 실행
        public static int ExecuteNonQuery(string query)
        {
            int result = 0;

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        result = command.ExecuteNonQuery();
                    }
                }

                Console.WriteLine($"쿼리 실행 성공. 영향받은 행: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"쿼리 실행 실패: {ex.Message}");
                Console.WriteLine($"쿼리: {query}");
            }

            return result;
        }

        // DB 경로 확인용
        public static string GetDatabasePath() => dbPath;
    }
}