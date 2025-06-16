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
        private static string GetConnectionString() => $"Data Source={DatabaseHelper.GetDatabasePath()};";

        public static List<NoteCategory> LoadNotesBySubject(int subjectId)
        {
            var result = new List<NoteCategory>();
            var allLines = new List<dynamic>();

            try
            {
                // 모든 라인을 displayOrder 순으로 가져오기
                string query = $@"
                SELECT 'category' as lineType, categoryId as id, title as content, displayOrder, 0 as parentCategoryId
                FROM category 
                WHERE subjectId = {subjectId}
                UNION ALL
                SELECT 'text' as lineType, TextId as id, content, displayOrder, categoryId as parentCategoryId
                FROM noteContent 
                WHERE subjectId = {subjectId}
                ORDER BY displayOrder, id";

                DataTable table = DatabaseHelper.ExecuteSelect(query);

                NoteCategory currentCategory = null;

                foreach (DataRow row in table.Rows)
                {
                    string lineType = row["lineType"].ToString();

                    if (lineType == "category")
                    {
                        // 새 카테고리 시작
                        currentCategory = new NoteCategory
                        {
                            CategoryId = Convert.ToInt32(row["id"]),
                            Title = row["content"].ToString()
                        };
                        result.Add(currentCategory);
                    }
                    else if (lineType == "text" && currentCategory != null)
                    {
                        // 현재 카테고리에 텍스트 추가
                        int parentCategoryId = Convert.ToInt32(row["parentCategoryId"]);

                        // 올바른 카테고리 찾기
                        var targetCategory = result.FirstOrDefault(c => c.CategoryId == parentCategoryId);
                        if (targetCategory != null)
                        {
                            targetCategory.Lines.Add(new NoteLine
                            {
                                Index = Convert.ToInt32(row["id"]),
                                Content = row["content"].ToString()
                            });
                        }
                    }
                }

                Debug.WriteLine($"[DB] LoadNotesBySubject 완료. 카테고리 수: {result.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] LoadNotesBySubject 실패: {ex.Message}");
            }

            return result;
        }

        public static int GetNextDisplayOrder(int subjectId)
        {
            try
            {
                // 카테고리와 텍스트 중 최대 displayOrder 찾기
                string query = $@"
                SELECT MAX(displayOrder) as maxOrder FROM (
                    SELECT displayOrder FROM category WHERE subjectId = {subjectId}
                    UNION ALL
                    SELECT displayOrder FROM noteContent WHERE subjectId = {subjectId}
                )";

                var result = DatabaseHelper.ExecuteSelect(query);
                if (result.Rows.Count > 0 && result.Rows[0]["maxOrder"] != DBNull.Value)
                {
                    return Convert.ToInt32(result.Rows[0]["maxOrder"]) + 1;
                }
                return 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] GetNextDisplayOrder 실패: {ex.Message}");
                return 1;
            }
        }

        public static void ShiftDisplayOrdersAfter(int subjectId, int afterOrder)
        {
            try
            {
                using var conn = new SqliteConnection(GetConnectionString());
                conn.Open();
                using var transaction = conn.BeginTransaction();

                // 카테고리 순서 업데이트
                var categoryCmd = conn.CreateCommand();
                categoryCmd.Transaction = transaction;
                categoryCmd.CommandText = @"
                UPDATE category 
                SET displayOrder = displayOrder + 1 
                WHERE subjectId = @subjectId AND displayOrder > @afterOrder";
                categoryCmd.Parameters.AddWithValue("@subjectId", subjectId);
                categoryCmd.Parameters.AddWithValue("@afterOrder", afterOrder);
                categoryCmd.ExecuteNonQuery();

                // 텍스트 순서 업데이트
                var contentCmd = conn.CreateCommand();
                contentCmd.Transaction = transaction;
                contentCmd.CommandText = @"
                UPDATE noteContent 
                SET displayOrder = displayOrder + 1 
                WHERE subjectId = @subjectId AND displayOrder > @afterOrder";
                contentCmd.Parameters.AddWithValue("@subjectId", subjectId);
                contentCmd.Parameters.AddWithValue("@afterOrder", afterOrder);
                contentCmd.ExecuteNonQuery();

                transaction.Commit();
                Debug.WriteLine($"[DB] displayOrder 시프트 완료. afterOrder: {afterOrder}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] ShiftDisplayOrdersAfter 실패: {ex.Message}");
            }
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
        public static int InsertCategory(string content, int subjectId, int displayOrder = -1, int timeId = 1)
        {
            try
            {
                if (displayOrder == -1)
                {
                    displayOrder = GetNextDisplayOrder(subjectId);
                }

                using var conn = new SqliteConnection(GetConnectionString());
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                INSERT INTO category (title, subjectId, timeId, displayOrder)
                VALUES (@title, @subjectId, @timeId, @displayOrder);
                SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@title", content);
                cmd.Parameters.AddWithValue("@subjectId", subjectId);
                cmd.Parameters.AddWithValue("@timeId", timeId);
                cmd.Parameters.AddWithValue("@displayOrder", displayOrder);

                var result = cmd.ExecuteScalar();
                int categoryId = Convert.ToInt32(result);

                Debug.WriteLine($"[DB] 새 카테고리 삽입 완료. CategoryId: {categoryId}, DisplayOrder: {displayOrder}");
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
        public static void DeleteCategory(int categoryId, bool deleteTexts = true)
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

                if (deleteTexts)
                {
                    // 관련 noteContent도 삭제
                    var deleteNotesCmd = conn.CreateCommand();
                    deleteNotesCmd.Transaction = transaction;
                    deleteNotesCmd.CommandText = "DELETE FROM noteContent WHERE categoryId = @categoryId";
                    deleteNotesCmd.Parameters.AddWithValue("@categoryId", categoryId);
                    int notesDeleted = deleteNotesCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[DB] 삭제된 노트: {notesDeleted}개");
                }

                // 카테고리만 삭제
                var deleteCategoryCmd = conn.CreateCommand();
                deleteCategoryCmd.Transaction = transaction;
                deleteCategoryCmd.CommandText = "DELETE FROM category WHERE categoryId = @categoryId";
                deleteCategoryCmd.Parameters.AddWithValue("@categoryId", categoryId);
                int categoryDeleted = deleteCategoryCmd.ExecuteNonQuery();

                transaction.Commit();
                Debug.WriteLine($"[DB] 카테고리 삭제 완료. CategoryId: {categoryId}, 텍스트 삭제 여부: {deleteTexts}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] DeleteCategory 실패: {ex.Message}");
            }
        }

        public static void ReassignTextsToCategory(int fromCategoryId, int toCategoryId)
        {
            if (fromCategoryId <= 0 || toCategoryId <= 0)
            {
                Debug.WriteLine($"[WARNING] ReassignTextsToCategory - 유효하지 않은 CategoryId: from={fromCategoryId}, to={toCategoryId}");
                return;
            }

            try
            {
                string query = $@"
            UPDATE noteContent 
            SET categoryId = {toCategoryId}
            WHERE categoryId = {fromCategoryId}";

                int rowsAffected = DatabaseHelper.ExecuteNonQuery(query);
                Debug.WriteLine($"[DB] 텍스트 재할당 완료. {fromCategoryId} -> {toCategoryId}, 영향받은 행: {rowsAffected}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] ReassignTextsToCategory 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 새로운 일반 텍스트 라인 삽입
        /// </summary>
        public static int InsertNewLine(MarkdownLineViewModel line, int displayOrder = -1)
        {
            try
            {
                if (displayOrder == -1)
                {
                    displayOrder = GetNextDisplayOrder(line.SubjectId);
                }

                using var conn = new SqliteConnection(GetConnectionString());
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                INSERT INTO noteContent (content, subjectId, categoryId, displayOrder)
                VALUES (@content, @subjectId, @categoryId, @displayOrder);
                SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@content", line.Content ?? "");
                cmd.Parameters.AddWithValue("@subjectId", line.SubjectId);
                cmd.Parameters.AddWithValue("@categoryId", line.CategoryId);
                cmd.Parameters.AddWithValue("@displayOrder", displayOrder);

                var result = cmd.ExecuteScalar();
                int textId = Convert.ToInt32(result);

                Debug.WriteLine($"[DB] 새 라인 삽입 완료. TextId: {textId}, DisplayOrder: {displayOrder}");
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
                    // CategoryId가 없으면 저장하지 않음
                    if (line.CategoryId <= 0)
                    {
                        Debug.WriteLine($"[WARNING] CategoryId가 유효하지 않아 저장 건너뜀. CategoryId: {line.CategoryId}");
                        return;
                    }

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

        public static void EnsureDefaultCategory(int subjectId)
        {
            try
            {
                // 기본 카테고리가 있는지 확인
                string checkQuery = $"SELECT COUNT(*) as count FROM category WHERE categoryId = 1 AND subjectId = {subjectId}";
                var result = DatabaseHelper.ExecuteSelect(checkQuery);

                if (result.Rows.Count > 0 && Convert.ToInt32(result.Rows[0]["count"]) == 0)
                {
                    // 기본 카테고리 생성
                    string insertQuery = $@"
                INSERT INTO category (categoryId, title, subjectId, displayOrder) 
                VALUES (1, '# 기본', {subjectId}, 0)";
                    DatabaseHelper.ExecuteNonQuery(insertQuery);
                    Debug.WriteLine("[DB] 기본 카테고리 생성됨");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] 기본 카테고리 생성 실패: {ex.Message}");
            }
        }
    }
}