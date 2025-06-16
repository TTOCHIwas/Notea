using Notea.Helpers;
using Notea.Modules.Subject.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using static Notea.Modules.Subject.ViewModels.MarkdownLineViewModel;

namespace Notea.Modules.Subject.ViewModels
{
    public class NoteEditorViewModel : INotifyPropertyChanged
    {

        private readonly UndoRedoManager<NoteState> _undoRedoManager = new();
        public ObservableCollection<MarkdownLineViewModel> Lines { get; set; }
        private int _nextDisplayOrder = 1;
        public int SubjectId { get; set; } = 1;
        public int CurrentCategoryId { get; set; } = 1;

        private Stack<(int categoryId, int level)> _categoryStack = new();


        private DispatcherTimer _idleTimer;
        private DateTime _lastActivityTime;
        private const int IDLE_TIMEOUT_SECONDS = 5; // 5초간 입력이 없으면 저장

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

            InitializeIdleTimer();

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
            InitializeIdleTimer();
            int currentDisplayOrder = 1;

            if (loadedNotes != null && loadedNotes.Count > 0)
            {
                // 재귀적으로 카테고리와 라인 추가
                foreach (var category in loadedNotes)
                {
                    currentDisplayOrder = AddCategoryWithHierarchy(category, currentDisplayOrder);
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
                emptyLine.SetOriginalContent("");
                Lines.Add(emptyLine);
                RegisterLineEvents(emptyLine);
            }

            Lines.CollectionChanged += Lines_CollectionChanged;
        }

        /// <summary>
        /// 카테고리와 하위 구조를 재귀적으로 추가
        /// </summary>
        private int AddCategoryWithHierarchy(NoteCategory category, int displayOrder)
        {
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
                Level = category.Level,
                DisplayOrder = displayOrder++
            };

            categoryLine.SetOriginalContent(category.Title);
            Lines.Add(categoryLine);
            RegisterLineEvents(categoryLine);

            // 카테고리의 라인들 추가
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
                    DisplayOrder = displayOrder++
                };

