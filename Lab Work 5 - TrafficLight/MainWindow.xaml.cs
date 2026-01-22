using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace TrafficLightFSM
{
    public partial class MainWindow : Window
    {
        // Экземпляр класса иерархического конечного автомата светофора
        private TrafficLightFSM trafficLight;

        public MainWindow()
        {
            InitializeComponent();
            trafficLight = new TrafficLightFSM(UpdateUI);
            UpdateUI();
            UpdateToggleButtonText(); // ← НОВОЕ: инициализация текста кнопки
        }

        // Метод обновляет внешний вид элементов интерфейса в зависимости от состояния автомата.
        private void UpdateUI()
        {
            RedLight.Fill = trafficLight.IsRedOn ? Brushes.Red : Brushes.DarkRed;
            YellowLight.Fill = trafficLight.IsYellowOn ? Brushes.Yellow : Brushes.DarkGoldenrod;
            GreenLight.Fill = trafficLight.IsGreenOn ? Brushes.LimeGreen : Brushes.DarkGreen;
            StateInfo.Text = $"Состояние: {trafficLight.CurrentState}";
            SubStateInfo.Text = $"Подсостояние: {trafficLight.CurrentSubState}";
            TimerInfo.Text = $"Таймер: {trafficLight.CurrentTimer} сек";
            ProgressTimer.Maximum = trafficLight.MaxTimerValue;
            ProgressTimer.Value = trafficLight.CurrentTimer;
        }

        // Нажатие кнопки "Старт" — запуск работы автомата
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            trafficLight.Start();
        }

        // Нажатие кнопки "Стоп" — остановка автомата и выключение сигналов
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            trafficLight.Stop();
        }

        // Нажатие кнопки "Аварийный режим" — переход к мигающему желтому
        private void EmergencyButton_Click(object sender, RoutedEventArgs e)
        {
            trafficLight.TriggerEmergency();
        }

        // ← НОВАЯ КНОПКА: переключение режима мигания после красного
        private void ToggleFlashingMode_Click(object sender, RoutedEventArgs e)
        {
            trafficLight.ToggleFlashingAfterRedMode();
            UpdateToggleButtonText();
        }

        // ← НОВОЕ: обновляет текст кнопки
        private void UpdateToggleButtonText()
        {
            ToggleFlashingButton.Content = trafficLight.IsFlashingAfterRedEnabled
                ? "Режим: Мигание после красного (вкл)"
                : "Режим: Мигание после красного (выкл)";
        }
    }

    // Класс иерархического конечного автомата светофора.
    // Содержит основные состояния (верхний уровень) и подсостояния (нижний уровень).
    public class TrafficLightFSM
    {
        // Верхний уровень иерархии
        public enum MainState
        {
            Off, // Светофор выключен
            Operating, // Рабочий режим (нормальная последовательность)
            Emergency // Аварийный режим (мигающий желтый)
        }

        // Нижний уровень
        public enum OperatingSubState
        {
            Red, // Горит красный
            RedYellow, // Красный и желтый одновременно
            Green, // Горит зеленый
            Yellow, // Горит желтый (перед красным)
            FlashingYellowAfterRed // Мигающий желтый после красного !!!!!!!!!!
        }

        // Время (в секундах) для каждого сигнала
        private readonly int red = 3;
        private readonly int red_yellow = 1;
        private readonly int green = 3;
        private readonly int yellow = 2;

        private bool useFlashingAfterRed = true; // true = после красного — мигающий жёлтый!!!!!!!!!

        public bool IsFlashingAfterRedEnabled => useFlashingAfterRed; // !!!!!!!!!!!!!!

        // Текущие значения состояний и таймеров
        private MainState currentMainState;
        private OperatingSubState currentOperatingSubState;
        private int currentTimer; // оставшееся время в текущем состоянии

        // Таймеры WPF для работы в реальном времени
        private DispatcherTimer timer; // основной таймер для переключения сигналов
        private DispatcherTimer blinkTimer; // отдельный таймер для мигания в аварийном режиме
        private Action updateCallback;
        private bool isYellowBlinking;

        // Свойства для доступа к информации о текущем состоянии
        public string CurrentState => currentMainState.ToString(); // текущее состояние верхнего уровня
        public string CurrentSubState => currentMainState == MainState.Operating ? currentOperatingSubState.ToString() : "-"; // подсостояние
        public int CurrentTimer => currentTimer; // оставшееся время
        public int MaxTimerValue { get; private set; } // максимальное время в данном состоянии

        // Определяем, какой сигнал должен быть включен
        public bool IsRedOn => currentMainState == MainState.Operating &&
                              (currentOperatingSubState == OperatingSubState.Red ||
                               currentOperatingSubState == OperatingSubState.RedYellow);

        public bool IsYellowOn => (currentMainState == MainState.Operating &&
                                  (currentOperatingSubState == OperatingSubState.Yellow ||
                                   currentOperatingSubState == OperatingSubState.RedYellow ||
                                   (currentOperatingSubState == OperatingSubState.FlashingYellowAfterRed && isYellowBlinking))) ||
                                 (currentMainState == MainState.Emergency && isYellowBlinking);

        public bool IsGreenOn => currentMainState == MainState.Operating &&
                                currentOperatingSubState == OperatingSubState.Green;

        // Конструктор автомата: инициализирует состояния и создает таймеры.
        public TrafficLightFSM(Action updateCallback)
        {
            this.updateCallback = updateCallback;
            // Изначально светофор выключен
            currentMainState = MainState.Off;
            currentOperatingSubState = OperatingSubState.Red;
            currentTimer = 0;
            isYellowBlinking = false;

            // Основной таймер для нормальной работы светофора (1 секунда)
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            // Таймер для мигания желтого в аварийном режиме (0.5 секунды)
            blinkTimer = new DispatcherTimer();
            blinkTimer.Interval = TimeSpan.FromSeconds(0.5);
            blinkTimer.Tick += BlinkTimer_Tick;
        }

        // Уменьшает таймер и вызывает переход при достижении нуля.
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (currentMainState == MainState.Operating)
            {
                currentTimer--;

                // Если включён режим "мигание после красного"
                if (useFlashingAfterRed && currentOperatingSubState == OperatingSubState.Red && currentTimer <= 0)
                {
                    currentOperatingSubState = OperatingSubState.FlashingYellowAfterRed;
                    currentTimer = 0;
                    MaxTimerValue = 0;
                    blinkTimer.Start();
                    isYellowBlinking = true;
                    timer.Stop();
                    updateCallback?.Invoke();
                    return;
                }

                if (currentTimer <= 0)
                {
                    TransitionToNextState();
                }
            }
            // Обновляем интерфейс
            updateCallback?.Invoke();
        }

        // Срабатывает каждые полсекунды в аварийном режиме.
        // Переключает мигание желтого света.
        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            if (currentMainState == MainState.Emergency ||
                (currentMainState == MainState.Operating && currentOperatingSubState == OperatingSubState.FlashingYellowAfterRed))
            {
                isYellowBlinking = !isYellowBlinking;
                updateCallback?.Invoke();
            }
        }

        // Определяет переходы между подсостояниями в обычном цикле работы.
        private void TransitionToNextState()
        {
            switch (currentOperatingSubState)
            {
                case OperatingSubState.Red:
                    currentOperatingSubState = OperatingSubState.RedYellow;
                    currentTimer = red_yellow;
                    MaxTimerValue = red_yellow;
                    break;
                case OperatingSubState.RedYellow:
                    currentOperatingSubState = OperatingSubState.Green;
                    currentTimer = green;
                    MaxTimerValue = green;
                    break;
                case OperatingSubState.Green:
                    currentOperatingSubState = OperatingSubState.Yellow;
                    currentTimer = yellow;
                    MaxTimerValue = yellow;
                    break;
                case OperatingSubState.Yellow:
                    currentOperatingSubState = OperatingSubState.Red;
                    currentTimer = red;
                    MaxTimerValue = red;
                    break;
            }
        }

        // Запускает работу автомата: включается режим "Operating" и начинается цикл сигналов.
        public void Start()
        {
            if (currentMainState == MainState.Off || currentMainState == MainState.Emergency)
            {
                blinkTimer.Stop();
                isYellowBlinking = false;
                currentMainState = MainState.Operating;
                currentOperatingSubState = OperatingSubState.Red;
                currentTimer = red;
                MaxTimerValue = red;
                timer.Start();
                updateCallback?.Invoke();
            }
        }

        // Останавливает автомат и выключает все сигналы.
        public void Stop()
        {
            timer.Stop();
            blinkTimer.Stop();
            currentMainState = MainState.Off;
            currentTimer = 0;
            isYellowBlinking = false;
            updateCallback?.Invoke();
        }

        // Переключает светофор в аварийный режим: мигает желтым.
        public void TriggerEmergency()
        {
            timer.Stop();
            currentMainState = MainState.Emergency;
            currentTimer = 0;
            MaxTimerValue = 0;
            blinkTimer.Start();
            isYellowBlinking = true;
            updateCallback?.Invoke();
        }

        // Переключение режима
        public void ToggleFlashingAfterRedMode()
        {
            useFlashingAfterRed = !useFlashingAfterRed;

            // Если выключаем режим, а сейчас в мигании — выходим в обычный цикл
            if (!useFlashingAfterRed && currentOperatingSubState == OperatingSubState.FlashingYellowAfterRed)
            {
                blinkTimer.Stop();
                isYellowBlinking = false;
                currentOperatingSubState = OperatingSubState.RedYellow;
                currentTimer = red_yellow;
                MaxTimerValue = red_yellow;
                timer.Start();
            }

            updateCallback?.Invoke();
        }
    }
}