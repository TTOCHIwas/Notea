using Notea.Modules.Subject.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Notea.Modules.Subject.Views
{
    /// <summary>
    /// NoteEditorView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NoteEditorView : UserControl
    {
        public NoteEditorView()
        {
            InitializeComponent();
            this.DataContext = new NoteEditorViewModel();
        }

        private bool _isInternalFocusChange = false;

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("=== TextBox_GotFocus ===");
            if (sender is TextBox textBox && textBox.DataContext is MarkdownLineViewModel vm)
            {
                vm.IsEditing = true;
                textBox.CaretIndex = textBox.Text.Length;
                Debug.WriteLine($"IsEditing set to: {vm.IsEditing}");
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("=== TextBox_LostFocus ===");

            // 내부 포커스 변경 중이면 처리하지 않음
            if (_isInternalFocusChange)
            {
                Debug.WriteLine("Internal focus change, skipping...");
                return;
            }

            if (sender is TextBox textBox && textBox.DataContext is MarkdownLineViewModel vm)
            {
                Debug.WriteLine($"Content before update: {vm.Content}");
                Debug.WriteLine($"Inlines count before: {vm.Inlines.Count}");

                // 먼저 Inlines를 업데이트
                vm.UpdateInlinesFromContent();

                Debug.WriteLine($"Inlines count after: {vm.Inlines.Count}");
                foreach (var inline in vm.Inlines)
                {
                    Debug.WriteLine($"  Inline type: {inline.GetType().Name}");
                }

                // 약간의 지연 후 IsEditing을 false로 설정
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    vm.IsEditing = false;
                    Debug.WriteLine($"IsEditing set to: {vm.IsEditing}");
                }), System.Windows.Threading.DispatcherPriority.DataBind);
            }
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("=== TextBlock_MouseDown ===");
            if (sender is not FrameworkElement fe || fe.DataContext is not MarkdownLineViewModel vm)
                return;

            vm.IsEditing = true;

            Dispatcher.InvokeAsync(() =>
            {
                int index = ((NoteEditorViewModel)this.DataContext).Lines.IndexOf(vm);
                editorView.UpdateLayout();

                var container = ItemsControlContainer.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                var textBox = FindVisualChild<TextBox>(container);
                textBox?.Focus();
                textBox?.Select(textBox.Text.Length, 0);
            }, DispatcherPriority.Input);
        }

        private void TextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("=== TextBlock_Loaded ===");
            if (sender is TextBlock textBlock && textBlock.DataContext is MarkdownLineViewModel vm)
            {
                Debug.WriteLine($"Content: {vm.Content}");
                Debug.WriteLine($"Inlines count: {vm.Inlines.Count}");

                if (string.IsNullOrWhiteSpace(vm.Content))
                {
                    Debug.WriteLine("Content is empty, returning");
                    return;
                }

                if (vm.Inlines.Count == 0)
                {
                    Debug.WriteLine("Inlines empty, updating...");
                    vm.UpdateInlinesFromContent();
                }

                textBlock.Inlines.Clear();
                Debug.WriteLine($"Adding {vm.Inlines.Count} inlines to TextBlock");

                foreach (var inline in vm.Inlines)
                {
                    Debug.WriteLine($"  Adding inline: {inline.GetType().Name}");
                    textBlock.Inlines.Add(inline);
                }

                Debug.WriteLine($"TextBlock now has {textBlock.Inlines.Count} inlines");
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox ||
                textBox.DataContext is not MarkdownLineViewModel lineVM ||
                this.DataContext is not NoteEditorViewModel vm)
                return;

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                HandleEnter(vm);
            }
            else if (e.Key == Key.Back)
            {
                e.Handled = HandleBackspace(vm, textBox, lineVM);
            }
            else if (e.Key == Key.B && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = HandleBoldShortcut(textBox);
            }
        }

        private void HandleEnter(NoteEditorViewModel vm)
        {
            _isInternalFocusChange = true;

            // 현재 라인의 인덱스 저장
            var currentLine = vm.Lines.LastOrDefault();
            var currentIndex = currentLine != null ? vm.Lines.IndexOf(currentLine) : -1;

            // 새 라인 추가 (IsEditing = true로 생성됨)
            vm.AddNewLine();

            // 즉시 새 TextBox에 포커스 설정 시도
            Dispatcher.InvokeAsync(() =>
            {
                // 새 라인의 컨테이너가 생성될 때까지 대기
                editorView.UpdateLayout();

                var newContainer = ItemsControlContainer.ItemContainerGenerator.ContainerFromIndex(vm.Lines.Count - 1) as FrameworkElement;
                if (newContainer != null)
                {
                    var newTextBox = FindVisualChild<TextBox>(newContainer);
                    if (newTextBox != null)
                    {
                        newTextBox.Focus();

                        // 포커스가 설정된 후에 이전 라인의 IsEditing을 false로
                        if (currentLine != null)
                        {
                            currentLine.IsEditing = false;
                        }
                    }
                }

                _isInternalFocusChange = false;
            }, DispatcherPriority.Input);  // 우선순위를 Input으로 변경
        }

        private void TryFocusNewTextBox(int index, int retries)
        {
            var container = ItemsControlContainer.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
            var textBox = FindVisualChild<TextBox>(container);

            if (textBox != null)
            {
                textBox.Focus();
                textBox.CaretIndex = textBox.Text.Length;
                return;
            }

            if (retries > 0)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    TryFocusNewTextBox(index, retries - 1);
                }, DispatcherPriority.Background);
            }
        }

        private bool HandleBackspace(NoteEditorViewModel vm, TextBox textBox, MarkdownLineViewModel lineVM)
        {
            if (textBox.CaretIndex > 0 || !string.IsNullOrWhiteSpace(textBox.Text) || vm.Lines.Count <= 1)
                return false;

            int index = vm.Lines.IndexOf(lineVM);
            vm.Lines.Remove(lineVM);

            Dispatcher.BeginInvoke(() =>
            {
                editorView.UpdateLayout();
                FocusTextBoxAtIndex(Math.Max(0, index - 1));
            }, DispatcherPriority.ApplicationIdle);

            return true;
        }

        private bool HandleBoldShortcut(TextBox textBox)
        {

            if (textBox.SelectionLength == 0) return false;

            string selected = textBox.SelectedText;
            string bolded = $"**{selected}**";
            int start = textBox.SelectionStart;

            textBox.Text = textBox.Text.Remove(start, textBox.SelectionLength).Insert(start, bolded);
            textBox.SelectionStart = start;
            textBox.SelectionLength = bolded.Length;

            return true;
        }
        private void FocusTextBoxAtIndex(int index)
        {
            var container = ItemsControlContainer.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
            if (container == null)
            {
                Dispatcher.InvokeAsync(() => FocusTextBoxAtIndex(index), DispatcherPriority.Background);
                return;
            }

            var textBox = FindVisualChild<TextBox>(container);

            if (textBox != null)
            {
                textBox.Focus();
                textBox.CaretIndex = textBox.Text.Length;
            }

            // Scroll into view
            container.BringIntoView(); // 추가
        }

        private void TextBlock_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            Debug.WriteLine("=== TextBlock_TargetUpdated ===");
            if (sender is not TextBlock textBlock || textBlock.DataContext is not MarkdownLineViewModel vm)
                return;

            if (string.IsNullOrWhiteSpace(vm.Content))
            {
                return;
            }

            if (vm.Inlines.Count == 0)
                vm.UpdateInlinesFromContent();

            textBlock.Inlines.Clear();
            foreach (var inline in vm.Inlines)
                textBlock.Inlines.Add(inline);
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T target)
                    return target;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}