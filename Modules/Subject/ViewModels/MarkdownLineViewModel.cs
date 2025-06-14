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
        private string _placeholder = "텍스트를 입력하세요...";

        public MarkdownLineViewModel()
        {
            Content = "";
            UpdateInlinesFromContent();
            UpdatePlaceholder();
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
                // Content가 비어있거나 특정 패턴만 있을 때 true
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

                // 코드 블록 시작만 있는 경우
                if (Content == "```")
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
                    1 => "제목을 입력하세요",
                    2 => "부제목을 입력하세요",
                    3 => "섹션 제목을 입력하세요",
                    4 => "소제목을 입력하세요",
                    5 => "작은 제목을 입력하세요",
                    6 => "가장 작은 제목을 입력하세요",
                    _ => "제목을 입력하세요"
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

            // 코드 블록
            if (Content == "```")
            {
                Placeholder = "코드를 입력하세요";
                return;
            }

            // 시간대별 기본 플레이스홀더
            var hour = DateTime.Now.Hour;
            if (hour >= 5 && hour < 12)
            {
                var morningPlaceholders = new[]
                {
                    "오늘 어떤 걸 배우셨나요",
                    "내용을 입력하세요",
                    "오늘 가장 어려웠던 건 무엇인가요"
                };
                Placeholder = morningPlaceholders[Math.Abs(GetHashCode()) % morningPlaceholders.Length];
            }
            else if (hour >= 12 && hour < 18)
            {
                var afternoonPlaceholders = new[]
                {
                    "오후의 공부를 기록하세요",
                    "무엇을 기록하시겠어요?",
                    "생각을 정리해보세요"
                };
                Placeholder = afternoonPlaceholders[Math.Abs(GetHashCode()) % afternoonPlaceholders.Length];
            }
            else if (hour >= 18 && hour < 22)
            {
                var eveningPlaceholders = new[]
                {
                    "하루를 정리해보세요",
                    "저녁의 생각을 적어보세요",
                    "오늘의 배움을 기록하세요"
                };
                Placeholder = eveningPlaceholders[Math.Abs(GetHashCode()) % eveningPlaceholders.Length];
            }
            else
            {
                var nightPlaceholders = new[]
                {
                    "공부는 역시 밤이 잘 돼요",
                    "조용한 새벽이 공부가 잘 돼요",
                };
                Placeholder = nightPlaceholders[Math.Abs(GetHashCode()) % nightPlaceholders.Length];
            }
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
            return input
                .Replace("->", "→")
                .Replace("=>", "⇒")
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
                Margin = new Thickness(1);

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
                Debug.WriteLine("[DEBUG] Content is null or empty.");
                Inlines = newInlines;
                return;
            }

            // 1. Heading 처리 먼저
            var headingMatch = Regex.Match(Content, @"^(#{1,6})\s*(.*)");
            if (headingMatch.Success)
            {
                int level = headingMatch.Groups[1].Value.Length;
                string text = headingMatch.Groups[2].Value;

                // 헤딩 레벨에 따른 폰트 크기 계산
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
                Inlines = newInlines; // 전체 교체
                OnPropertyChanged(nameof(Inlines)); // 명시적으로 PropertyChanged 발생
                return; // 헤딩이면 여기서 종료 (인라인 마크다운 안 함)
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
                    var boldRun = new Run(match.Groups[2].Value) { FontFamily = font, FontSize = FontSize };
                    newInlines.Add(new Bold(boldRun) { FontFamily = font });
                }
                else if (match.Value.StartsWith("*"))
                {
                    var italicRun = new Run(match.Groups[3].Value) { FontFamily = font, FontSize = FontSize };
                    newInlines.Add(new Italic(italicRun) { FontFamily = font });
                }
                else if (match.Value.StartsWith("__"))
                {
                    var underline = new Run(match.Groups[4].Value) { FontFamily = font, FontSize = FontSize };
                    underline.TextDecorations = TextDecorations.Underline;
                    newInlines.Add(underline);
                }
                else if (match.Value.StartsWith("~~"))
                {
                    var strike = new Run(match.Groups[5].Value) { FontFamily = font, FontSize = FontSize };
                    strike.TextDecorations = TextDecorations.Strikethrough;
                    newInlines.Add(strike);
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < Content.Length)
            {
                newInlines.Add(new Run(Content.Substring(lastIndex)) { FontFamily = font, FontSize = FontSize });
            }

            Inlines = newInlines; // 전체 교체
            OnPropertyChanged(nameof(Inlines)); // 명시적으로 PropertyChanged 발생
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(FontWeight));
            OnPropertyChanged(nameof(Margin));
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Content);

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}