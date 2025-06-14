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
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var lineVM = textBox.DataContext as MarkdownLineViewModel;
            if (lineVM == null) return;

            var vm = this.DataContext as NoteEditorViewModel;
            if (vm == null) return;

            // 기존 Enter, Backspace 처리
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                HandleEnter(vm);
            }
            else if (e.Key == Key.Back)
            {
                e.Handled = HandleBackspace(vm, textBox, lineVM);
            }
            // 마크다운 단축키 처리
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                switch (e.Key)
                {
                    case Key.B:
                        e.Handled = HandleBoldShortcut(textBox);
                        break;
                    case Key.I:
                        e.Handled = HandleItalicShortcut(textBox);
                        break;
                    case Key.U:
                        e.Handled = HandleUnderlineShortcut(textBox);
                        break;
                    case Key.X when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                        e.Handled = HandleStrikethroughShortcut(textBox);
                        break;
                }
            }
            // 방향키 네비게이션 추가
            else if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = HandleArrowNavigation(vm, textBox, lineVM, e.Key);
            }
        }

        // 방향키 네비게이션 처리
        private bool HandleArrowNavigation(NoteEditorViewModel vm, TextBox textBox, MarkdownLineViewModel lineVM, Key key)
        {
            int currentIndex = vm.Lines.IndexOf(lineVM);
            int caretPos = textBox.CaretIndex;
            int textLength = textBox.Text.Length;

            switch (key)
            {
                case Key.Up:
                    // 첫 번째 줄이 아니면 위로 이동
                    if (currentIndex > 0)
                    {
                        return MoveToLine(vm, currentIndex - 1, caretPos);
                    }
                    break;

                case Key.Down:
                    // 마지막 줄이 아니면 아래로 이동
                    if (currentIndex < vm.Lines.Count - 1)
                    {
                        return MoveToLine(vm, currentIndex + 1, caretPos);
                    }
                    break;

                case Key.Left:
                    // 커서가 맨 앞이고 첫 번째 줄이 아니면 이전 줄 끝으로
                    if (caretPos == 0 && currentIndex > 0)
                    {
                        return MoveToLine(vm, currentIndex - 1, -1); // -1은 줄 끝을 의미
                    }
                    break;

                case Key.Right:
                    // 커서가 맨 끝이고 마지막 줄이 아니면 다음 줄 시작으로
                    if (caretPos == textLength && currentIndex < vm.Lines.Count - 1)
                    {
                        return MoveToLine(vm, currentIndex + 1, 0);
                    }
                    break;
            }

            return false;
        }

        // 특정 라인으로 이동 (부드러운 전환 버전)
        private bool MoveToLine(NoteEditorViewModel vm, int targetIndex, int caretPosition)
        {
            if (targetIndex < 0 || targetIndex >= vm.Lines.Count)
                return false;

            var currentLine = vm.Lines.FirstOrDefault(l => l.IsEditing);
            var targetLine = vm.Lines[targetIndex];

            if (currentLine == targetLine)
                return false;

            // 내부 포커스 변경 플래그 설정
            _isInternalFocusChange = true;

            // 타겟 라인을 먼저 편집 모드로 설정 (깜빡임 방지)
            targetLine.IsEditing = true;

            // 짧은 지연 후 포커스 이동 (템플릿 전환 시간 확보)
            Dispatcher.InvokeAsync(() =>
            {
                editorView.UpdateLayout();

                var container = ItemsControlContainer.ItemContainerGenerator.ContainerFromIndex(targetIndex) as FrameworkElement;
                if (container != null)
                {
                    var targetTextBox = FindVisualChild<TextBox>(container);
                    if (targetTextBox != null)
                    {
                        targetTextBox.Focus();

                        // 커서 위치 설정
                        if (caretPosition == -1)
                        {
                            targetTextBox.CaretIndex = targetTextBox.Text.Length;
                        }
                        else if (caretPosition >= 0)
                        {
                            targetTextBox.CaretIndex = Math.Min(caretPosition, targetTextBox.Text.Length);
                        }

                        // 이전 라인의 편집 모드를 나중에 해제 (포커스 이동 후)
                        if (currentLine != null && currentLine != targetLine)
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                currentLine.IsEditing = false;
                                _isInternalFocusChange = false;
                            }, DispatcherPriority.Background);
                        }

                        container.BringIntoView();
                    }
                }
            }, DispatcherPriority.Render); // Input보다 빠른 우선순위

            return true;
        }

        // 마크다운 토글 처리 (개선된 버전)
        private bool HandleMarkdownToggle(TextBox textBox, string markdownSymbol)
        {
            if (textBox.SelectionLength == 0)
            {
                // 선택한 텍스트가 없으면 마크다운 기호만 삽입하고 커서를 중간에 위치
                int caretPos = textBox.CaretIndex;
                textBox.Text = textBox.Text.Insert(caretPos, markdownSymbol + markdownSymbol);
                textBox.CaretIndex = caretPos + markdownSymbol.Length;
                return true;
            }

            string fullText = textBox.Text;
            string selectedText = textBox.SelectedText;
            int selectionStart = textBox.SelectionStart;
            int selectionEnd = selectionStart + textBox.SelectionLength;
            int symbolLength = markdownSymbol.Length;

            // 선택 영역 앞뒤로 정확히 해당 마크다운 기호가 있는지 확인
            bool hasExactMarkdownBefore = false;
            bool hasExactMarkdownAfter = false;

            if (selectionStart >= symbolLength && selectionEnd + symbolLength <= fullText.Length)
            {
                string beforeSymbol = fullText.Substring(selectionStart - symbolLength, symbolLength);
                string afterSymbol = fullText.Substring(selectionEnd, symbolLength);

                hasExactMarkdownBefore = beforeSymbol == markdownSymbol;
                hasExactMarkdownAfter = afterSymbol == markdownSymbol;

                // ** 와 * 구분하기 위한 추가 체크
                if (markdownSymbol == "*" && hasExactMarkdownBefore && selectionStart >= 2)
                {
                    if (fullText[selectionStart - 2] == '*')
                        hasExactMarkdownBefore = false;
                }
                if (markdownSymbol == "*" && hasExactMarkdownAfter && selectionEnd + 1 < fullText.Length)
                {
                    if (fullText[selectionEnd + 1] == '*')
                        hasExactMarkdownAfter = false;
                }
            }

            if (hasExactMarkdownBefore && hasExactMarkdownAfter)
            {
                // 마크다운 제거
                textBox.Text = fullText.Remove(selectionEnd, symbolLength)
                                      .Remove(selectionStart - symbolLength, symbolLength);

                textBox.SelectionStart = selectionStart - symbolLength;
                textBox.SelectionLength = selectedText.Length;
            }
            else
            {
                // 마크다운 추가
                string formattedText = markdownSymbol + selectedText + markdownSymbol;
                textBox.Text = fullText.Remove(selectionStart, textBox.SelectionLength)
                                      .Insert(selectionStart, formattedText);

                textBox.SelectionStart = selectionStart;
                textBox.SelectionLength = formattedText.Length;
            }

            return true;
        }

        // 각 마크다운 단축키 처리
        private bool HandleBoldShortcut(TextBox textBox)
        {
            return HandleMarkdownToggle(textBox, "**");
        }

        private bool HandleItalicShortcut(TextBox textBox)
        {
            return HandleMarkdownToggle(textBox, "*");
        }

        private bool HandleUnderlineShortcut(TextBox textBox)
        {
            return HandleMarkdownToggle(textBox, "__");
        }

        private bool HandleStrikethroughShortcut(TextBox textBox)
        {
            return HandleMarkdownToggle(textBox, "~~");
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