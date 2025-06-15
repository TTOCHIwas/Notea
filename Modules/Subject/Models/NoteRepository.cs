using Microsoft.Data.Sqlite;
using Notea.Modules.Subject.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            getCategories.CommandText = "SELECT categoryId, title FROM category WHERE subJectId = @sid ORDER BY categoryId";
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
                SELECT TextId, content FROM noteContent 
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
                        Index = notes.GetInt32(0), // TextId를 Index로 사용
                        Content = notes.GetString(1)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 제목인지 확인하는 메서드
        /// </summary>
        public static bool IsHeading(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;
            return Regex.IsMatch(content.Trim(), @"^#{1,6}\s+.+");
        }

        /// <summary>
        /// 제목에서 # 기호를 제거하고 실제 제목 텍스트만 추출
        /// </summary>
        public static string ExtractHeadingText(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "";

            var match = Regex.Match(content.Trim(), @"^#{1,6}\s+(.+)");
            return match.Success ? match.Groups[1].Value.Trim() : content;
        }

        /// <summary>
        /// 새로운 카테고리(제목) 삽입 - 마크다운 문법 그대로 저장
        /// </summary>
        public static int InsertCategory(string content, int subjectId, int timeId = 1)
        {
            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO category (title, subJectId, timeId)
                VALUES (@title, @subjectId, @timeId);
                SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@title", content); // 마크다운 문법 그대로 저장
            cmd.Parameters.AddWithValue("@subjectId", subjectId);
            cmd.Parameters.AddWithValue("@timeId", timeId);

            var result = cmd.ExecuteScalar();
            int categoryId = Convert.ToInt32(result);

            Debug.WriteLine($"[DB] 새 카테고리 삽입 완료. CategoryId: {categoryId}, Content: {content}");
            return categoryId;
        }

        /// <summary>
        /// 카테고리(제목) 업데이트 - 마크다운 문법 그대로 저장
        /// </summary>
        public static void UpdateCategory(int categoryId, string content)
        {
            if (categoryId <= 0)
            {
                Debug.WriteLine($"[WARNING] UpdateCategory 호출됐지만 CategoryId가 유효하지 않음: {categoryId}");
                return;
            }

            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE category 
                SET title = @title
                WHERE categoryId = @categoryId";

            cmd.Parameters.AddWithValue("@title", content); // 마크다운 문법 그대로 저장
            cmd.Parameters.AddWithValue("@categoryId", categoryId);

            int rowsAffected = cmd.ExecuteNonQuery();
            Debug.WriteLine($"[DB] 카테고리 업데이트 완료. CategoryId: {categoryId}, Content: {content}, 영향받은 행: {rowsAffected}");
        }

        /// <summary>
        /// 카테고리(제목) 삭제 및 관련 noteContent도 함께 삭제
        /// </summary>
        public static void DeleteCategory(int categoryId)
        {
            if (categoryId <= 0)
            {
                Debug.WriteLine($"[WARNING] DeleteCategory 호출됐지만 CategoryId가 유효하지 않음: {categoryId}");
                return;
            }

            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. 먼저 관련 noteContent 삭제
                var deleteNotesCmd = conn.CreateCommand();
                deleteNotesCmd.Transaction = transaction;
                deleteNotesCmd.CommandText = "DELETE FROM noteContent WHERE categoryId = @categoryId";
                deleteNotesCmd.Parameters.AddWithValue("@categoryId", categoryId);
                int notesDeleted = deleteNotesCmd.ExecuteNonQuery();

                // 2. 카테고리 삭제
                var deleteCategoryCmd = conn.CreateCommand();
                deleteCategoryCmd.Transaction = transaction;
                deleteCategoryCmd.CommandText = "DELETE FROM category WHERE categoryId = @categoryId";
                deleteCategoryCmd.Parameters.AddWithValue("@categoryId", categoryId);
                int categoryDeleted = deleteCategoryCmd.ExecuteNonQuery();

                transaction.Commit();
                Debug.WriteLine($"[DB] 카테고리 삭제 완료. CategoryId: {categoryId}, 삭제된 노트: {notesDeleted}개, 삭제된 카테고리: {categoryDeleted}개");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Debug.WriteLine($"[DB ERROR] 카테고리 삭제 실패, 롤백됨: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 새로운 일반 텍스트 라인 삽입
        /// </summary>
        public static int InsertNewLine(MarkdownLineViewModel line)
        {
            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO noteContent (content, subjectId, categoryId)
                VALUES (@content, @subjectId, @categoryId);
                SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@content", line.Content ?? "");
            cmd.Parameters.AddWithValue("@subjectId", line.SubjectId);
            cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);

            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// 기존 일반 텍스트 라인 업데이트
        /// </summary>
        public static void UpdateLine(MarkdownLineViewModel line)
        {
            if (line.TextId <= 0)
            {
                Debug.WriteLine($"[WARNING] UpdateLine 호출됐지만 TextId가 유효하지 않음: {line.TextId}");
                return;
            }

            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE noteContent 
                SET content = @content
                WHERE TextId = @textId";

            cmd.Parameters.AddWithValue("@content", line.Content ?? "");
            cmd.Parameters.AddWithValue("@textId", line.TextId);

            int rowsAffected = cmd.ExecuteNonQuery();
            if (rowsAffected == 0)
            {
                Debug.WriteLine($"[WARNING] UpdateLine 실행됐지만 영향받은 행이 없음. TextId: {line.TextId}");
            }
        }

        /// <summary>
        /// 일반 텍스트 라인 삭제
        /// </summary>
        public static void DeleteLine(int textId)
        {
            if (textId <= 0)
            {
                Debug.WriteLine($"[WARNING] DeleteLine 호출됐지만 TextId가 유효하지 않음: {textId}");
                return;
            }

            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM noteContent WHERE TextId = @textId";
            cmd.Parameters.AddWithValue("@textId", textId);

            int rowsAffected = cmd.ExecuteNonQuery();
            Debug.WriteLine($"[DB] 라인 삭제 완료. TextId: {textId}, 영향받은 행: {rowsAffected}");
        }

        /// <summary>
        /// 라인이 제목인지 일반 텍스트인지 판단하여 적절히 저장
        /// </summary>
        public static void SaveOrUpdateLine(MarkdownLineViewModel line)
        {
            try
            {
                if (IsHeading(line.Content))
                {
                    // 제목인 경우 - 마크다운 문법 그대로 저장
                    if (line.CategoryId <= 0)
                    {
                        // 새로운 제목 삽입
                        int newCategoryId = InsertCategory(line.Content, line.SubjectId); // Content 그대로 저장
                        line.CategoryId = newCategoryId;
                        line.IsHeadingLine = true;
                        Debug.WriteLine($"[DB] 새 제목 삽입 완료. CategoryId: {newCategoryId}, Content: {line.Content}");
                    }
                    else
                    {
                        // 기존 제목 업데이트
                        UpdateCategory(line.CategoryId, line.Content); // Content 그대로 저장
                        Debug.WriteLine($"[DB] 제목 업데이트 완료. CategoryId: {line.CategoryId}, Content: {line.Content}");
                    }
                }
                else
                {
                    // 일반 텍스트인 경우
                    if (line.TextId <= 0)
                    {
                        // 새로운 라인 삽입
                        int newTextId = InsertNewLine(line);
                        line.TextId = newTextId;
                        Debug.WriteLine($"[DB] 새 라인 삽입 완료. TextId: {newTextId}, Content: {line.Content}");
                    }
                    else
                    {
                        // 기존 라인 업데이트
                        UpdateLine(line);
                        Debug.WriteLine($"[DB] 라인 업데이트 완료. TextId: {line.TextId}, Content: {line.Content}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] SaveOrUpdateLine 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 여러 라인을 한 번에 처리 (트랜잭션) - 제목/일반텍스트 구분
        /// </summary>
        public static void SaveLinesInTransaction(List<MarkdownLineViewModel> lines)
        {
            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (var line in lines)
                {
                    if (IsHeading(line.Content))
                    {
                        // 제목 처리 - 마크다운 문법 그대로 저장

                        if (line.CategoryId <= 0)
                        {
                            // 새로운 제목 삽입
                            var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT INTO category (title, subJectId, timeId)
                                VALUES (@title, @subjectId, @timeId);
                                SELECT last_insert_rowid();";

                            cmd.Parameters.AddWithValue("@title", line.Content); // 마크다운 문법 그대로 저장
                            cmd.Parameters.AddWithValue("@subjectId", line.SubjectId);
                            cmd.Parameters.AddWithValue("@timeId", 1);

                            var result = cmd.ExecuteScalar();
                            line.CategoryId = Convert.ToInt32(result);
                            line.IsHeadingLine = true;
                        }
                        else
                        {
                            // 기존 제목 업데이트
                            var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                UPDATE category 
                                SET title = @title
                                WHERE categoryId = @categoryId";

                            cmd.Parameters.AddWithValue("@title", line.Content); // 마크다운 문법 그대로 저장
                            cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // 일반 텍스트 처리
                        if (line.TextId <= 0)
                        {
                            // 새로운 라인 삽입
                            var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT INTO noteContent (content, subjectId, categoryId)
                                VALUES (@content, @subjectId, @categoryId);
                                SELECT last_insert_rowid();";

                            cmd.Parameters.AddWithValue("@content", line.Content ?? "");
                            cmd.Parameters.AddWithValue("@subjectId", line.SubjectId);
                            cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);

                            var result = cmd.ExecuteScalar();
                            line.TextId = Convert.ToInt32(result);
                        }
                        else
                        {
                            // 기존 라인 업데이트
                            var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                UPDATE noteContent 
                                SET content = @content
                                WHERE TextId = @textId";

                            cmd.Parameters.AddWithValue("@content", line.Content ?? "");
                            cmd.Parameters.AddWithValue("@textId", line.TextId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                transaction.Commit();
                Debug.WriteLine($"[DB] 트랜잭션으로 {lines.Count}개 라인 저장 완료");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Debug.WriteLine($"[DB ERROR] 트랜잭션 실패, 롤백됨: {ex.Message}");
                throw;
            }
        }

        // 레거시 메서드들 (하위 호환성을 위해 유지)
        public static void SaveOrUpdateNoteLine(MarkdownLineViewModel line)
        {
            SaveOrUpdateLine(line);
        }

        public static void SaveOrInsertNoteLine(MarkdownLineViewModel line)
        {
            SaveOrUpdateLine(line);
        }
    }
}