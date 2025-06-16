using Notea.Modules.Subject.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Notea.Modules.Subject.ViewModels
{
    public class MarkdownLineViewModel : INotifyPropertyChanged
    {
        private string _content;
        private ObservableCollection<Inline> _inlines = new ObservableCollection<Inline>();
        private double _fontSize = 14;
        private FontWeight _fontWeight = FontWeights.Normal;
        private bool _isHeading = false;
        private int _headingLevel = 0;
        private string _rawContent;
        private bool _isEditing;
        private string _placeholder = "";
        private bool _isComposing = false;
        private bool _hasFocus = false;
        private bool _isDirty = false;
        private DispatcherTimer _saveTimer;

        public int TextId { get; set; } // 기본 키
        public int CategoryId { get; set; } // 제목 줄에 연결
        public int SubjectId { get; set; } // 과목 식별용
        public int Index { get; set; } // 줄 순서

        public bool IsHeadingLine { get; set; } = false;

        public MarkdownLineViewModel()
        {
            InitializeSaveTimer();
            Content = "";
            UpdateInlinesFromContent();
            UpdatePlaceholder();
        }

        private int _displayOrder;
        public int DisplayOrder
        {
            get => _displayOrder;
            set
            {
                if (_displayOrder != value)
                {
                    _displayOrder = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 자동 저장을 위한 타이머 초기화
        /// </summary>
        private void InitializeSaveTimer()
        {
            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000) // 1초 지연 후 저장
            };
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                if (_isDirty)
                {
                    SaveToDatabase();
                    _isDirty = false;
                }
            };
        }

        public string HeadingText
        {
            get
            {
                if (NoteRepository.IsMarkdownHeading(Content))
                {
                    return NoteRepository.ExtractHeadingText(Content);
                }
                return Content;
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasFocus
        {
            get => _hasFocus;
            set
            {
                if (_hasFocus != value)
                {
                    _hasFocus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowPlaceholder));
                }
            }
        }

        public bool IsComposing
        {
            get => _isComposing;
            set
            {
                if (_isComposing != value)
                {
                    _isComposing = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowPlaceholder));
                }
            }
        }

        public string RawContent
        {
            get => _rawContent;
            set
            {
                if (_rawContent != value)
                {
                    _rawContent = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Inline> Inlines
        {
            get => _inlines;
            set
            {
                _inlines = value;
                OnPropertyChanged();
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (_isEditing != value)
                {
                    _isEditing = value;
                    OnPropertyChanged();

                    if (!_isEditing)
                    {
                        IsComposing = false;
                        HasFocus = false;

                        // 편집 완료 시 즉시 저장
                        if (_isDirty)
                        {
                            SaveToDatabase();
                            _isDirty = false;
                        }
                    }
                }
            }
        }

        public string Content
        {
            get => _content;
            set
            {
                var preprocessed = PreprocessContent(value);
                if (_content != preprocessed)
                {
                    string oldContent = _content;
                    _content = preprocessed;

                    // 제목 여부 체크 (화면 표시용 - 모든 제목 레벨)
                    bool wasHeading = IsHeadingLine;
                    bool isMarkdownHeading = NoteRepository.IsMarkdownHeading(_content);

                    // 카테고리로 저장될 제목인지 체크 (# 하나만)
                    IsHeadingLine = NoteRepository.IsCategoryHeading(_content);

                    // 제목 상태가 변경된 경우 처리
                    if (wasHeading != IsHeadingLine)
                    {
                        OnHeadingStatusChanged(wasHeading, IsHeadingLine);
                    }

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HeadingText));
                    ApplyMarkdownStyle();
                    UpdateInlinesFromContent();
                    UpdatePlaceholder();
                    OnPropertyChanged(nameof(ShowPlaceholder));

                    // 변경사항이 있음을 표시하고 자동 저장 타이머 시작
                    IsDirty = true;
                    if (_saveTimer != null)
                    {
                        _saveTimer.Stop();
                        _saveTimer.Start();
                    }
                }
            }
        }

        private void OnHeadingStatusChanged(bool wasHeading, bool isHeading)
        {
            if (wasHeading && !isHeading)
            {
                Debug.WriteLine($"[DEBUG] 제목에서 일반 텍스트로 변경됨: {Content}");

                // 카테고리 삭제 전에 이전 카테고리 찾기
                int previousCategoryId = FindPreviousCategoryId();

                if (previousCategoryId > 0)
                {
                    // 현재 카테고리에 속한 텍스트들을 이전 카테고리로 이동
                    NoteRepository.ReassignTextsToCategory(this.CategoryId, previousCategoryId);
                }

                // 카테고리 삭제 (이제 텍스트들은 안전하게 재할당됨)
                NoteRepository.DeleteCategory(this.CategoryId);

                // 현재 라인을 일반 텍스트로 변환
                CategoryId = previousCategoryId > 0 ? previousCategoryId : 1; // 이전 카테고리 또는 기본값
                TextId = 0; // 새로운 텍스트로 저장될 것임
            }
            else if (!wasHeading && isHeading)
            {
                Debug.WriteLine($"[DEBUG] 일반 텍스트에서 제목으로 변경됨: {Content}");

                // 기존 텍스트 라인 삭제
                if (this.TextId > 0)
                {
                    NoteRepository.DeleteLine(this.TextId);
                }

                CategoryId = 0; // 새로운 카테고리로 저장될 것임
                TextId = 0;
            }
        }

        private int FindPreviousCategoryId()
        {
            // NoteEditorViewModel에서 현재 라인의 위치를 기준으로 이전 카테고리를 찾아야 함
            // 이를 위해 PropertyChanged 이벤트를 통해 ViewModel에 요청
            var args = new FindPreviousCategoryEventArgs { CurrentLine = this };
            OnRequestFindPreviousCategory(args);
            return args.PreviousCategoryId;
        }

        public event EventHandler<FindPreviousCategoryEventArgs> RequestFindPreviousCategory;

        protected virtual void OnRequestFindPreviousCategory(FindPreviousCategoryEventArgs e)
        {
            RequestFindPreviousCategory?.Invoke(this, e);
        }

        public class FindPreviousCategoryEventArgs : EventArgs
        {
            public MarkdownLineViewModel CurrentLine { get; set; }
            public int PreviousCategoryId { get; set; }
        }

        public string Placeholder
        {
            get => _placeholder;
            set
            {
                if (_placeholder != value)
                {
                    _placeholder = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowPlaceholder
        {
            get
            {
                if (_isComposing) return false;
                if (!_hasFocus) return false;

                if (string.IsNullOrWhiteSpace(Content))
                    return true;

                // 제목 기호만 있는 경우 (모든 레벨)
                var headingOnlyPattern = @"^#{1,6}\s*$";
                if (Regex.IsMatch(Content, headingOnlyPattern))
                    return true;

                // 리스트 기호만 있는 경우
                var listOnlyPattern = @"^(\-|\*|\+)\s*$";
                if (Regex.IsMatch(Content, listOnlyPattern))
                    return true;

                // 순서 있는 리스트 기호만 있는 경우
                var orderedListOnlyPattern = @"^\d+\.\s*$";
                if (Regex.IsMatch(Content, orderedListOnlyPattern))
                    return true;

                return false;
            }
        }

        private void UpdatePlaceholder()
        {
            // 제목 레벨에 따른 플레이스홀더
            var headingMatch = Regex.Match(Content, @"^(#{1,6})\s*$");
            if (headingMatch.Success)
            {
                int level = headingMatch.Groups[1].Value.Length;
                Placeholder = level switch
                {
                    1 => "\t제목을 입력하세요",
                    2 => "\t부제목을 입력하세요",
                    3 => "\t섹션 제목을 입력하세요",
                    4 => "\t소제목을 입력하세요",
                    5 => "\t작은 제목을 입력하세요",
                    6 => "\t가장 작은 제목을 입력하세요",
                    _ => "\t제목을 입력하세요"
                };
                return;
            }

            // 리스트에 따른 플레이스홀더
            var listMatch = Regex.Match(Content, @"^(\-|\*|\+)\s*$");
            if (listMatch.Success)
            {
                Placeholder = "목록 항목을 입력하세요";
                return;
            }

            // 순서 있는 리스트
            var orderedListMatch = Regex.Match(Content, @"^(\d+)\.\s*$");
            if (orderedListMatch.Success)
            {
                var number = orderedListMatch.Groups[1].Value;
                Placeholder = $"{number}번 항목을 입력하세요";
                return;
            }

            // 빈 줄일 때
            if (string.IsNullOrWhiteSpace(Content))
            {
                Placeholder = "내용을 입력하세요...";
                return;
            }

            // 그 외의 경우 placeholder 없음
            Placeholder = "";
        }

        // 엔터 처리 시 리스트/제목 정리를 위한 메서드
        public bool ShouldCleanupOnEnter()
        {
            // 리스트 기호만 있는 경우
            if (Regex.IsMatch(Content, @"^(\-|\*|\+)\s*$"))
            {
                Content = ""; // 리스트 기호 제거
                return true;
            }

            // 순서 있는 리스트 기호만 있는 경우
            if (Regex.IsMatch(Content, @"^\d+\.\s*$"))
            {
                Content = ""; // 번호 제거
                return true;
            }

            // 제목 기호만 있는 경우 (모든 레벨)
            if (Regex.IsMatch(Content, @"^#{1,6}\s*$"))
            {
                Content = ""; // 제목 기호 제거
                return true;
            }

            return false;
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public FontWeight FontWeight
        {
            get => _fontWeight;
            set
            {
                if (_fontWeight != value)
                {
                    _fontWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsHeading
        {
            get => _isHeading;
            set
            {
                if (_isHeading != value)
                {
                    _isHeading = value;
                    OnPropertyChanged();
                }
            }
        }

        public int HeadingLevel
        {
            get => _headingLevel;
            set
            {
                if (_headingLevel != value)
                {
                    _headingLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        private Thickness _margin = new Thickness(4);
        public Thickness Margin
        {
            get => _margin;
            set { _margin = value; OnPropertyChanged(); }
        }

        private string PreprocessContent(string input)
        {
            if (input == null) return "";

            return input
                .Replace("/cap ", "∩")
                .Replace("/cup ", "∪")
                .Replace("/inf ", "∞")
                .Replace("/pd ", "∂")
                .Replace("/sum ", "∑")
                .Replace("/int ", "∫")
                .Replace("/sqrt ", "√")
                .Replace("/theta ", "θ")
                .Replace("/pi ", "π")
                .Replace("/mu ", "μ")
                .Replace("/sigma ", "σ")
                .Replace(":. ", "∴")
                .Replace(":> ", "∵")
                .Replace("-> ", "→")
                .Replace("<- ", "←")
                .Replace("!= ", "≠")
                .Replace("~= ", "≈")
                .Replace("<= ", "≤")
                .Replace(">= ", "≥")
                .Replace("ㄴ ", "↳");
        }

        private void ApplyMarkdownStyle()
        {
            if (string.IsNullOrWhiteSpace(Content))
            {
                ResetStyle();
                return;
            }

            // 제목 (# ~ ######) - 모든 레벨 스타일 적용
            var headingMatch = Regex.Match(Content, @"^(#{1,6})\s+(.*)");

            if (headingMatch.Success)
            {
                HeadingLevel = headingMatch.Groups[1].Value.Length;
                IsHeading = true;

                FontWeight = FontWeights.Bold;
                FontSize = HeadingLevel switch
                {
                    1 => 26,
                    2 => 22,
                    3 => 18,
                    4 => 16,
                    5 => 14,
                    6 => 13,
                    _ => 14
                };

                Margin = new Thickness(4);
                return;
            }

            // Bold 처리
            if (Regex.IsMatch(Content, @"^\*\*(.*?)\*\*$"))
            {
                FontWeight = FontWeights.Bold;
            }
            else
            {
                FontWeight = FontWeights.Normal;
            }

            // 리스트 처리
            var listMatch = Regex.Match(Content, @"^(\-|\*)\s+(.*)");
            if (listMatch.Success)
            {
                IsHeading = false;
                HeadingLevel = 0;
                FontWeight = FontWeights.SemiBold;
                FontSize = 14;
                Margin = new Thickness(20, 4, 4, 4);
                return;
            }

            ResetStyle();
            Inlines = MarkdownParser.Parse(Content);
        }

        private void ResetStyle()
        {
            IsHeading = false;
            HeadingLevel = 0;
            FontSize = 14;
            FontWeight = FontWeights.Normal;
            Margin = new Thickness(4);
        }

        public void UpdateInlinesFromContent()
        {
            var newInlines = new ObservableCollection<Inline>();
            var font = new FontFamily(new Uri("pack://application:,,,/"), "./Resources/Fonts/#Pretendard Variable");

            if (string.IsNullOrEmpty(Content))
            {
                Inlines = newInlines;
                return;
            }

            // 1. Heading 처리 (# ~ ######) - 모든 레벨
            var headingMatch = Regex.Match(Content, @"^(#{1,6})\s*(.*)");
            if (headingMatch.Success)
            {
                int level = headingMatch.Groups[1].Value.Length;
                string text = headingMatch.Groups[2].Value;

                double headingFontSize = level switch
                {
                    1 => 26,
                    2 => 22,
                    3 => 18,
                    4 => 16,
                    5 => 14,
                    6 => 13,
                    _ => 14
                };

                var run = new Run(text)
                {
                    FontSize = headingFontSize,
                    FontWeight = FontWeights.Bold,
                    FontFamily = font
                };

                newInlines.Add(run);
                Inlines = newInlines;
                OnPropertyChanged(nameof(Inlines));
                return;
            }

            // 2. 인라인 마크다운 처리
            var pattern = @"(\*\*(.*?)\*\*|\*(.*?)\*|__(.*?)__|~~(.*?)~~)";
            var matches = Regex.Matches(Content, pattern);

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    var plain = Content.Substring(lastIndex, match.Index - lastIndex);
                    newInlines.Add(new Run(plain) { FontFamily = font, FontSize = FontSize });
                }

                if (match.Value.StartsWith("**"))
                {
                    var boldRun = new Run(match.Groups[2].Value)
                    {
                        FontFamily = font,
                        FontSize = FontSize,
                        FontWeight = FontWeights.Bold
                    };
                    newInlines.Add(boldRun);
                }
                else if (match.Value.StartsWith("*"))
                {
                    var italicRun = new Run(match.Groups[3].Value)
                    {
                        FontFamily = font,
                        FontSize = FontSize,
                        FontStyle = FontStyles.Italic
                    };
                    newInlines.Add(italicRun);
                }
                else if (match.Value.StartsWith("__"))
                {
                    var underline = new Run(match.Groups[4].Value)
                    {
                        FontFamily = font,
                        FontSize = FontSize,
                        TextDecorations = TextDecorations.Underline
                    };
                    newInlines.Add(underline);
                }
                else if (match.Value.StartsWith("~~"))
                {
                    var strike = new Run(match.Groups[5].Value)
                    {
                        FontFamily = font,
                        FontSize = FontSize,
                        TextDecorations = TextDecorations.Strikethrough
                    };
                    newInlines.Add(strike);
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < Content.Length)
            {
                newInlines.Add(new Run(Content.Substring(lastIndex)) { FontFamily = font, FontSize = FontSize });
            }

            Inlines = newInlines;
            OnPropertyChanged(nameof(Inlines));
        }
        public class CategoryCreatedEventArgs : EventArgs
        {
            public int NewCategoryId { get; set; }
        }
        public event EventHandler<CategoryCreatedEventArgs> CategoryCreated;

        protected virtual void OnCategoryCreated(int categoryId)
        {
            CategoryCreated?.Invoke(this, new CategoryCreatedEventArgs { NewCategoryId = categoryId });
        }

        private void SaveToDatabase()
        {
            try
            {
                // 부모가 없어도 삭제하지 않고 기본 카테고리 할당
                if (!IsHeadingLine && CategoryId <= 0)
                {
                    Debug.WriteLine($"[DB] CategoryId가 없음. 기본 카테고리 할당. Content: {Content}");
                    CategoryId = 1; // 기본 카테고리
                }

                if (IsHeadingLine)  // # 하나인 카테고리 제목인 경우
                {
                    if (CategoryId <= 0)
                    {
                        int newCategoryId = NoteRepository.InsertCategory(Content, SubjectId, DisplayOrder);
                        CategoryId = newCategoryId;
                        OnCategoryCreated(newCategoryId);
                        Debug.WriteLine($"[DB] 새 제목 생성 완료. CategoryId: {newCategoryId}, DisplayOrder: {DisplayOrder}");
                    }
                    else
                    {
                        NoteRepository.UpdateCategory(CategoryId, Content);
                        Debug.WriteLine($"[DB] 제목 업데이트 완료. CategoryId: {CategoryId}");
                    }
                }
                else
                {
                    // 일반 텍스트 저장
                    if (TextId <= 0)
                    {
                        int newTextId = NoteRepository.InsertNewLine(this, DisplayOrder);
                        TextId = newTextId;
                        Debug.WriteLine($"[DB] 새 텍스트 저장 완료. TextId: {newTextId}, CategoryId: {CategoryId}, DisplayOrder: {DisplayOrder}");
                    }
                    else
                    {
                        NoteRepository.UpdateLine(this);
                        Debug.WriteLine($"[DB] 텍스트 업데이트 완료. TextId: {TextId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB ERROR] 자동 저장 실패: {ex.Message}");
            }
        }

        public void SaveImmediately()
        {
            _saveTimer?.Stop();
            if (_isDirty)
            {
                SaveToDatabase();
                _isDirty = false;
            }
        }

        public void Dispose()
        {
            _saveTimer?.Stop();
            _saveTimer = null;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Content);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}