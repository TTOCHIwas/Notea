using Microsoft.Data.Sqlite;
using System;
using System.Data.SQLite;
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
    }
}