// App.xaml.cs
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
            Database.DatabaseInitializer.InitializeDatabase();
            Database.DatabaseInitializer.UpdateSchemaForDisplayOrder();
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