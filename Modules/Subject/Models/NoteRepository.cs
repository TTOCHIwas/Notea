using Microsoft.Data.Sqlite;
using Notea.Helpers;
using Notea.Modules.Subject.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Notea.Modules.Subject.Models
{
    public static class NoteRepository
    {
        // DB 경로는 DatabaseHelper에서 관리
        private static string GetConnectionString() => $"Data Source={DatabaseHelper.GetDatabasePath()};Version=3;";

        public static List<NoteCategory> LoadNotesBySubject(int subjectId)
        {
            var result = new List<NoteCategory>();

            try
            {
                // DatabaseHelper를 사용하여 데이터 조회
                string categoryQuery = $"SELECT categoryId, title FROM category WHERE subJectId = {subjectId} ORDER BY categoryId";
                DataTable categoryTable = DatabaseHelper.ExecuteSelect(categoryQuery);

                foreach (DataRow row in categoryTable.Rows)
                {
                    var categoryId = Convert.ToInt32(row["categoryId"]);
                    var title = row["title"].ToString();

                    var category = new NoteCategory
                    {
                        CategoryId = categoryId,
                        Title = title
                    };

                    // 각 카테고리의 콘텐츠 조회
                    string contentQuery = $@"
                        SELECT TextId, content FROM noteContent 
                        WHERE categoryId = {categoryId} AND subJectId = {subjectId}
                        ORDER BY TextId";

                    DataTable contentTable = DatabaseHelper.ExecuteSelect(contentQuery);

                    foreach (DataRow contentRow in contentTable.Rows)
                    {
                        category.Lines.Add(new NoteLine
                        {
                            Index = Convert.ToInt32(contentRow["TextId"]),
                            Content = contentRow["content"].ToString()
                        });
                    }

                    result.Add(category);
                }

                Debug.WriteLine($"[DB] LoadNotesBySubject 완료. 카테고리 수: {result.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] LoadNotesBySubject 실패: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 카테고리로 저장할 제목인지 확인하는 메서드 - # 하나로 시작하는 경우만 카테고리로 저장
        /// </summary>
        public static bool IsCategoryHeading(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;
            // ^#\s+ : 라인 시작(^) + # 하나 + 공백(\s+) + 아무 문자
            // (?!#) : # 다음에 또 #이 오지 않는 경우만
            return Regex.IsMatch(content.Trim(), @"^#(?!#)\s+.+");
        }

        /// <summary>
        /// 마크다운 제목인지 확인 (렌더링용) - #~###### 모두 제목으로 표시
        /// </summary>
        public static bool IsMarkdownHeading(string content)
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

            var match = Regex.Match(content.Trim(), @"^#\s+(.+)");
            return match.Success ? match.Groups[1].Value.Trim() : content;
        }

        /// <summary>
        /// 새로운 카테고리(제목) 삽입 - 마크다운 문법 그대로 저장
        /// </summary>
        public static int InsertCategory(string content, int subjectId, int timeId = 1)
        {
            try
            {
                using var conn = new SqliteConnection(GetConnectionString());
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO category (title, subJectId, timeId)
                    VALUES (@title, @subjectId, @timeId);
                    SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@title", content);
                cmd.Parameters.AddWithValue("@subjectId", subjectId);
                cmd.Parameters.AddWithValue("@timeId", timeId);

                var result = cmd.ExecuteScalar();
                int categoryId = Convert.ToInt32(result);

                Debug.WriteLine($"[DB] 새 카테고리 삽입 완료. CategoryId: {categoryId}, Content: {content}");
                return categoryId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] InsertCategory 실패: {ex.Message}");
                return 0;
            }
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

            try
            {
                string query = $@"
                    UPDATE category 
                    SET title = '{content.Replace("'", "''")}'
                    WHERE categoryId = {categoryId}";

                int rowsAffected = DatabaseHelper.ExecuteNonQuery(query);
                Debug.WriteLine($"[DB] 카테고리 업데이트 완료. CategoryId: {categoryId}, Content: {content}, 영향받은 행: {rowsAffected}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] UpdateCategory 실패: {ex.Message}");
            }
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

            try
            {
                using var conn = new SqliteConnection(GetConnectionString());
                conn.Open();

                using var transaction = conn.BeginTransaction();

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
                Debug.WriteLine($"[DB ERROR] DeleteCategory 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 새로운 일반 텍스트 라인 삽입
        /// </summary>
        public static int InsertNewLine(MarkdownLineViewModel line)
        {
            try
            {
                using var conn = new SqliteConnection(GetConnectionString());
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO noteContent (content, subJectId, categoryId)
                    VALUES (@content, @subjectId, @categoryId);
                    SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@content", line.Content ?? "");
                cmd.Parameters.AddWithValue("@subjectId", line.SubjectId);
                cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);

                var result = cmd.ExecuteScalar();
                int textId = Convert.ToInt32(result);

                Debug.WriteLine($"[DB] 새 라인 삽입 완료. TextId: {textId}, CategoryId: {line.CategoryId}, Content: {line.Content}");
                return textId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] InsertNewLine 실패: {ex.Message}");
                return 0;
            }
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

            try
            {
                string query = $@"
                    UPDATE noteContent 
                    SET content = '{(line.Content ?? "").Replace("'", "''")}'
                    WHERE TextId = {line.TextId}";

                int rowsAffected = DatabaseHelper.ExecuteNonQuery(query);

                if (rowsAffected == 0)
                {
                    Debug.WriteLine($"[WARNING] UpdateLine 실행됐지만 영향받은 행이 없음. TextId: {line.TextId}");
                }
                else
                {
                    Debug.WriteLine($"[DB] 라인 업데이트 완료. TextId: {line.TextId}, Content: {line.Content}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] UpdateLine 실패: {ex.Message}");
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

            try
            {
                string query = $"DELETE FROM noteContent WHERE TextId = {textId}";
                int rowsAffected = DatabaseHelper.ExecuteNonQuery(query);
                Debug.WriteLine($"[DB] 라인 삭제 완료. TextId: {textId}, 영향받은 행: {rowsAffected}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] DeleteLine 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 라인이 제목인지 일반 텍스트인지 판단하여 적절히 저장
        /// </summary>
        public static void SaveOrUpdateLine(MarkdownLineViewModel line)
        {
            try
            {
                // CategoryId가 없으면 저장하지 않음
                if (line.CategoryId <= 0)
                {
                    Debug.WriteLine($"[WARNING] CategoryId가 유효하지 않아 저장 건너뜀. CategoryId: {line.CategoryId}");
                    return;
                }

                if (IsCategoryHeading(line.Content))  // # 하나만 카테고리로 저장
                {
                    // 카테고리(제목)인 경우
                    if (line.IsHeadingLine && line.CategoryId > 0)
                    {
                        // 기존 제목 업데이트
                        UpdateCategory(line.CategoryId, line.Content);
                    }
                    else
                    {
                        // 새로운 제목 삽입
                        int newCategoryId = InsertCategory(line.Content, line.SubjectId);
                        line.CategoryId = newCategoryId;
                        line.IsHeadingLine = true;
                    }
                }
                else
                {
                    // 일반 텍스트인 경우 (##, ### 등도 포함)
                    if (line.TextId <= 0)
                    {
                        // 새로운 라인 삽입
                        int newTextId = InsertNewLine(line);
                        line.TextId = newTextId;
                    }
                    else
                    {
                        // 기존 라인 업데이트
                        UpdateLine(line);
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
        /// 여러 라인을 한 번에 처리 (트랜잭션)
        /// </summary>
        public static void SaveLinesInTransaction(List<MarkdownLineViewModel> lines)
        {
            using var conn = new SqliteConnection(GetConnectionString());
            conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (var line in lines)
                {
                    if (line.CategoryId <= 0)
                    {
                        Debug.WriteLine($"[WARNING] 트랜잭션 중 CategoryId가 유효하지 않은 라인 건너뜀. Content: {line.Content}");
                        continue;
                    }

                    if (IsCategoryHeading(line.Content))  // # 하나만 카테고리로 저장
                    {
                        // 제목 처리
                        if (line.IsHeadingLine && line.CategoryId > 0)
                        {
                            var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                UPDATE category 
                                SET title = @title
                                WHERE categoryId = @categoryId";

                            cmd.Parameters.AddWithValue("@title", line.Content);
                            cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // 일반 텍스트 처리
                        if (line.TextId > 0)
                        {
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
    }
}