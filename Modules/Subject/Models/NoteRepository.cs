using Microsoft.Data.Sqlite;
using Notea.Modules.Subject.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notea.Modules.Subject.Models
{
    public static class NoteRepository
    {
        public static List<NoteCategory> LoadNotesBySubject(int subjectId)
        {
            var result = new List<NoteCategory>();

            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            // 1. category 불러오기
            var getCategories = conn.CreateCommand();
            getCategories.CommandText = "SELECT categoryId, title FROM category WHERE subJectId = @sid";
            getCategories.Parameters.AddWithValue("@sid", subjectId);

            using var reader = getCategories.ExecuteReader();
            while (reader.Read())
            {
                var categoryId = reader.GetInt32(0);
                var title = reader.GetString(1);

                var category = new NoteCategory
                {
                    CategoryId = categoryId,
                    Title = title
                };

                result.Add(category);
            }

            // 2. 각 category에 속한 noteContent 불러오기
            foreach (var cat in result)
            {
                var getNotes = conn.CreateCommand();
                getNotes.CommandText = @"
                SELECT content FROM noteContent 
                WHERE categoryId = @cid AND subJectId = @sid
                ORDER BY TextId
            ";
                getNotes.Parameters.AddWithValue("@cid", cat.CategoryId);
                getNotes.Parameters.AddWithValue("@sid", subjectId);

                using var notes = getNotes.ExecuteReader();
                while (notes.Read())
                {
                    cat.Lines.Add(new NoteLine
                    {
                        Content = notes.GetString(0)
                    });
                }
            }

            return result;
        }

        public static void SaveOrUpdateNoteLine(MarkdownLineViewModel line)
        {
            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            // 기본 예시: TextId가 없는 경우 새로 삽입
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            INSERT OR REPLACE INTO noteContent (TextId, content, categoryId, subjectId)
            VALUES (@id, @content, @categoryId, @subjectId)";

            cmd.Parameters.AddWithValue("@id", line.TextId);
            cmd.Parameters.AddWithValue("@content", line.Content);
            cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);
            cmd.Parameters.AddWithValue("@subjectId", line.SubjectId);

            cmd.ExecuteNonQuery();
        }

        public static void SaveOrInsertNoteLine(MarkdownLineViewModel line)
        {
            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO noteContent (content, subjectId, categoryId)
                VALUES (@content, @subjectId, @categoryId);";

            cmd.Parameters.AddWithValue("@content", line.Content ?? "");
            cmd.Parameters.AddWithValue("@subjectId", line.SubjectId);
            cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);

            cmd.ExecuteNonQuery();
        }
    }
}
