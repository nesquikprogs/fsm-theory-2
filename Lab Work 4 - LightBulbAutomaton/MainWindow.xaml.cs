using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace LightBulbAutomaton
{
    public partial class MainWindow : Window
    {
        private enum BulbState
        {
            Off,        // Состояние: лампочка выключена
            On,         // Состояние: лампочка включена
            BurnedOut,  // Состояние: лампочка перегорела
        }

        private BulbState currentState = BulbState.Off;  // Начальное состояние автомата
        private DispatcherTimer burnOutTimer;           // Таймер для отслеживания времени работы
        private int secondsElapsed = 0;                 // Счётчик секунд
        private const int burnout_time = 5;             // Время до перегорания в секундах

        public MainWindow()
        {
            InitializeComponent();  // Инициализация компонентов интерфейса
            InitializeTimer();      // Настройка таймера
            UpdateUI();             // Обновление интерфейса при запуске
        }

        private void InitializeTimer()
        {
            burnOutTimer = new DispatcherTimer();           // Создание таймера
            burnOutTimer.Interval = TimeSpan.FromSeconds(1); // Интервал в 1 секунду
            burnOutTimer.Tick += BurnOutTimer_Tick;         // Обработчик события тика
        }

        private void BurnOutTimer_Tick(object sender, EventArgs e)
        {
            secondsElapsed++;                           // Увеличение счётчика времени
            TimerText.Text = $"Таймер: {secondsElapsed} сек"; // Обновление текста таймера

            if (secondsElapsed >= burnout_time)         // Проверка на перегорание
            {
                BurnOut();                              // Переход в состояние перегорания
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Обработка нажатия кнопки "Переключить" в зависимости от текущего состояния
            switch (currentState)
            {
                case BulbState.Off:
                    TurnOn();   // Переход из выключенного в включённое состояние
                    break;
                case BulbState.On:
                    TurnOff();  // Переход из включённого в выключённое состояние
                    break;
                case BulbState.BurnedOut:
                    break;      // Никаких действий при перегоревшей лампочке
            }
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            // Обработка нажатия кнопки "Заменить лампочку" для перегоревшей или взорвавшейся
            if (currentState == BulbState.BurnedOut)
            {
                ReplaceBulb();  // Переход в выключенное состояние
            }
        }

        private void TurnOn()
        {
            currentState = BulbState.On;     // Установка состояния "включена"
            secondsElapsed = 0;             // Сброс счётчика времени
            burnOutTimer.Start();           // Запуск таймера
            UpdateUI();                     // Обновление интерфейса
        }

        private void TurnOff()
        {
            currentState = BulbState.Off;   // Установка состояния "выключена"
            burnOutTimer.Stop();            // Остановка таймера
            secondsElapsed = 0;             // Сброс счётчика времени
            UpdateUI();                     // Обновление интерфейса
        }

        private void BurnOut()
        {
            currentState = BulbState.BurnedOut;  // Установка состояния "перегорела"
            burnOutTimer.Stop();                // Остановка таймера
            UpdateUI();                         // Обновление интерфейса
        }

        private void ReplaceBulb()
        {
            currentState = BulbState.Off;   // Сброс в состояние "выключена"
            secondsElapsed = 0;             // Сброс счётчика времени
            UpdateUI();                     // Обновление интерфейса
        }

        private void UpdateUI()
        {
            // Обновление интерфейса в зависимости от текущего состояния автомата
            switch (currentState)
            {
                case BulbState.Off:
                    LightBulb.Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)); // Серый цвет
                    StatusText.Text = "Состояние: Выключена";
                    TimerText.Text = "Таймер: 0 сек";
                    ToggleButton.IsEnabled = true;   // Активировать кнопку переключения
                    ReplaceButton.IsEnabled = false; // Деактивировать кнопку замены
                    break;

                case BulbState.On:
                    LightBulb.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0)); // Жёлтый цвет
                    StatusText.Text = "Состояние: Включена";
                    ToggleButton.IsEnabled = true;
                    ReplaceButton.IsEnabled = false;
                    break;

                case BulbState.BurnedOut:
                    LightBulb.Fill = new SolidColorBrush(Color.FromRgb(139, 0, 0)); // Тёмно-красный цвет
                    StatusText.Text = "Состояние: Перегорела";
                    TimerText.Text = "Таймер: -";
                    ToggleButton.IsEnabled = false;  // Деактивировать переключение
                    ReplaceButton.IsEnabled = true;  // Активировать замену
                    break;

            }
        }
    }
}