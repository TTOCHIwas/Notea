using Notea.Modules.Subject.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Notea.Modules.Subject.Views
{
    public partial class NoteEditorView : UserControl
    {
        public NoteEditorView()
        {
            InitializeComponent();
        }

        private bool _isInternalFocusChange = false;

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is MarkdownLineViewModel vm)
            {
                vm.IsEditing = true;
                vm.HasFocus = true; // 포커스 상태 설정
                textBox.CaretIndex = textBox.Text.Length;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isInternalFocusChange)
            {
                return;
            }

            if (sender is TextBox textBox && textBox.DataContext is MarkdownLineViewModel vm)
            {
                vm.HasFocus = false; // 포커스 상태 해제
                vm.IsComposing = false; // IME 조합 상태 리셋
                vm.UpdateInlinesFromContent();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    vm.IsEditing = false;
                }), DispatcherPriority.DataBind);
            }
        }

        // 한글 입력 시 실시간으로 placeholder 숨기기
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is MarkdownLineViewModel vm)
            {
                // 어떤 텍스트든 입력이 시작되면 즉시 조합 상태로 설정
                vm.IsComposing = true;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is MarkdownLineViewModel vm)
            {
                // 텍스트가 실제로 있으면 조합 중이든 아니든 placeholder 숨김
                vm.IsComposing = !string.IsNullOrEmpty(textBox.Text);
                var noteEditorVm = FindParentDataContext<NoteEditorViewModel>(textBox);
                noteEditorVm.UpdateActivity();
            }
        }

        public static T? FindParentDataContext<T>(DependencyObject child) where T : class
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if ((parent as FrameworkElement)?.DataContext is T vm)
                    return vm;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not MarkdownLineViewModel vm)
                return;

            vm.IsEditing = true;

            Dispatcher.InvokeAsync(() =>
            {
                int index = ((NoteEditorViewModel)this.DataContext).Lines.IndexOf(vm);
                editorView.UpdateLayout();

                var container = ItemsControlContainer.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                var textBox = FindVisualChild<TextBox>(container);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.Select(textBox.Text.Length, 0);
                }
            }, DispatcherPriority.Input);
        }

        // 2. NoteEditorView.xaml.cs - 단축키 및 리스트 처리 확장
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var lineVM = textBox.DataContext as MarkdownLineViewModel;
            if (lineVM == null) return;

            var vm = this.DataContext as NoteEditorViewModel;
            if (vm == null) return;

            // 한글 입력 중 ESC 키로 조합 취소 시 처리
            if (e.Key == Key.Escape)
            {
                lineVM.IsComposing = false;
            }
            // Enter 키 처리 - 리스트 자동 계속
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                HandleEnterWithList(vm, lineVM);
            }
            else if (e.Key == Key.Back)
            {
                if (lineVM.IsComposing && textBox.Text.Length <= 1)
                {
                    lineVM.IsComposing = false;
                }
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
                    // 헤딩 단축키 추가
                    case Key.D1:
                        e.Handled = HandleHeadingShortcut(textBox, 1);
                        break;
                    case Key.D2:
                        e.Handled = HandleHeadingShortcut(textBox, 2);
                        break;
                    case Key.D3:
                        e.Handled = HandleHeadingShortcut(textBox, 3);
                        break;
                    case Key.D4:
                        e.Handled = HandleHeadingShortcut(textBox, 4);
                        break;
                    case Key.D5:
                        e.Handled = HandleHeadingShortcut(textBox, 5);
                        break;
                    case Key.D6:
                        e.Handled = HandleHeadingShortcut(textBox, 6);
                        break;
                    // 리스트 토글
                    case Key.L:
                        e.Handled = HandleListToggle(textBox);
                        break;
                }
            }
            // 방향키 네비게이션
            else if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = HandleArrowNavigation(vm, textBox, lineVM, e.Key);
            }
        }

        /// <summary>
        /// 헤딩 단축키 처리 (Ctrl+1~6)
        /// </summary>
        private bool HandleHeadingShortcut(TextBox textBox, int level)
        {
            string prefix = new string('#', level) + " ";

            // 현재 줄이 이미 헤딩인지 확인
            var headingMatch = Regex.Match(textBox.Text, @"^(#{1,6})\s+(.*)");
            if (headingMatch.Success)
            {
                // 기존 헤딩 레벨 변경
                string content = headingMatch.Groups[2].Value;
                textBox.Text = prefix + content;
                textBox.CaretIndex = textBox.Text.Length;
            }
            else
            {
                // 새로운 헤딩으로 변환
                textBox.Text = prefix + textBox.Text;
                textBox.CaretIndex = textBox.Text.Length;
            }

            return true;
        }

        /// <summary>
        /// 리스트 토글 (Ctrl+L)
        /// </summary>
        private bool HandleListToggle(TextBox textBox)
        {
            var lineVM = textBox.DataContext as MarkdownLineViewModel;
            if (lineVM == null) return false;

            // 이미 리스트인 경우 해제
            if (lineVM.IsList)
            {
                // 리스트 기호 제거
                var listPattern = @"^(\-|\*|\+|\d+\.)\s+";
                textBox.Text = Regex.Replace(textBox.Text, listPattern, "");
                textBox.CaretIndex = 0;
            }
            else
            {
                // 리스트로 변환
                textBox.Text = "- " + textBox.Text;
                textBox.CaretIndex = textBox.Text.Length;
            }

            return true;
        }

        /// <summary>
        /// Enter 키 처리 - 리스트 자동 계속
        /// </summary>
        private void HandleEnterWithList(NoteEditorViewModel vm, MarkdownLineViewModel currentLine)
        {
            _isInternalFocusChange = true;

            // 리스트나 제목 기호만 있으면 제거
            if (currentLine.ShouldCleanupOnEnter())
            {
                // 리스트 해제
                currentLine.IsList = false;
                currentLine.ListSymbol = "";
            }

            var currentIndex = vm.Lines.IndexOf(currentLine);

            // 현재 라인의 편집 모드 종료
            currentLine.IsEditing = false;

            // 새 라인 생성
            vm.InsertNewLineAt(currentIndex + 1);
            var newLine = vm.Lines[currentIndex + 1];

            // 리스트 자동 계속
            if (currentLine.IsList && !string.IsNullOrWhiteSpace(currentLine.Content)
                && !currentLine.ShouldCleanupOnEnter())
            {
                string nextPrefix = currentLine.GetNextListPrefix();
                if (!string.IsNullOrEmpty(nextPrefix))
                {
                    newLine.Content = nextPrefix;
                }
            }

            // 새 라인에 포커스
            Dispatcher.InvokeAsync(() =>
            {
                editorView.UpdateLayout();

                var newContainer = ItemsControlContainer.ItemContainerGenerator
                    .ContainerFromIndex(currentIndex + 1) as FrameworkElement;

                if (newContainer != null)
                {
                    var newTextBox = FindVisualChild<TextBox>(newContainer);
                    if (newTextBox != null)
                    {
                        newTextBox.Focus();
                        // 리스트 prefix가 있으면 커서를 끝으로
                        if (!string.IsNullOrEmpty(newLine.Content))
                        {
                            newTextBox.CaretIndex = newTextBox.Text.Length;
                        }
                    }
                }

                _isInternalFocusChange = false;
            }, DispatcherPriority.Input);
        }

        private bool HandleArrowNavigation(NoteEditorViewModel vm, TextBox textBox, MarkdownLineViewModel lineVM, Key key)
        {
            int currentIndex = vm.Lines.IndexOf(lineVM);
            int caretPos = textBox.CaretIndex;
            int textLength = textBox.Text.Length;

            switch (key)
            {
                case Key.Up:
                    if (currentIndex > 0)
                    {
                        return MoveToLine(vm, currentIndex - 1, caretPos);
                    }
                    break;

                case Key.Down:
                    if (currentIndex < vm.Lines.Count - 1)
                    {
                        return MoveToLine(vm, currentIndex + 1, caretPos);
                    }
                    break;

                case Key.Left:
                    if (caretPos == 0 && currentIndex > 0)
                    {
                        return MoveToLine(vm, currentIndex - 1, -1);
                    }
                    break;

                case Key.Right:
                    if (caretPos == textLength && currentIndex < vm.Lines.Count - 1)
                    {
                        return MoveToLine(vm, currentIndex + 1, 0);
                    }
                    break;
            }

            return false;
        }

        private bool MoveToLine(NoteEditorViewModel vm, int targetIndex, int caretPosition)
        {
            if (targetIndex < 0 || targetIndex >= vm.Lines.Count)
                return false;

            var currentLine = vm.Lines.FirstOrDefault(l => l.IsEditing);
            var targetLine = vm.Lines[targetIndex];

            if (currentLine == targetLine)
                return false;

            _isInternalFocusChange = true;

            targetLine.IsEditing = true;

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

                        if (caretPosition == -1)
                        {
                            targetTextBox.CaretIndex = targetTextBox.Text.Length;
                        }
                        else if (caretPosition >= 0)
                        {
                            targetTextBox.CaretIndex = Math.Min(caretPosition, targetTextBox.Text.Length);
                        }

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
            }, DispatcherPriority.Render);

            return true;
        }

        private bool HandleMarkdownToggle(TextBox textBox, string markdownSymbol)
        {
            if (textBox.SelectionLength == 0)
            {
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

            bool hasExactMarkdownBefore = false;
            bool hasExactMarkdownAfter = false;

            if (selectionStart >= symbolLength && selectionEnd + symbolLength <= fullText.Length)
            {
                string beforeSymbol = fullText.Substring(selectionStart - symbolLength, symbolLength);
                string afterSymbol = fullText.Substring(selectionEnd, symbolLength);

                hasExactMarkdownBefore = beforeSymbol == markdownSymbol;
                hasExactMarkdownAfter = afterSymbol == markdownSymbol;

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
                textBox.Text = fullText.Remove(selectionEnd, symbolLength)
                                      .Remove(selectionStart - symbolLength, symbolLength);

                textBox.SelectionStart = selectionStart - symbolLength;
                textBox.SelectionLength = selectedText.Length;
            }
            else
            {
                string formattedText = markdownSymbol + selectedText + markdownSymbol;
                textBox.Text = fullText.Remove(selectionStart, textBox.SelectionLength)
                                      .Insert(selectionStart, formattedText);

                textBox.SelectionStart = selectionStart;
                textBox.SelectionLength = formattedText.Length;
            }

            return true;
        }

        private bool HandleBoldShortcut(TextBox textBox) => HandleMarkdownToggle(textBox, "**");
        private bool HandleItalicShortcut(TextBox textBox) => HandleMarkdownToggle(textBox, "*");
        private bool HandleUnderlineShortcut(TextBox textBox) => HandleMarkdownToggle(textBox, "__");
        private bool HandleStrikethroughShortcut(TextBox textBox) => HandleMarkdownToggle(textBox, "~~");

        private void HandleEnter(NoteEditorViewModel vm)
        {
            _isInternalFocusChange = true;

            // 현재 편집 중인 라인 찾기
            var currentLine = vm.Lines.FirstOrDefault(l => l.IsEditing);
            if (currentLine == null)
            {
                vm.AddNewLine();
                return;
            }

            var currentIndex = vm.Lines.IndexOf(currentLine);

            // 현재 라인의 편집 모드 종료 (자동 저장됨)
            currentLine.IsEditing = false;

            // 중요: 현재 라인 바로 다음에 새 라인 삽입
            vm.InsertNewLineAt(currentIndex + 1);

            // 새로 삽입된 라인에 포커스
            Dispatcher.InvokeAsync(() =>
            {
                editorView.UpdateLayout();

                var newContainer = ItemsControlContainer.ItemContainerGenerator
                    .ContainerFromIndex(currentIndex + 1) as FrameworkElement;

                if (newContainer != null)
                {
                    var newTextBox = FindVisualChild<TextBox>(newContainer);
                    if (newTextBox != null)
                    {
                        newTextBox.Focus();
                    }
                }

                _isInternalFocusChange = false;
            }, DispatcherPriority.Input);
        }

        private bool HandleBackspace(NoteEditorViewModel vm, TextBox textBox, MarkdownLineViewModel lineVM)
        {
            if (textBox.CaretIndex > 0 || !string.IsNullOrWhiteSpace(textBox.Text) || vm.Lines.Count <= 1)
                return false;

            int index = vm.Lines.IndexOf(lineVM);

            // 라인 삭제 (자동으로 데이터베이스에서도 삭제됨)
            vm.RemoveLine(lineVM);

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

            container.BringIntoView();
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

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