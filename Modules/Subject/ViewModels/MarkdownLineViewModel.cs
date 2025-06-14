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

        public MarkdownLineViewModel()
        {
            Content = "";
            UpdateInlinesFromContent();
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
                }
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

            Debug.WriteLine("═════════════════════════════════════");
            Debug.WriteLine($"[DEBUG] UpdateInlinesFromContent 호출! Content = \"{Content}\"");
            Debug.WriteLine(Environment.StackTrace);
            Inlines.Clear();

            var font = new FontFamily(new Uri("pack://application:,,,/"), "./Resources/Fonts/#Pretendard Variable");

            if (string.IsNullOrEmpty(Content))
            {
                Debug.WriteLine("[DEBUG] Content is null or empty.");
                return;
            }

            // 1. Heading 처리 먼저
            var headingMatch = Regex.Match(Content, @"^(#{1,6})\s*(.*)");
            if (headingMatch.Success)
            {
                int level = headingMatch.Groups[1].Value.Length;
                string text = headingMatch.Groups[2].Value;

                var run = new Run(text)
                {
                    FontSize = 24 - (level - 1) * 2, // 헤딩 수준에 따른 크기 감소
                    FontWeight = FontWeights.Bold,
                    FontFamily = font
                };

                Inlines.Add(run);
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
                    Inlines.Add(new Run(plain) { FontFamily = font });
                }

                if (match.Value.StartsWith("**"))
                {
                    Inlines.Add(new Bold(new Run(match.Groups[2].Value)) { FontFamily = font });
                }
                else if (match.Value.StartsWith("*"))
                {
                    Inlines.Add(new Italic(new Run(match.Groups[3].Value)) { FontFamily = font });
                }
                else if (match.Value.StartsWith("__"))
                {
                    var underline = new Run(match.Groups[4].Value) { FontFamily = font };
                    underline.TextDecorations = TextDecorations.Underline;
                    Inlines.Add(underline);
                }
                else if (match.Value.StartsWith("~~"))
                {
                    var strike = new Run(match.Groups[5].Value) { FontFamily = font };
                    strike.TextDecorations = TextDecorations.Strikethrough;
                    Inlines.Add(strike);
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < Content.Length)
            {
                Inlines.Add(new Run(Content.Substring(lastIndex)) { FontFamily = font });
            }

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