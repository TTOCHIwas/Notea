using Notea.Modules.Subject.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace Notea.Modules.Subject.ViewModels
{
    public class NoteEditorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MarkdownLineViewModel> Lines { get; set; }

        public int SubjectId { get; set; } = 1;
        public int CurrentCategoryId { get; set; } = 1;

        public NoteEditorViewModel()
        {
            Lines = new ObservableCollection<MarkdownLineViewModel>
            {
                new MarkdownLineViewModel { IsEditing = false }
            };

            Lines.CollectionChanged += (s, e) =>
            {
                if (Lines.Count == 0)
                {
                    Lines.Add(new MarkdownLineViewModel());
                }
            };
        }

        public NoteEditorViewModel(List<NoteCategory> loadedNotes)
        {
            Lines = new ObservableCollection<MarkdownLineViewModel>();

            foreach (var category in loadedNotes)
            {
                Lines.Add(new MarkdownLineViewModel
                {
                    Content = category.Title,
                    IsEditing = false
                });

                foreach (var line in category.Lines)
                {
                    Lines.Add(new MarkdownLineViewModel
                    {
                        Content = line.Content,
                        IsEditing = false
                    });
                }
            }

            Debug.WriteLine($"[DEBUG] 초기화된 라인 수: {Lines.Count}");

            Lines.CollectionChanged += (s, e) =>
            {
                if (Lines.Count == 0)
                {
                    Lines.Add(new MarkdownLineViewModel());
                }
            };
        }

        public void AddNewLine()
        {
            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",

                SubjectId = this.SubjectId,
                CategoryId = this.CurrentCategoryId,
                Index = Lines.Count
            };

            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
            }

            Lines.Add(newLine);

            NoteRepository.SaveOrInsertNoteLine(newLine);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        
    }
}
