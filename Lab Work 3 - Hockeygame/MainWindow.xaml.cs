using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace HockeyGame
{
    public partial class MainWindow : Window
    {
        private PlayState playState; //Нынешнее игровое состояние
        private DispatcherTimer gameTimer; //Таймер идущий на протяжение игры(собственно для обеспечения ее продолжения на неопределённое время)

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeGame();
        }

        private void InitializeGame()
        {
            //Создаем игровое состояние с полем для игроков
            playState = new PlayState(GameCanvas);
            playState.OnScoreChanged += UpdateScore;
            playState.OnStateChanged += UpdateState;

            //Игровой цикл: 60 FPS (16 мс на кадр)
            gameTimer = new DispatcherTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(10);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void GameLoop(object sender, EventArgs e)
        {
            //Основной игровой цикл
            playState.Update();
            playState.Render();
        }

        private void UpdateScore(int leftScore, int rightScore)
        {
            ScoreText.Text = $"Счет: {leftScore} - {rightScore}";
        }

        private void UpdateState(string state)
        {
            StateText.Text = $"Состояние: {state}";
        }

        //Обновляем позицию мыши для управления игроком
        private void GameCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(GameCanvas);
            playState.UpdateMousePosition(position);
        }

        //Обработка клика мыши(удар по шайбе)
        private void GameCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(GameCanvas);
            playState.HandleMouseClick(position);
        }

        //Обработка клавиш управления
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.G)
            {
                playState.RandomizePositions();
            }
            else if (e.Key == Key.R)
            {
                playState.ResetGame();
            }
        }
    }
}