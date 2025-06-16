using Microsoft.Data.Sqlite;
using Notea.Helpers;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace Notea.Database
{
    public static class DatabaseInitializer
    {
        private const string DbFileName = "notea.db";

        public static void InitializeDatabase()
        {
            if (!File.Exists(DbFileName))
            {
                using var connection = new SqliteConnection($"Data Source={DbFileName}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE category
                    (
                        categoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                        displayOrder INTEGER DEFAULT 0,
                        title      VARCHAR NOT NULL,
                        subJectId  INTEGER NOT NULL,
                        timeId     INTEGER NOT NULL,
                        FOREIGN KEY (subJectId) REFERENCES subject (subJectId),
                        FOREIGN KEY (timeId) REFERENCES time (timeId)
                    );

                    CREATE TABLE memo
                    (
                        noteId  INTEGER PRIMARY KEY AUTOINCREMENT,
                        content text    NULL    
                    );

                    CREATE TABLE monthlyEvent
                    (
                        planId      INTEGER PRIMARY KEY AUTOINCREMENT,
                        title       VARCHAR  NOT NULL,
                        description VARCHAR  NULL    ,
                        isDday      BOOLEAN  NOT NULL,
                        startDate   DATETIME NOT NULL,
                        endDate     DATETIME NOT NULL,
                        color       VARCHAR  NULL    
                    );

                    CREATE TABLE noteContent
                    (
                        TextId     INTEGER PRIMARY KEY AUTOINCREMENT,
                        displayOrder INTEGER DEFAULT 0,
                        content    VARCHAR NULL    ,
                        categoryId INTEGER NOT NULL,
                        subJectId  INTEGER NOT NULL,
                        FOREIGN KEY (categoryId) REFERENCES category (categoryId),
                        FOREIGN KEY (subJectId) REFERENCES subject (subJectId)
                    );

                    CREATE TABLE subject
                    (
                        subJectId INTEGER PRIMARY KEY AUTOINCREMENT,
                        title     VARCHAR NOT NULL
                    );

                    CREATE TABLE time
                    (
                        timeId     INTEGER PRIMARY KEY AUTOINCREMENT,
                        createDate DATETIME NOT NULL,
                        record     INT      NOT NULL
                    );

                    CREATE TABLE todo
                    (
                        todoId     INTEGER PRIMARY KEY AUTOINCREMENT,
                        createDate DATETIME NOT NULL,
                        title      VARCHAR  NOT NULL,
                        isDo       BOOLEAN  NOT NULL
                    );
                    ";

                command.ExecuteNonQuery();
            }
        }
        public static void UpdateSchemaForDisplayOrder()
        {
            try
            {
                // category 테이블에 displayOrder 컬럼 추가 (없으면)
                string checkCategoryColumn = @"
                    SELECT COUNT(*) as count 
                    FROM pragma_table_info('category') 
                    WHERE name='displayOrder'";

                var result = DatabaseHelper.ExecuteSelect(checkCategoryColumn);
                if (result.Rows.Count > 0 && Convert.ToInt32(result.Rows[0]["count"]) == 0)
                {
                    string addCategoryOrder = @"
                        ALTER TABLE category ADD COLUMN displayOrder INTEGER DEFAULT 0";
                    DatabaseHelper.ExecuteNonQuery(addCategoryOrder);
                    Debug.WriteLine("[DB] category.displayOrder 컬럼 추가됨");
                }

                // noteContent 테이블에 displayOrder 컬럼 추가 (없으면)
                string checkContentColumn = @"
                    SELECT COUNT(*) as count 
                    FROM pragma_table_info('noteContent') 
                    WHERE name='displayOrder'";

                result = DatabaseHelper.ExecuteSelect(checkContentColumn);
                if (result.Rows.Count > 0 && Convert.ToInt32(result.Rows[0]["count"]) == 0)
                {
                    string addContentOrder = @"
                        ALTER TABLE noteContent ADD COLUMN displayOrder INTEGER DEFAULT 0";
                    DatabaseHelper.ExecuteNonQuery(addContentOrder);
                    Debug.WriteLine("[DB] noteContent.displayOrder 컬럼 추가됨");
                }

                // 기존 데이터의 displayOrder 초기화 (0인 경우)
                string updateExistingOrders = @"
                    UPDATE category SET displayOrder = categoryId WHERE displayOrder = 0;
                    UPDATE noteContent SET displayOrder = TextId WHERE displayOrder = 0;";
                DatabaseHelper.ExecuteNonQuery(updateExistingOrders);

                Debug.WriteLine("[DB] displayOrder 스키마 업데이트 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] displayOrder 스키마 업데이트 실패: {ex.Message}");
            }
        }
    }
}