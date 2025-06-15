using Notea.Modules.Subject.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notea.Modules.Subject.ViewModels
{
    public class NotePageViewModel : INotifyPropertyChanged
    {
        public NoteEditorViewModel EditorViewModel { get; set; }
        public string SubjectTitle { get; set; }

        public NotePageViewModel()
        {
            SubjectTitle = "윈도우즈 프로그래밍";
            LoadNote(1); // subjectId = 1 예시
            OnPropertyChanged(nameof(SubjectTitle));
        }

        private void LoadNote(int subjectId)
        {
            var noteData = NoteRepository.LoadNotesBySubject(subjectId);

            // NoteEditorViewModel에 전달
            EditorViewModel = new NoteEditorViewModel(noteData);
            OnPropertyChanged(nameof(EditorViewModel));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
