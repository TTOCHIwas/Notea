using Notea.Modules.Subject.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using static Notea.Modules.Subject.ViewModels.MarkdownLineViewModel;

namespace Notea.Modules.Subject.ViewModels
{
    public class NoteEditorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MarkdownLineViewModel> Lines { get; set; }
        private int _nextDisplayOrder = 1;
        public int SubjectId { get; set; } = 1;
        public int CurrentCategoryId { get; set; } = 1;

        public NoteEditorViewModel()
        {
            Lines = new ObservableCollection<MarkdownLineViewModel>
            {
                new MarkdownLineViewModel
                {
                    IsEditing = true,  // 처음에 편집 가능하도록
                    SubjectId = this.SubjectId,
                    CategoryId = this.CurrentCategoryId,
                    Content = ""  // 빈 내용으로 시작
                }
            };

            // PropertyChanged 이벤트 등록
            Lines[0].PropertyChanged += OnLinePropertyChanged;

            Lines.CollectionChanged += (s, e) =>
            {
                if (Lines.Count == 0)
                {
                    var newLine = new MarkdownLineViewModel
                    {
                        IsEditing = true,
                        SubjectId = this.SubjectId,
                        CategoryId = this.CurrentCategoryId,
                        Content = ""
                    };
                    newLine.PropertyChanged += OnLinePropertyChanged;
                    Lines.Add(newLine);
                }
            };
        }

        public NoteEditorViewModel(List<NoteCategory> loadedNotes)
        {
            Lines = new ObservableCollection<MarkdownLineViewModel>();
            int currentDisplayOrder = 1;

            if (loadedNotes != null && loadedNotes.Count > 0)
            {
                foreach (var category in loadedNotes)
                {
                    // 카테고리 ID 업데이트
                    CurrentCategoryId = category.CategoryId;

                    // 카테고리 제목 추가
                    var categoryLine = new MarkdownLineViewModel
                    {
                        Content = category.Title,
                        IsEditing = false,
                        SubjectId = this.SubjectId,
                        CategoryId = category.CategoryId,
                        TextId = 0,
                        IsHeadingLine = true,
                        DisplayOrder = currentDisplayOrder++
                    };

                    Lines.Add(categoryLine);
                    RegisterLineEvents(categoryLine); // 이벤트 등록 메서드로 분리

                    // 각 라인 추가
                    foreach (var line in category.Lines)
                    {
                        var contentLine = new MarkdownLineViewModel
                        {
                            Content = line.Content,
                            IsEditing = false,
                            SubjectId = this.SubjectId,
                            CategoryId = category.CategoryId,
                            TextId = line.Index,
                            Index = Lines.Count,
                            DisplayOrder = currentDisplayOrder++
                        };

                        Lines.Add(contentLine);
                        RegisterLineEvents(contentLine); // 이벤트 등록 메서드로 분리
                    }
                }

                _nextDisplayOrder = currentDisplayOrder;
            }
            else
            {
                // 빈 라인 추가
                var emptyLine = new MarkdownLineViewModel
                {
                    IsEditing = true,
                    SubjectId = this.SubjectId,
                    CategoryId = this.CurrentCategoryId,
                    Content = "",
                    DisplayOrder = 1
                };
                Lines.Add(emptyLine);
                RegisterLineEvents(emptyLine);
            }

            // CollectionChanged 이벤트 한 번만 등록
            Lines.CollectionChanged += Lines_CollectionChanged;
        }

        private void RegisterLineEvents(MarkdownLineViewModel line)
        {
            line.PropertyChanged += OnLinePropertyChanged;
            line.CategoryCreated += OnCategoryCreated;
            line.RequestFindPreviousCategory += OnRequestFindPreviousCategory;
        }

        private void UnregisterLineEvents(MarkdownLineViewModel line)
        {
            line.PropertyChanged -= OnLinePropertyChanged;
            line.CategoryCreated -= OnCategoryCreated;
            line.RequestFindPreviousCategory -= OnRequestFindPreviousCategory;
            line.Dispose(); // Timer dispose
        }

        private void Lines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (Lines.Count == 0)
            {
                var newLine = new MarkdownLineViewModel
                {
                    IsEditing = true,
                    SubjectId = this.SubjectId,
                    CategoryId = this.CurrentCategoryId,
                    Content = "",
                    DisplayOrder = 1
                };
                Lines.Add(newLine);
                RegisterLineEvents(newLine);
            }

