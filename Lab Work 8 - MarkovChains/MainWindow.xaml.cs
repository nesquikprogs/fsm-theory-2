using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace MarkovChainAnalyzer
{
    // Основное окно приложения
    public partial class MainWindow : Window
    {
        private MarkovChainAnalyzer analyzer;               // Анализатор цепей Маркова
        private TextGenerationFSM generationFSM;            // Конечный автомат генерации текста
        private List<WordStatItem> allWordStats;           // Список статистики всех слов
        private List<WordStatItem> allWordsForGeneration; // Слова для выбора начального слова
        private CollectionViewSource wordStatsViewSource;  // Источник данных для DataGrid статистики слов
        private CollectionViewSource generationWordsViewSource; // Источник данных для ComboBox начальных слов
        private DispatcherTimer autoGenerationTimer;       // Таймер для автоматической генерации текста

        public MainWindow()
        {
            InitializeComponent();

            // Инициализация объектов
            analyzer = new MarkovChainAnalyzer();
            generationFSM = new TextGenerationFSM(analyzer);
            wordStatsViewSource = new CollectionViewSource();
            generationWordsViewSource = new CollectionViewSource();

            InitializeAutoGenerationTimer();
            SubscribeToFSMEvents();

            InitializeTextFields(); // Инициализация текстовых полей
            UpdateControlStates();  // Обновление состояния элементов управления
        }

        // Инициализация текстовых блоков
        private void InitializeTextFields()
        {
            CurrentStateTextBlock.Text = "Idle - Ожидание";
            SelectionInfoTextBlock.Text = "Нет данных";
            GeneratedTextBox.Text = "";
            ProgressTextBlock.Text = "0/0";
        }

        // Инициализация таймера для автоматической генерации
        private void InitializeAutoGenerationTimer()
        {
            autoGenerationTimer = new DispatcherTimer();
            autoGenerationTimer.Interval = TimeSpan.FromMilliseconds(500); // интервал 500 мс
            autoGenerationTimer.Tick += AutoGenerationTimer_Tick;
        }

        // Подписка на события конечного автомата
        private void SubscribeToFSMEvents()
        {
            generationFSM.StateChanged += OnGenerationStateChanged;
            generationFSM.SelectionInfoChanged += OnSelectionInfoChanged;
        }

        // Обработка изменения состояния генератора
        private void OnGenerationStateChanged(GenerationState newState)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateCurrentState(newState);
                UpdateGenerationStatus();
                UpdateProgress();
                UpdateControlStates();

                // Останавливаем таймер при завершении или ошибке
                if (newState == GenerationState.Completed || newState == GenerationState.Error)
                {
                    autoGenerationTimer.Stop();
                }
            });
        }

        // Обновление информации о выбранном слове
        private void OnSelectionInfoChanged(WordSelectionInfo selectionInfo)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateSelectionInfo(selectionInfo);
                UpdateAvailableTransitions(selectionInfo);
            });
        }

        // Событие таймера для авто-генерации
        private void AutoGenerationTimer_Tick(object sender, EventArgs e)
        {
            if (generationFSM.IsAutoGeneration &&
                generationFSM.CurrentState != GenerationState.Completed &&
                generationFSM.CurrentState != GenerationState.Error)
            {
                generationFSM.ProcessNextStep();
            }
            else
            {
                autoGenerationTimer.Stop();
            }
        }

        // Кнопка "Анализ текста"
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = InputTextBox.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show("Пожалуйста, введите текст для анализа.");
                    return;
                }

                analyzer.AnalyzeText(text); // Анализ текста
                UpdateResults();           // Обновление UI после анализа
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе текста: {ex.Message}");
            }
        }

        // Кнопка "Загрузить файл"
        private void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                    Title = "Выберите текстовый файл"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string fileContent = File.ReadAllText(openFileDialog.FileName, Encoding.UTF8);
                    InputTextBox.Text = fileContent;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}");
            }
        }

        // Обновление всех результатов после анализа
        private void UpdateResults()
        {
            DisplayWordStatistics();   // Обновляем статистику слов
            PopulateStartWordComboBox(); // Заполняем ComboBox начальных слов
            TransitionsGrid.ItemsSource = null;

            generationFSM.Reset();     // Сбрасываем генератор
            UpdateCurrentState(generationFSM.CurrentState);
            UpdateGenerationStatus();
        }

        // Отображение статистики слов
        private void DisplayWordStatistics()
        {
            allWordStats = analyzer.GetWordStatistics()
                .OrderByDescending(s => s.Frequency)
                .Select(s => new WordStatItem
                {
                    Word = s.Word,
                    Frequency = s.Frequency,
                    NextWordsCount = s.NextWordsCount
                })
                .ToList();

            wordStatsViewSource.Source = allWordStats;
            WordStatsGrid.ItemsSource = wordStatsViewSource.View;
        }

        // Заполнение ComboBox начальных слов
        private void PopulateStartWordComboBox()
        {
            allWordsForGeneration = analyzer.GetWordStatistics()
                .OrderBy(s => s.Word)
                .Select(s => new WordStatItem
                {
                    Word = s.Word,
                    Frequency = s.Frequency,
                    NextWordsCount = s.NextWordsCount
                })
                .ToList();

            generationWordsViewSource.Source = allWordsForGeneration;
            StartWordComboBox.ItemsSource = generationWordsViewSource.View;

            if (allWordsForGeneration.Count > 0)
                StartWordComboBox.SelectedIndex = 0;
        }

        // Обработка выбора слова в таблице статистики
        private void WordStatsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WordStatsGrid.SelectedItem is WordStatItem selectedWord)
                DisplayWordTransitions(selectedWord.Word);
        }

        // Отображение переходов для выбранного слова
        private void DisplayWordTransitions(string word)
        {
            var transitions = analyzer.GetTopTransitionsWithProbabilities(word, 20);
            TransitionsGrid.ItemsSource = transitions;
        }

        // Фильтр статистики слов по поиску
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyWordStatsFilter();
        }

        private void ApplyWordStatsFilter()
        {
            if (wordStatsViewSource.View != null)
            {
                string searchText = SearchTextBox.Text.ToLower();
                wordStatsViewSource.View.Filter = string.IsNullOrWhiteSpace(searchText)
                    ? null
                    : new Predicate<object>(item =>
                    {
                        var wordStat = item as WordStatItem;
                        return wordStat != null && wordStat.Word.ToLower().Contains(searchText);
                    });
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
        }

        // Фильтр слов для генерации
        private void GenerationSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyGenerationWordsFilter();
        }

        private void ApplyGenerationWordsFilter()
        {
            if (generationWordsViewSource.View != null)
            {
                string searchText = GenerationSearchTextBox.Text.ToLower();
                generationWordsViewSource.View.Filter = string.IsNullOrWhiteSpace(searchText)
                    ? null
                    : new Predicate<object>(item =>
                    {
                        var wordStat = item as WordStatItem;
                        return wordStat != null && wordStat.Word.ToLower().Contains(searchText);
                    });

                if (generationWordsViewSource.View.Cast<object>().Any())
                    StartWordComboBox.SelectedIndex = 0;
            }
        }

        private void ClearGenerationSearchButton_Click(object sender, RoutedEventArgs e)
        {
            GenerationSearchTextBox.Text = "";
        }

        // Кнопка "Сделать шаг генерации"
        private void GenerateStepButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Если генерация еще не начата
                if (generationFSM.CurrentState == GenerationState.Idle ||
                    generationFSM.CurrentState == GenerationState.Completed ||
                    generationFSM.CurrentState == GenerationState.Error)
                {
                    if (StartWordComboBox.SelectedItem is WordStatItem selectedWord)
                    {
                        if (int.TryParse(WordCountTextBox.Text, out int wordCount) && wordCount > 0)
                        {
                            generationFSM.StartGeneration(selectedWord.Word, wordCount, false);
                        }
                        else
                        {
                            MessageBox.Show("Пожалуйста, введите корректное количество слов.");
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Пожалуйста, выберите начальное слово.");
                        return;
                    }
                }

                generationFSM.ProcessNextStep(); // Выполняем шаг генерации
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выполнении шага генерации: {ex.Message}");
            }
        }

        // Кнопка "Начать авто-генерацию"
        private void StartAutoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (generationFSM.CurrentState == GenerationState.Idle ||
                    generationFSM.CurrentState == GenerationState.Completed ||
                    generationFSM.CurrentState == GenerationState.Error)
                {
                    if (StartWordComboBox.SelectedItem is WordStatItem selectedWord)
                    {
                        if (int.TryParse(WordCountTextBox.Text, out int wordCount) && wordCount > 0)
                        {
                            generationFSM.StartGeneration(selectedWord.Word, wordCount, true);
                        }
                        else
                        {
                            MessageBox.Show("Пожалуйста, введите корректное количество слов.");
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Пожалуйста, выберите начальное слово.");
                        return;
                    }
                }
                else
                {
                    generationFSM.SetAutoGeneration(true);
                }

                autoGenerationTimer.Start(); // Запуск таймера
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске автоматической генерации: {ex.Message}");
            }
        }

        // Кнопка "Остановить авто-генерацию"
        private void StopAutoButton_Click(object sender, RoutedEventArgs e)
        {
            generationFSM.SetAutoGeneration(false);
            autoGenerationTimer.Stop();
            UpdateControlStates();
        }

        // Кнопка "Сброс"
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            generationFSM.Reset();
            autoGenerationTimer.Stop();
            UpdateCurrentState(generationFSM.CurrentState);
            UpdateGenerationStatus();
            UpdateControlStates();
        }

        // Обновление текстового блока состояния генератора
        private void UpdateCurrentState(GenerationState state)
        {
            string stateText = GetStateDisplayName(state);
            string modeText = generationFSM.IsAutoGeneration ? " [АВТО]" : " [РУЧНОЙ]";
            CurrentStateTextBlock.Text = stateText + modeText;
        }

        // Читаемое имя состояния для UI
        private string GetStateDisplayName(GenerationState state)
        {
            switch (state)
            {
                case GenerationState.Idle: return "Idle - Ожидание";
                case GenerationState.SelectingWord: return "SelectingWord - Выбор начального слова";
                case GenerationState.WordSelected: return "WordSelected - Начальное слово выбрано";
                case GenerationState.SelectingNextWord: return "SelectingNextWord - Выбор следующего слова";
                case GenerationState.NextWordSelected: return "NextWordSelected - Следующее слово выбрано";
                case GenerationState.Completed: return "Completed - Завершено";
                case GenerationState.Error: return "Error - Ошибка";
                default: return "Unknown - Неизвестно";
            }
        }

        // Обновление поля с сгенерированным текстом
        private void UpdateGenerationStatus()
        {
            GeneratedTextBox.Text = generationFSM.GetCurrentGeneratedText();
        }

        // Обновление информации о выбранном слове
        private void UpdateSelectionInfo(WordSelectionInfo selectionInfo)
        {
            if (selectionInfo != null && !string.IsNullOrEmpty(selectionInfo.SelectedWord))
            {
                string info = $"Текущее слово: {selectionInfo.CurrentWord}\n";
                info += $"Выбранное слово: {selectionInfo.SelectedWord}\n";
                info += $"Вероятность выбора: {selectionInfo.Probability:P2}";
                SelectionInfoTextBlock.Text = info;
            }
            else
            {
                SelectionInfoTextBlock.Text = "Нет данных";
            }
        }

        // Обновление таблицы возможных переходов
        private void UpdateAvailableTransitions(WordSelectionInfo selectionInfo)
        {
            AvailableTransitionsGrid.ItemsSource = selectionInfo?.AvailableTransitions;
        }

        // Обновление прогресса генерации
        private void UpdateProgress()
        {
            int currentCount = generationFSM.GeneratedWords.Count;
            int targetCount = generationFSM.TargetWordCount;

            if (targetCount > 0)
            {
                double progress = (double)currentCount / targetCount * 100;
                GenerationProgressBar.Value = progress;
                ProgressTextBlock.Text = $"{currentCount}/{targetCount}";
            }
            else
            {
                GenerationProgressBar.Value = 0;
                ProgressTextBlock.Text = "0/0";
            }
        }

        // Включение/отключение кнопок в зависимости от состояния генерации
        private void UpdateControlStates()
        {
            var state = generationFSM.CurrentState;
            bool isAuto = generationFSM.IsAutoGeneration;

            GenerateStepButton.IsEnabled = !isAuto && state != GenerationState.Completed && state != GenerationState.Error;
            StartAutoButton.IsEnabled = !isAuto && state != GenerationState.Completed && state != GenerationState.Error;
            StopAutoButton.IsEnabled = isAuto && state != GenerationState.Completed && state != GenerationState.Error;
            ResetButton.IsEnabled = state != GenerationState.Idle;

            StartWordComboBox.IsEnabled = !isAuto && (state == GenerationState.Idle || state == GenerationState.Completed || state == GenerationState.Error);
            WordCountTextBox.IsEnabled = !isAuto && (state == GenerationState.Idle || state == GenerationState.Completed || state == GenerationState.Error);
            GenerationSearchTextBox.IsEnabled = !isAuto && (state == GenerationState.Idle || state == GenerationState.Completed || state == GenerationState.Error);
            ClearGenerationSearchButton.IsEnabled = !isAuto && (state == GenerationState.Idle || state == GenerationState.Completed || state == GenerationState.Error);
        }

        // Очистка таймера при закрытии окна
        protected override void OnClosed(EventArgs e)
        {
            autoGenerationTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
