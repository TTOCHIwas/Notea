using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

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

        public MarkdownLineViewModel()
        {
            Content = "";
            UpdateInlinesFromContent();
            UpdatePlaceholder();
        }

        // 포커스 상태 추가
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

        // IME 조합 중 여부
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

                    // 편집 모드가 끝나면 조합 상태와 포커스 리셋
                    if (!_isEditing)
                    {
                        IsComposing = false;
                        HasFocus = false;
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
                    _content = preprocessed;
                    OnPropertyChanged();
                    ApplyMarkdownStyle();
                    UpdateInlinesFromContent();
                    UpdatePlaceholder();
                    OnPropertyChanged(nameof(ShowPlaceholder));
                }
            }
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
                // 조합 중이면 무조건 숨기기
                if (_isComposing) return false;

                // 포커스가 없으면 숨기기
                if (!_hasFocus) return false;

                // Content가 비어있을 때만 표시
                if (string.IsNullOrWhiteSpace(Content))
                    return true;

                // 제목 기호만 있는 경우
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

            // 제목 기호만 있는 경우
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

            // 제목 (# ~ ######)
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

            if (Regex.IsMatch(Content, @"^\*\*(.*?)\*\*$"))
            {
                FontWeight = FontWeights.Bold;
            }
            else
            {
                FontWeight = FontWeights.Normal;
            }

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

            // 1. Heading 처리
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

        public bool IsEmpty => string.IsNullOrWhiteSpace(Content);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}