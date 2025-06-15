using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notea.Database
{
    public static class DummyDataSeeder
    {
        public static void Seed(Microsoft.Data.Sqlite.SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();

            cmd.CommandText = @"
                INSERT OR IGNORE INTO subject (subJectId, title) VALUES (1, '윈도우즈 프로그래밍');

                INSERT OR IGNORE INTO time (timeId, createDate, record)
                VALUES (1, datetime('now'), 1);

                INSERT OR IGNORE INTO category (categoryId, title, subJectId, timeId)
                VALUES (1, '# 제목입니다', 1, 1);

                INSERT OR IGNORE INTO noteContent (TextId, content, categoryId, subJectId)
                VALUES 
                  (1, '이건 **본문**입니다.', 1, 1),
                  (2, '두 번째 *줄*입니다.', 1, 1);
                ";
            cmd.ExecuteNonQuery();
        }
    }
}
