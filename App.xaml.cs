using Microsoft.Data.Sqlite;
using Notea.Database;
using Notea.Helpers;
using Notea.Modules.Subject.Models;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Notea
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            InitializeDatabase();

            // DatabaseHelper의 정적 생성자가 DB를 초기화함
            bool isConnected = DatabaseHelper.TestConnection();

            if (!isConnected)
            {
                MessageBox.Show("데이터베이스 연결에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // DB 경로 확인
            string dbPath = DatabaseHelper.GetDatabasePath();
            if (File.Exists(dbPath))
            {
                System.Diagnostics.Debug.WriteLine($"[DB] 데이터베이스 파일 확인됨: {dbPath}");
                System.Diagnostics.Debug.WriteLine($"[DB] 파일 크기: {new FileInfo(dbPath).Length} bytes");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DB] 데이터베이스 파일이 존재하지 않음: {dbPath}");
            }
        }
        private void InitializeDatabase()
        {
            // 데이터베이스 초기화
            DatabaseInitializer.InitializeDatabase(); // 기존 초기화 메서드가 있다면
            DatabaseInitializer.UpdateSchemaForDisplayOrder();

            // 각 과목에 대해 기본 카테고리 확인
            NoteRepository.EnsureDefaultCategory(1); // subjectId = 1

            Debug.WriteLine("[APP] 데이터베이스 초기화 완료");
        }
    }
}