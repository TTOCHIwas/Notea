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
                new MarkdownLineViewModel
                {
                    IsEditing = false,
                    SubjectId = this.SubjectId,
                    CategoryId = this.CurrentCategoryId
                }
            };

            Lines.CollectionChanged += (s, e) =>
            {
                if (Lines.Count == 0)
                {
                    Lines.Add(new MarkdownLineViewModel
                    {
                        SubjectId = this.SubjectId,
                        CategoryId = this.CurrentCategoryId
                    });
                }
            };
        }

        public NoteEditorViewModel(List<NoteCategory> loadedNotes)
        {
            Lines = new ObservableCollection<MarkdownLineViewModel>();

            foreach (var category in loadedNotes)
            {
                // 카테고리 제목 추가
                Lines.Add(new MarkdownLineViewModel
                {
                    Content = category.Title,
                    IsEditing = false,
                    SubjectId = this.SubjectId,
                    CategoryId = category.CategoryId,
                    TextId = 0 // 카테고리 제목은 별도 테이블이므로 0으로 설정
                });

                // 각 라인 추가
                foreach (var line in category.Lines)
                {
                    Lines.Add(new MarkdownLineViewModel
                    {
                        Content = line.Content,
                        IsEditing = false,
                        SubjectId = this.SubjectId,
                        CategoryId = category.CategoryId,
                        TextId = line.Index, // NoteLine의 Index가 실제로는 TextId
                        Index = Lines.Count
                    });
                }
            }

            Debug.WriteLine($"[DEBUG] 초기화된 라인 수: {Lines.Count}");

            Lines.CollectionChanged += (s, e) =>
            {
                if (Lines.Count == 0)
                {
                    Lines.Add(new MarkdownLineViewModel
                    {
                        SubjectId = this.SubjectId,
                        CategoryId = this.CurrentCategoryId
                    });
                }

                // 라인이 제거된 경우 데이터베이스에서도 삭제
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                {
                    foreach (MarkdownLineViewModel removedLine in e.OldItems)
                    {
                        if (removedLine.TextId > 0)
                        {
                            NoteRepository.DeleteLine(removedLine.TextId);
                            Debug.WriteLine($"[DEBUG] 라인 삭제됨. TextId: {removedLine.TextId}");
                        }
                    }
                }

                // 인덱스 재정렬
                UpdateLineIndices();
            };
        }

        /// <summary>
        /// 새로운 라인 추가
        /// </summary>
        public void AddNewLine()
        {
            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = this.SubjectId,
                CategoryId = this.CurrentCategoryId, // 현재 활성 카테고리 사용
                Index = Lines.Count,
                TextId = 0
            };

            Lines.Add(newLine);

            // PropertyChanged 이벤트 등록
            newLine.PropertyChanged += OnEditorLinePropertyChanged;

            Debug.WriteLine($"[DEBUG] 새 라인 추가됨. 현재 CategoryId: {CurrentCategoryId}");
        }

        private void OnLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is MarkdownLineViewModel line && e.PropertyName == nameof(MarkdownLineViewModel.IsHeadingLine))
            {
                if (line.IsHeadingLine)
                {
                    // 새로운 제목이 생성됨 - 이후 라인들의 CategoryId 업데이트
                    UpdateCurrentCategory(line);
                }
            }
        }

        private void UpdateCurrentCategory(MarkdownLineViewModel headingLine)
        {
            // 제목 라인이 저장된 후 CategoryId가 설정되면 현재 카테고리로 설정
            if (headingLine.CategoryId > 0)
            {
                CurrentCategoryId = headingLine.CategoryId;
                Debug.WriteLine($"[DEBUG] 현재 카테고리 변경됨: {CurrentCategoryId}");

                // 이 제목 이후의 모든 라인들의 CategoryId 업데이트
                int headingIndex = Lines.IndexOf(headingLine);
                for (int i = headingIndex + 1; i < Lines.Count; i++)
                {
                    if (!Lines[i].IsHeadingLine) // 다음 제목이 나올 때까지
                    {
                        Lines[i].CategoryId = CurrentCategoryId;
                    }
                    else
                    {
                        break; // 다음 제목을 만나면 중단
                    }
                }
            }
        }

        public void RemoveLine(MarkdownLineViewModel line)
        {
            if (Lines.Contains(line))
            {
                if (line.IsHeadingLine && line.CategoryId > 0)
                {
                    // 제목 라인 삭제 시 카테고리와 관련 모든 콘텐츠 삭제
                    NoteRepository.DeleteCategory(line.CategoryId);

                    // 이 카테고리에 속한 모든 라인들을 UI에서도 제거
                    var linesToRemove = Lines.Where(l => l.CategoryId == line.CategoryId && l != line).ToList();
                    foreach (var lineToRemove in linesToRemove)
                    {
                        Lines.Remove(lineToRemove);
                    }

                    // 현재 카테고리가 삭제되는 경우, 이전 카테고리로 변경
                    if (CurrentCategoryId == line.CategoryId)
                    {
                        UpdateCurrentCategoryAfterDeletion();
                    }
                }

                Lines.Remove(line);
                line.PropertyChanged -= OnEditorLinePropertyChanged; // 이벤트 해제
            }
        }

        private void UpdateCurrentCategoryAfterDeletion()
        {
            // 가장 마지막 제목의 CategoryId를 현재 카테고리로 설정
            var lastHeading = Lines.LastOrDefault(l => l.IsHeadingLine && l.CategoryId > 0);
            if (lastHeading != null)
            {
                CurrentCategoryId = lastHeading.CategoryId;
            }
            else
            {
                // 제목이 없으면 기본 카테고리 사용
                CurrentCategoryId = 1;
            }

            Debug.WriteLine($"[DEBUG] 삭제 후 현재 카테고리: {CurrentCategoryId}");
        }



        public void InsertNewLineAt(int index)
        {
            if (index < 0 || index > Lines.Count)
                index = Lines.Count;

            // 삽입 위치 이전의 가장 최근 제목 찾기
            int categoryId = CurrentCategoryId;
            for (int i = index - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    categoryId = Lines[i].CategoryId;
                    break;
                }
            }

            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = this.SubjectId,
                CategoryId = categoryId,
                Index = index,
                TextId = 0
            };

            Lines.Insert(index, newLine);
            newLine.PropertyChanged += OnEditorLinePropertyChanged;

            Debug.WriteLine($"[DEBUG] 새 라인 삽입됨. 위치: {index}, CategoryId: {categoryId}");
        }

        /// <summary>
        /// 모든 라인의 인덱스를 재정렬
        /// </summary>
        private void UpdateLineIndices()
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
            }
        }

        public void UpdateAllCategoryIds()
        {
            int currentCategoryId = 1; // 기본 카테고리

            foreach (var line in Lines)
            {
                if (line.IsHeadingLine && line.CategoryId > 0)
                {
                    currentCategoryId = line.CategoryId;
                }
                else if (!line.IsHeadingLine)
                {
                    line.CategoryId = currentCategoryId;
                }
            }

            CurrentCategoryId = currentCategoryId;
            Debug.WriteLine($"[DEBUG] 모든 라인의 CategoryId 업데이트 완료. 현재 카테고리: {CurrentCategoryId}");
        }

        /// <summary>
        /// 모든 변경사항을 데이터베이스에 일괄 저장
        /// </summary>
        public void SaveAllChanges()
        {
            try
            {
                var linesToSave = Lines.Where(l => l.TextId > 0).ToList();
                if (linesToSave.Any())
                {
                    NoteRepository.SaveLinesInTransaction(linesToSave);
                    Debug.WriteLine($"[DEBUG] {linesToSave.Count}개 라인 일괄 저장 완료");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] 일괄 저장 실패: {ex.Message}");
            }
        }

        private void OnEditorLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is MarkdownLineViewModel line)
            {
                if (e.PropertyName == nameof(MarkdownLineViewModel.CategoryId))
                {
                    // CategoryId가 변경된 경우 (새로운 제목이 저장됨)
                    if (line.IsHeadingLine && line.CategoryId > 0)
                    {
                        UpdateCurrentCategory(line);
                    }
                }
                else if (e.PropertyName == nameof(MarkdownLineViewModel.IsHeadingLine))
                {
                    // 제목 상태가 변경된 경우
                    if (line.IsHeadingLine)
                    {
                        Debug.WriteLine($"[DEBUG] 라인이 제목으로 변경됨: {line.Content}");
                    }
                    else
                    {
                        Debug.WriteLine($"[DEBUG] 라인이 일반 텍스트로 변경됨: {line.Content}");
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}