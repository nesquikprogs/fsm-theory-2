using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MarkovChainAnalyzer
{
    // Состояния конечного автомата для генерации текста
    public enum GenerationState
    {
        Idle,           // Ожидание начала генерации
        SelectingWord,  // Выбор начального слова
        WordSelected,   // Начальное слово выбрано
        SelectingNextWord, // Выбор следующего слова
        NextWordSelected,  // Следующее слово выбрано
        Completed,      // Генерация завершена
        Error           // Произошла ошибка
    }

    // Статистика по отдельным словам
    public class WordStatItem
    {
        public string Word { get; set; }           // Слово
        public int Frequency { get; set; }        // Частота встречаемости
        public int NextWordsCount { get; set; }   // Количество возможных следующих слов
    }

    // Переход слова к следующему с вероятностью
    public class TransitionItem
    {
        public string Key { get; set; }           // Следующее слово
        public int Value { get; set; }           // Количество переходов
        public string Probability { get; set; }  // Вероятность перехода в процентах
    }

    // Статистика слов для анализа
    public class WordStatistics
    {
        public string Word { get; set; }
        public int Frequency { get; set; }
        public int NextWordsCount { get; set; }
    }

    // Информация о текущем выборе слова при генерации
    public class WordSelectionInfo
    {
        public string CurrentWord { get; set; }              // Слово, от которого выбираем следующее
        public string SelectedWord { get; set; }             // Выбранное слово
        public double Probability { get; set; }             // Вероятность выбора
        public List<TransitionItem> AvailableTransitions { get; set; } // Возможные переходы
    }

    // Конечный автомат для генерации текста
    public class TextGenerationFSM
    {
        private MarkovChainAnalyzer analyzer;  // Анализатор текста
        private Random random;                 // Генератор случайных чисел

        public GenerationState CurrentState { get; private set; } // Текущее состояние
        public List<string> GeneratedWords { get; private set; }  // Сгенерированные слова
        public string StartWord { get; private set; }             // Начальное слово
        public int TargetWordCount { get; private set; }          // Желаемое количество слов
        public string Result { get; private set; }                // Итоговый текст
        public string ErrorMessage { get; private set; }          // Сообщение об ошибке
        public WordSelectionInfo CurrentSelection { get; private set; } // Текущий выбор слова
        public bool IsAutoGeneration { get; private set; }        // Флаг автоматической генерации

        // События для интерфейса
        public event Action<GenerationState> StateChanged;
        public event Action<WordSelectionInfo> SelectionInfoChanged;

        public TextGenerationFSM(MarkovChainAnalyzer analyzer)
        {
            this.analyzer = analyzer;
            this.random = new Random();
            this.CurrentState = GenerationState.Idle;
            this.GeneratedWords = new List<string>();
            this.CurrentSelection = new WordSelectionInfo();
            this.IsAutoGeneration = false;
        }

        // Запуск генерации текста
        public void StartGeneration(string startWord, int wordCount, bool autoGeneration = false)
        {
            if (CurrentState != GenerationState.Idle && CurrentState != GenerationState.Completed && CurrentState != GenerationState.Error)
                throw new InvalidOperationException($"Невозможно начать генерацию из состояния {CurrentState}");

            Reset();  // Сбрасываем прошлую генерацию
            StartWord = startWord;
            TargetWordCount = wordCount;
            IsAutoGeneration = autoGeneration;
            TransitionToState(GenerationState.SelectingWord); // Переходим к выбору начального слова
        }

        // Основной шаг генерации (вызывается вручную или автоматически)
        public void ProcessNextStep()
        {
            switch (CurrentState)
            {
                case GenerationState.SelectingWord: ProcessSelectingWord(); break;
                case GenerationState.WordSelected: ProcessWordSelected(); break;
                case GenerationState.SelectingNextWord: ProcessSelectingNextWord(); break;
                case GenerationState.NextWordSelected: ProcessNextWordSelected(); break;
            }
        }

        // Выбор начального слова
        private void ProcessSelectingWord()
        {
            try
            {
                // Проверка существования слова
                if (!analyzer.WordExists(StartWord))
                {
                    ErrorMessage = $"Слово '{StartWord}' не найдено в проанализированном тексте";
                    TransitionToState(GenerationState.Error);
                    return;
                }

                // Обновляем информацию для интерфейса
                CurrentSelection = new WordSelectionInfo
                {
                    CurrentWord = "[Начало]",
                    SelectedWord = StartWord,
                    Probability = 1.0,
                    AvailableTransitions = new List<TransitionItem>()
                };
                SelectionInfoChanged?.Invoke(CurrentSelection);

                TransitionToState(GenerationState.WordSelected); // Переход к следующему шагу
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка при выборе начального слова: {ex.Message}";
                TransitionToState(GenerationState.Error);
            }
        }

        // Добавление начального слова в результат
        private void ProcessWordSelected()
        {
            try
            {
                GeneratedWords.Add(StartWord);

                // Если нужно только одно слово, завершаем
                if (GeneratedWords.Count >= TargetWordCount)
                    CompleteGeneration();
                else
                {
                    TransitionToState(GenerationState.SelectingNextWord); // Переход к выбору следующего слова

                    // Если авто-режим, сразу делаем следующий шаг
                    if (IsAutoGeneration)
                        ProcessNextStep();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка при добавлении начального слова: {ex.Message}";
                TransitionToState(GenerationState.Error);
            }
        }

        // Выбор следующего слова на основе вероятностей
        private void ProcessSelectingNextWord()
        {
            try
            {
                string currentWord = GeneratedWords.Last();
                var transitions = analyzer.GetTransitions(currentWord);
                var transitionsWithProb = analyzer.GetTopTransitionsWithProbabilities(currentWord, 10);

                if (transitions.Count == 0)
                {
                    // Если нет переходов, выбираем случайное слово
                    var allWords = analyzer.GetAllWords();
                    string randomWord = allWords[random.Next(allWords.Count)];

                    CurrentSelection = new WordSelectionInfo
                    {
                        CurrentWord = currentWord,
                        SelectedWord = randomWord,
                        Probability = 1.0 / allWords.Count,
                        AvailableTransitions = transitionsWithProb
                    };
                    SelectionInfoChanged?.Invoke(CurrentSelection);

                    TransitionToState(GenerationState.NextWordSelected);
                    return;
                }

                // Вероятностный выбор следующего слова
                int total = transitions.Values.Sum();
                int randomValue = random.Next(total);
                int cumulative = 0;

                foreach (var transition in transitions)
                {
                    cumulative += transition.Value;
                    if (randomValue < cumulative)
                    {
                        double probability = (double)transition.Value / total;

                        CurrentSelection = new WordSelectionInfo
                        {
                            CurrentWord = currentWord,
                            SelectedWord = transition.Key,
                            Probability = probability,
                            AvailableTransitions = transitionsWithProb
                        };
                        SelectionInfoChanged?.Invoke(CurrentSelection);
                        TransitionToState(GenerationState.NextWordSelected);
                        return;
                    }
                }

                // Если не выбрали случайно, берем первое слово
                var firstTransition = transitions.First();
                double firstProbability = (double)firstTransition.Value / total;

                CurrentSelection = new WordSelectionInfo
                {
                    CurrentWord = currentWord,
                    SelectedWord = firstTransition.Key,
                    Probability = firstProbability,
                    AvailableTransitions = transitionsWithProb
                };
                SelectionInfoChanged?.Invoke(CurrentSelection);
                TransitionToState(GenerationState.NextWordSelected);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка при выборе следующего слова: {ex.Message}";
                TransitionToState(GenerationState.Error);
            }
        }

        // Добавление выбранного слова в результат и проверка завершения
        private void ProcessNextWordSelected()
        {
            try
            {
                GeneratedWords.Add(CurrentSelection.SelectedWord);

                if (GeneratedWords.Count >= TargetWordCount)
                    CompleteGeneration();
                else
                {
                    TransitionToState(GenerationState.SelectingNextWord);
                    if (IsAutoGeneration)
                        ProcessNextStep();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка при добавлении слова: {ex.Message}";
                TransitionToState(GenerationState.Error);
            }
        }

        // Включение/выключение автоматической генерации
        public void ToggleAutoGeneration()
        {
            IsAutoGeneration = !IsAutoGeneration;
            if (IsAutoGeneration && (CurrentState == GenerationState.SelectingNextWord || CurrentState == GenerationState.NextWordSelected))
                ProcessNextStep();
        }

        // Установка режима автоматической генерации
        public void SetAutoGeneration(bool autoGeneration)
        {
            IsAutoGeneration = autoGeneration;
            if (IsAutoGeneration && (CurrentState == GenerationState.SelectingNextWord || CurrentState == GenerationState.NextWordSelected))
                ProcessNextStep();
        }

        // Завершение генерации
        private void CompleteGeneration()
        {
            Result = string.Join(" ", GeneratedWords);
            TransitionToState(GenerationState.Completed);
        }

        // Сброс автомата в исходное состояние
        public void Reset()
        {
            GeneratedWords.Clear();
            StartWord = null;
            TargetWordCount = 0;
            Result = null;
            ErrorMessage = null;
            CurrentSelection = new WordSelectionInfo();
            IsAutoGeneration = false;
            TransitionToState(GenerationState.Idle);
        }

        // Переход между состояниями
        private void TransitionToState(GenerationState newState)
        {
            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }

        // Получение текущего статуса для интерфейса
        public string GetStatusMessage()
        {
            string mode = IsAutoGeneration ? " [АВТО]" : " [РУЧНОЙ]";
            switch (CurrentState)
            {
                case GenerationState.Idle: return "Ожидание начала генерации" + mode;
                case GenerationState.SelectingWord: return "Выбор начального слова..." + mode;
                case GenerationState.WordSelected: return $"Начальное слово '{StartWord}' выбрано" + mode;
                case GenerationState.SelectingNextWord: return $"Выбор следующего слова для '{GeneratedWords.Last()}'..." + mode;
                case GenerationState.NextWordSelected: return $"Слово '{CurrentSelection.SelectedWord}' выбрано (вероятность: {CurrentSelection.Probability:P2})" + mode;
                case GenerationState.Completed: return $"Генерация завершена! Сгенерировано {GeneratedWords.Count} слов";
                case GenerationState.Error: return $"Ошибка: {ErrorMessage}";
                default: return "Неизвестное состояние";
            }
        }

        // Получение текущего сгенерированного текста
        public string GetCurrentGeneratedText() => string.Join(" ", GeneratedWords);
    }

    // Анализатор текста для цепей Маркова
    public class MarkovChainAnalyzer
    {
        private Dictionary<string, Dictionary<string, int>> transitions; // Переходы между словами
        private Dictionary<string, int> wordFrequencies;                 // Частота слов
        private Random random;

        public MarkovChainAnalyzer()
        {
            transitions = new Dictionary<string, Dictionary<string, int>>();
            wordFrequencies = new Dictionary<string, int>();
            random = new Random();
        }

        // Анализ текста: подсчет частот слов и переходов
        public void AnalyzeText(string text)
        {
            transitions.Clear();
            wordFrequencies.Clear();

            var words = Regex.Split(text.ToLower(), @"\s+")
                            .Where(word => !string.IsNullOrWhiteSpace(word))
                            .Select(word => Regex.Replace(word, @"[^\wа-яА-Я-]", "")) // Удаляем знаки, сохраняем дефисы
                            .Where(word => !string.IsNullOrEmpty(word))
                            .ToArray();

            if (words.Length < 2)
                throw new ArgumentException("Текст должен содержать как минимум 2 слова");

            // Подсчет частот слов и переходов
            for (int i = 0; i < words.Length - 1; i++)
            {
                string currentWord = words[i];
                string nextWord = words[i + 1];

                if (!wordFrequencies.ContainsKey(currentWord))
                    wordFrequencies[currentWord] = 0;
                wordFrequencies[currentWord]++;

                if (!transitions.ContainsKey(currentWord))
                    transitions[currentWord] = new Dictionary<string, int>();

                if (!transitions[currentWord].ContainsKey(nextWord))
                    transitions[currentWord][nextWord] = 0;

                transitions[currentWord][nextWord]++;
            }

            // Последнее слово
            string lastWord = words[words.Length - 1];
            if (!wordFrequencies.ContainsKey(lastWord))
                wordFrequencies[lastWord] = 0;
            wordFrequencies[lastWord]++;
        }

        // Получение статистики слов
        public List<WordStatistics> GetWordStatistics()
        {
            var stats = new List<WordStatistics>();
            foreach (var word in wordFrequencies.Keys)
            {
                int nextWordsCount = transitions.ContainsKey(word) ? transitions[word].Count : 0;
                stats.Add(new WordStatistics
                {
                    Word = word,
                    Frequency = wordFrequencies[word],
                    NextWordsCount = nextWordsCount
                });
            }
            return stats;
        }

        // Топ-переходы с вероятностями
        public List<TransitionItem> GetTopTransitionsWithProbabilities(string word, int topCount)
        {
            var result = new List<TransitionItem>();
            if (transitions.ContainsKey(word))
            {
                var wordTransitions = transitions[word];
                int totalTransitions = wordTransitions.Values.Sum();
                var topTransitions = wordTransitions.OrderByDescending(t => t.Value).Take(topCount);

                foreach (var transition in topTransitions)
                {
                    double probability = (double)transition.Value / totalTransitions;
                    result.Add(new TransitionItem
                    {
                        Key = transition.Key,
                        Value = transition.Value,
                        Probability = $"{probability:P2}" // В процентах
                    });
                }
            }
            return result;
        }

        public Dictionary<string, int> GetTransitions(string word) =>
            transitions.ContainsKey(word) ? transitions[word] : new Dictionary<string, int>();

        public bool WordExists(string word) => wordFrequencies.ContainsKey(word);

        public List<string> GetAllWords() => wordFrequencies.Keys.ToList();
    }
}