            // 라인이 제거된 경우
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (MarkdownLineViewModel removedLine in e.OldItems)
                {
                    if (removedLine.TextId > 0)
                    {
                        NoteRepository.DeleteLine(removedLine.TextId);
                        Debug.WriteLine($"[DEBUG] 라인 삭제됨. TextId: {removedLine.TextId}");
                    }
                    UnregisterLineEvents(removedLine);
                }
            }

            // 인덱스 재정렬
            UpdateLineIndices();
        }



        /// <summary>
        /// 새로운 라인 추가
        /// </summary>
        public void AddNewLine()
        {
            int categoryIdForNewLine = GetCurrentCategoryIdForNewLine();
            int displayOrder = Lines.Count > 0 ? Lines.Last().DisplayOrder + 1 : 1;

            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = this.SubjectId,
                CategoryId = categoryIdForNewLine > 0 ? categoryIdForNewLine : 1, // 기본값 보장
                Index = Lines.Count,
                DisplayOrder = displayOrder,
                TextId = 0
            };

            Lines.Add(newLine);

            // 이벤트 등록
            newLine.PropertyChanged += OnLinePropertyChanged;
            newLine.CategoryCreated += OnCategoryCreated;
            newLine.RequestFindPreviousCategory += OnRequestFindPreviousCategory;

            Debug.WriteLine($"[DEBUG] 새 라인 추가됨. CategoryId: {newLine.CategoryId}, DisplayOrder: {displayOrder}");
        }

        private int GetCurrentCategoryIdForNewLine()
        {
            // 마지막 라인부터 역순으로 가장 최근의 카테고리 찾기
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    Debug.WriteLine($"[DEBUG] 가장 최근 카테고리 찾음: {Lines[i].CategoryId} (라인 {i})");
                    return Lines[i].CategoryId;
                }
            }

            // 카테고리가 없으면 CurrentCategoryId 사용
            Debug.WriteLine($"[DEBUG] 카테고리를 찾지 못함. CurrentCategoryId 사용: {CurrentCategoryId}");
            return CurrentCategoryId > 0 ? CurrentCategoryId : 1;
        }

        private void OnLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is MarkdownLineViewModel line)
            {
                if (e.PropertyName == nameof(MarkdownLineViewModel.Content))
                {
                    // 일반 텍스트가 제목으로 변경되는 경우
                    if (NoteRepository.IsCategoryHeading(line.Content) && !line.IsHeadingLine)
                    {
                        line.IsHeadingLine = true;
                        line.SaveImmediately();
                        UpdateSubsequentLinesCategoryId(Lines.IndexOf(line) + 1, line.CategoryId);
                    }
                }
                else if (e.PropertyName == nameof(MarkdownLineViewModel.CategoryId))
                {
                    if (line.IsHeadingLine && line.CategoryId > 0)
                    {
                        UpdateCurrentCategory(line);
                    }
                }
            }
        }

        private void OnCategoryCreated(object sender, CategoryCreatedEventArgs e)
        {
            if (sender is MarkdownLineViewModel line)
            {
                CurrentCategoryId = e.NewCategoryId;
                Debug.WriteLine($"[DEBUG] 새 카테고리 생성됨. CurrentCategoryId 업데이트: {CurrentCategoryId}");

                // 이 제목 이후의 모든 라인들의 CategoryId 업데이트
                int headingIndex = Lines.IndexOf(line);
                UpdateSubsequentLinesCategoryId(headingIndex + 1, CurrentCategoryId);
            }
        }

        private void UpdateSubsequentLinesCategoryId(int startIndex, int categoryId)
        {
            for (int i = startIndex; i < Lines.Count; i++)
            {
                if (!Lines[i].IsHeadingLine)
                {
                    if (Lines[i].CategoryId != categoryId)
                    {
                        Lines[i].CategoryId = categoryId;
                        Debug.WriteLine($"[DEBUG] 라인 {i}의 CategoryId 업데이트: {categoryId}");
                    }
                }
                else
                {
                    break; // 다음 제목을 만나면 중단
                }
            }
        }


        private void OnRequestFindPreviousCategory(object sender, FindPreviousCategoryEventArgs e)
        {
            var currentLine = e.CurrentLine;
            int currentIndex = Lines.IndexOf(currentLine);

            // 현재 라인 이전에서 가장 가까운 카테고리 찾기
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    e.PreviousCategoryId = Lines[i].CategoryId;
                    return;
                }
            }

            // 이전 카테고리가 없으면 기본값
            e.PreviousCategoryId = 1;
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
                    // 하위 텍스트들을 이전 카테고리로 재할당
                    int previousCategoryId = GetPreviousCategoryId(Lines.IndexOf(line));
                    if (previousCategoryId > 0)
                    {
                        NoteRepository.ReassignTextsToCategory(line.CategoryId, previousCategoryId);
                    }

                    // 카테고리만 삭제 (텍스트는 재할당됨)
                    NoteRepository.DeleteCategory(line.CategoryId, false);

                    // 현재 카테고리가 삭제되는 경우
                    if (CurrentCategoryId == line.CategoryId)
                    {
                        CurrentCategoryId = previousCategoryId > 0 ? previousCategoryId : 1;
                    }
                }

                Lines.Remove(line);
                UnregisterLineEvents(line); // 이벤트 해제
            }
        }

        private int GetPreviousCategoryId(int currentIndex)
        {
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    return Lines[i].CategoryId;
                }
            }
            return 1; // 기본 카테고리
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

            // 삽입 위치에서의 CategoryId와 DisplayOrder 결정
            int categoryId = CurrentCategoryId;
            int displayOrder = 1;

            // 삽입 위치 이전의 가장 가까운 제목에서 CategoryId 가져오기
            for (int i = index - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    categoryId = Lines[i].CategoryId;
                    break;
                }
            }

            // DisplayOrder 계산
            if (index > 0 && index < Lines.Count)
            {
                // 중간 삽입: 이전과 다음 라인의 DisplayOrder 사이값
                int prevOrder = Lines[index - 1].DisplayOrder;
                int nextOrder = Lines[index].DisplayOrder;

                // 사이에 공간이 있으면 중간값 사용
                if (nextOrder - prevOrder > 1)
                {
                    displayOrder = prevOrder + 1;
                }
                else
                {
                    // 공간이 없으면 이후 모든 라인들의 DisplayOrder를 밀어냄
                    displayOrder = nextOrder;
                    ShiftDisplayOrdersFrom(displayOrder);
                }
            }
            else if (index > 0)
            {
                // 마지막에 추가
                displayOrder = Lines[index - 1].DisplayOrder + 1;
            }

            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = this.SubjectId,
                CategoryId = categoryId,
                Index = index,
                DisplayOrder = displayOrder,
                TextId = 0
            };

            // UI에 즉시 반영
            Lines.Insert(index, newLine);

            // 이후 라인들의 Index 업데이트
            for (int i = index + 1; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
            }

            // 이벤트 등록
            newLine.PropertyChanged += OnLinePropertyChanged;
            newLine.CategoryCreated += OnCategoryCreated;
            newLine.RequestFindPreviousCategory += OnRequestFindPreviousCategory;

            Debug.WriteLine($"[DEBUG] 새 라인 삽입됨. 위치: {index}, CategoryId: {categoryId}, DisplayOrder: {displayOrder}");
        }

        public void InsertNewLineAfter(MarkdownLineViewModel afterLine)
        {
            int insertIndex = Lines.IndexOf(afterLine) + 1;
            int insertDisplayOrder = afterLine.DisplayOrder;

            // 이후 라인들의 displayOrder 증가
            ShiftDisplayOrdersFrom(insertDisplayOrder + 1);

            // 새 라인 생성
            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = this.SubjectId,
                CategoryId = afterLine.CategoryId,
                Index = insertIndex,
                DisplayOrder = insertDisplayOrder + 1,
                TextId = 0
            };

            Lines.Insert(insertIndex, newLine);
            newLine.PropertyChanged += OnLinePropertyChanged;
            newLine.CategoryCreated += OnCategoryCreated;

            Debug.WriteLine($"[DEBUG] 새 라인 삽입. Index: {insertIndex}, DisplayOrder: {newLine.DisplayOrder}");
        }

        private void ShiftDisplayOrdersFrom(int fromOrder)
        {
            // 메모리에서 먼저 업데이트
            var linesToShift = Lines.Where(l => l.DisplayOrder >= fromOrder).ToList();
            foreach (var line in linesToShift)
            {
                line.DisplayOrder++;
                Debug.WriteLine($"[DEBUG] 라인 시프트: Content='{line.Content}', NewOrder={line.DisplayOrder}");
            }

            // DB에서도 업데이트
            NoteRepository.ShiftDisplayOrdersAfter(SubjectId, fromOrder - 1);
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}