                contentLine.SetOriginalContent(line.Content);
                Lines.Add(contentLine);
                RegisterLineEvents(contentLine);
            }

            // 하위 카테고리들 재귀적으로 추가
            foreach (var subCategory in category.SubCategories)
            {
                displayOrder = AddCategoryWithHierarchy(subCategory, displayOrder);
            }

            return displayOrder;
        }

        private void InitializeIdleTimer()
        {
            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _idleTimer.Tick += CheckIdleAndSave;
            _idleTimer.Start();
            _lastActivityTime = DateTime.Now;
        }

        public void UpdateActivity()
        {
            Debug.WriteLine("마지막 액티비티 시간 바뀜");
            _lastActivityTime = DateTime.Now;
        }

        private void CheckIdleAndSave(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastActivityTime).TotalSeconds >= IDLE_TIMEOUT_SECONDS)
            {
                Debug.WriteLine($"[IDLE] {IDLE_TIMEOUT_SECONDS}초간 유휴 상태 감지. 자동 저장 시작.");
                DebugPrintCurrentState();
                UpdateActivity();
                SaveAllChanges();
            }
        }

        public class NoteState
        {
            public List<LineState> Lines { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class LineState
        {
            public string Content { get; set; }
            public int CategoryId { get; set; }
            public int TextId { get; set; }
            public bool IsHeadingLine { get; set; }
        }

        // 현재 상태 저장
        private void SaveCurrentState()
        {
            var state = new NoteState
            {
                Timestamp = DateTime.Now,
                Lines = Lines.Select(l => new LineState
                {
                    Content = l.Content,
                    CategoryId = l.CategoryId,
                    TextId = l.TextId,
                    IsHeadingLine = l.IsHeadingLine
                }).ToList()
            };

            _undoRedoManager.AddState(state);
        }

        // Ctrl+Z 처리
        public void Undo()
        {
            var previousState = _undoRedoManager.Undo();
            if (previousState != null)
            {
                RestoreState(previousState);
            }
        }

        // Ctrl+Y 처리
        public void Redo()
        {
            var nextState = _undoRedoManager.Redo();
            if (nextState != null)
            {
                RestoreState(nextState);
            }
        }

        private void RestoreState(NoteState state)
        {
            // 상태 복원 로직
            Lines.Clear();
            foreach (var lineState in state.Lines)
            {
                var line = new MarkdownLineViewModel
                {
                    Content = lineState.Content,
                    CategoryId = lineState.CategoryId,
                    TextId = lineState.TextId,
                    IsHeadingLine = lineState.IsHeadingLine,
                    SubjectId = this.SubjectId
                };
                Lines.Add(line);
                RegisterLineEvents(line);
            }
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

            Debug.WriteLine($"[ADD LINE] 새 라인 추가 시작. CategoryId: {categoryIdForNewLine}, DisplayOrder: {displayOrder}");

            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = this.SubjectId,
                CategoryId = categoryIdForNewLine > 0 ? categoryIdForNewLine : 1,
                Index = Lines.Count,
                DisplayOrder = displayOrder,
                TextId = 0
            };

            Lines.Add(newLine);
            RegisterLineEvents(newLine);

            Debug.WriteLine($"[ADD LINE] 새 라인 추가 완료. Index: {newLine.Index}");
        }

        private int GetCurrentCategoryIdForNewLine()
        {
            // 마지막 라인부터 역순으로 가장 최근의 카테고리 찾기
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    Debug.WriteLine($"[DEBUG] 가장 최근 카테고리 찾음: {Lines[i].CategoryId} (라인 {i}, 레벨 {Lines[i].Level})");
                    return Lines[i].CategoryId;
                }
            }

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

        // 변경된 라인만 저장
        public void SaveAllChanges()
        {
            try
            {
                var changedLines = Lines.Where(l => l.HasChanges).ToList();
                if (!changedLines.Any())
                {
                    Debug.WriteLine("[SAVE] 변경사항 없음");
                    return;
                }

                Debug.WriteLine($"[SAVE] {changedLines.Count}개 라인 저장 시작");

                using var transaction = NoteRepository.BeginTransaction();
                try
                {
                    foreach (var line in changedLines)
                    {
                        Debug.WriteLine($"[SAVE] 라인 처리 중 - Content: {line.Content}, IsHeading: {line.IsHeadingLine}, CategoryId: {line.CategoryId}, TextId: {line.TextId}");

                        if (line.IsHeadingLine)
                        {
                            // 부모 카테고리 찾기
                            int? parentId = FindParentForHeading(line);
                            Debug.WriteLine($"[SAVE] 헤딩 부모 카테고리: {parentId}");

                            if (line.CategoryId <= 0)
                            {
                                // 새 카테고리 생성 - transaction 전달
                                int newCategoryId = NoteRepository.InsertCategory(
                                    line.Content,
                                    line.SubjectId,
                                    line.DisplayOrder,
                                    line.Level,
                                    parentId,
                                    transaction); // 트랜잭션 전달
                                line.CategoryId = newCategoryId;
                                Debug.WriteLine($"[SAVE] 새 카테고리 생성됨: {newCategoryId}");
                            }
                            else
                            {
                                // 기존 카테고리 업데이트 - transaction 전달
                                NoteRepository.UpdateCategory(line.CategoryId, line.Content, transaction);
                                Debug.WriteLine($"[SAVE] 카테고리 업데이트됨: {line.CategoryId}");
                            }
                        }
                        else
                        {
                            // 일반 텍스트
                            Debug.WriteLine($"[SAVE] 일반 텍스트 저장 - CategoryId: {line.CategoryId}");

                            if (line.CategoryId <= 0)
                            {
                                Debug.WriteLine($"[SAVE ERROR] CategoryId가 유효하지 않음: {line.CategoryId}");
                                line.CategoryId = GetCurrentCategoryIdForNewLine();
                                Debug.WriteLine($"[SAVE] CategoryId 재설정: {line.CategoryId}");
                            }

                            if (line.TextId <= 0)
                            {
                                // 새 텍스트 생성 - transaction 전달
                                int newTextId = NoteRepository.InsertNewLine(
                                    line.Content,
                                    line.SubjectId,
                                    line.CategoryId,
                                    line.DisplayOrder,
                                    transaction); // 트랜잭션 전달

                                if (newTextId > 0)
                                {
                                    line.TextId = newTextId;
                                    Debug.WriteLine($"[SAVE] 새 텍스트 생성됨: {newTextId}");
                                }
                                else
                                {
                                    Debug.WriteLine($"[SAVE ERROR] 텍스트 삽입 실패");
                                }
                            }
                            else
                            {
                                // 기존 텍스트 업데이트
                                NoteRepository.UpdateLine(line);
                                Debug.WriteLine($"[SAVE] 텍스트 업데이트됨: {line.TextId}");
                            }
                        }

                        // 저장 완료 후 변경사항 리셋
                        line.ResetChanges();
                    }

                    transaction.Commit();
                    Debug.WriteLine($"[SAVE] 트랜잭션 커밋 완료");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SAVE ERROR] 트랜잭션 실패, 롤백: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SAVE ERROR] 저장 실패: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 헤딩의 부모 카테고리 찾기
        /// </summary>
        private int? FindParentForHeading(MarkdownLineViewModel heading)
        {

            Debug.WriteLine($"[SAVE] 부모 찾는다 기다려라");

            int headingIndex = Lines.IndexOf(heading);

            for (int i = headingIndex - 1; i >= 0; i--)
            {
                Debug.WriteLine($"[SAVE] 부모 찾는 중이다 기다려라");

                var line = Lines[i];
                if (line.IsHeadingLine && line.Level < heading.Level && line.CategoryId > 0)
                {
                    Debug.WriteLine($"[SAVE] 부모 찾았다 임마 기다려라");

                    return line.CategoryId;
                }
            }

            Debug.WriteLine($"[SAVE] 부모 몬 찾았다 어어?");


            return null;
        }

        private void DebugPrintCurrentState()
        {
            Debug.WriteLine("=== 현재 에디터 상태 ===");
            Debug.WriteLine($"SubjectId: {SubjectId}");
            Debug.WriteLine($"CurrentCategoryId: {CurrentCategoryId}");
            Debug.WriteLine($"Lines 개수: {Lines.Count}");

            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                Debug.WriteLine($"[{i}] Content: '{line.Content}', " +
                               $"CategoryId: {line.CategoryId}, " +
                               $"TextId: {line.TextId}, " +
                               $"IsHeading: {line.IsHeadingLine}, " +
                               $"Level: {line.Level}, " +
                               $"HasChanges: {line.HasChanges}");
            }
            Debug.WriteLine("===================");
        }


        // View가 닫힐 때 호출
        public void OnViewClosing()
        {
            _idleTimer?.Stop();
            SaveAllChanges();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}