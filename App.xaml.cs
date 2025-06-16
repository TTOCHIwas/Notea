// App.xaml.cs
using Notea.Database;
using Notea.Modules.Subject.ViewModels;
using System.Windows;

namespace Notea
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 데이터베이스 초기화
            DatabaseInitializer.InitializeDatabase();

            // 스키마 업데이트
            DatabaseInitializer.UpdateSchemaForDisplayOrder();
            Notea.Helpers.DatabaseHelper.UpdateSchemaForHeadingLevel();

            // 기본 카테고리 확인
            Notea.Modules.Subject.Models.NoteRepository.EnsureDefaultCategory(1);

            // 테이블 구조 확인 (디버깅용)
            Notea.Helpers.DatabaseHelper.CheckTableStructure();

            // 전체 데이터 출력 (디버깅용)
            Notea.Helpers.DatabaseHelper.DebugPrintAllData(1);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 프로그램 종료 시 모든 변경사항 저장
            if (MainWindow != null && MainWindow.DataContext is NoteEditorViewModel NoteEditorVm)
            {
                // 모든 열린 에디터의 변경사항 저장
                NoteEditorVm.OnViewClosing();
            }
            
            base.OnExit(e);
        }
    }
}