using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace Notea.Modules.Subject.ViewModels
{
    public class NoteEditorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MarkdownLineViewModel> Lines { get; set; }

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

        public void AddNewLine()
        {
            Lines.Add(new MarkdownLineViewModel { IsEditing = true, Content = "" });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        
    }
}
