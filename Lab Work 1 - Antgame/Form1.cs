using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AntGame
{
    // Enum: 3 состояния DFA муравья. Переходы по расстояниям (курсор, листик, норка).
    public enum AntState ///////******* состояния DFA муравья
    {
        Fleeing, // убегание от курсора
        Collecting, // сбор листика
        ReturningHome // возврат домой
    }

    public partial class Form1 : Form
    {
        private Ant ant; // DFA муравья
        private Leaf leaf; // цель для Collecting
        private Random random; // спавн листиков
        private Timer gameTimer; // цикл ~60 FPS
        private PointF homePosition; // норка

        public Form1()
        {
            InitializeComponent();
            InitializeGame();
        }

        // Инициализация: поле, DFA (старт Collecting), первый листик, таймер.
        private void InitializeGame()
        {
            DoubleBuffered = true;
            Width = 800;
            Height = 600;
            Text = "Муравей и листик";

            random = new Random();
            homePosition = new PointF(Width / 2, Height / 2);
            ant = new Ant(homePosition);
            ant.SetFieldSize(new Size(Width, Height));

            // создаем первый листик - начальный вход для Collecting
            SpawnLeaf();

            // Таймер для обновления игры
            gameTimer = new Timer();
            gameTimer.Interval = 16; // ~60 FPS
            gameTimer.Tick += GameTimer_Tick;
            gameTimer.Start();
        }

        // Тик: обновление DFA, спавн листика после цикла.
        private void GameTimer_Tick(object sender, EventArgs e)
        {
            ant.Update(PointToClient(Cursor.Position), leaf);

            //если муравей вернулся домой с листиком, создаем новый листик - после ReturningHome → Collecting
            if (ant.State == AntState.Collecting && leaf != null && leaf.IsCollected)
            {
                leaf = null;
                SpawnLeaf();
            }
            Invalidate();
        }

        // Отрисовка: норка, листик, муравей, счёт, состояние.
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;

            // нарисуем дом (синий квадрат) - цель ReturningHome
            g.FillRectangle(Brushes.Blue, homePosition.X - 15, homePosition.Y - 15, 30, 30);

            // нарисуем листик (зеленый квадрат) - цель Collecting, если не собран
            if (leaf != null && !leaf.IsCollected)
            {
                g.FillRectangle(Brushes.Green, leaf.Position.X - 5, leaf.Position.Y - 5, 10, 10);
            }

            // нарисуем муравья (красный квадрат) - текущая позиция DFA
            g.FillRectangle(Brushes.Red, ant.Position.X - 8, ant.Position.Y - 8, 16, 16);

            // муравей несет листик на спине - флаг для ReturningHome
            if (ant.IsCarryingLeaf)
            {
                g.FillRectangle(Brushes.Green, ant.Position.X - 4, ant.Position.Y - 15, 8, 8);
            }

            // Отображаем счет - выход DFA: завершённые циклы
            g.DrawString($"Собрано листиков: {ant.CollectedLeaves}",
                new Font("Arial", 10), Brushes.Black, 10, 10);

            // Отображаем состояние - для демонстрации текущего состояния DFA
            string stateText;
            switch (ant.State)
            {
                case AntState.Fleeing:
                    stateText = "Избегание курсора";
                    break;
                case AntState.Collecting:
                    stateText = "Сбор";
                    break;
                case AntState.ReturningHome:
                    stateText = "Возвращение домой";
                    break;
                default:
                    stateText = "Неизвестно";
                    break;
            }
            g.DrawString($"Состояние: {stateText}",
                new Font("Arial", 10), Brushes.Black, 10, 30);
        }

        // Спавн: случайная позиция листика.
        private void SpawnLeaf()
        {
            leaf = new Leaf(new PointF(
                random.Next(50, Width - 50),
                random.Next(50, Height - 50)
            ));
        }
    }

    // Ant: DFA муравья. Update - таблица переходов (switch).
    public class Ant
    {
        public PointF Position { get; set; }
        public PointF Target { get; set; }
        public float Speed { get; set; }
        public AntState State { get; set; }
        public int CollectedLeaves { get; set; }
        public float FearDistance { get; set; }
        public bool IsCarryingLeaf { get; set; }
        public PointF HomePosition { get; set; }

        private Random random;

        public Size FieldSize { get; set; }

        // Конструктор: старт в Collecting.
        public Ant(PointF homePosition)
        {
            Position = homePosition;
            HomePosition = homePosition;
            Speed = 2f;
            State = AntState.Collecting; // игра начинается сразу со сбора - начальное состояние DFA
            FearDistance = 100f;
            IsCarryingLeaf = false;
            random = new Random();
            FieldSize = new Size(800, 600);
        }

        // Update: обработка по состоянию.
        public void Update(Point cursorPosition, Leaf leaf)
        {
            switch (State)
            {
                case AntState.Fleeing:
                    HandleFleeingState(cursorPosition);
                    break;
                case AntState.Collecting:
                    HandleCollectingState(cursorPosition, leaf);
                    break;
                case AntState.ReturningHome:
                    HandleReturningHomeState(cursorPosition);
                    break;
            }
        }

        public void SetFieldSize(Size size)
        {
            FieldSize = size;
        }

        // CheckBounds: границы поля (margin=20px).
        private void CheckBounds()
        {
            float margin = 20f;
            bool hitBoundary = false;

            if (Position.X < margin)
            {
                Position = new PointF(margin, Position.Y);
                hitBoundary = true;
            }
            if (Position.X > FieldSize.Width - margin)
            {
                Position = new PointF(FieldSize.Width - margin, Position.Y);
                hitBoundary = true;
            }
            if (Position.Y < margin)
            {
                Position = new PointF(Position.X, margin);
                hitBoundary = true;
            }
            if (Position.Y > FieldSize.Height - margin)
            {
                Position = new PointF(Position.X, FieldSize.Height - margin);
                hitBoundary = true;
            }
        }

        // Fleeing: движение от курсора (ускоренное).
        // Переход: Fleeing → Collecting если dist(курсор) > 150f.
        private void HandleFleeingState(Point cursorPosition) ///////****** курсор в Fleeing
        {
            // Бежим от курсора: вектор прочь + нормализация
            Vector2 fleeDirection = new Vector2(
                Position.X - cursorPosition.X,
                Position.Y - cursorPosition.Y
            );
            fleeDirection = Normalize(fleeDirection);

            Position = new PointF(
                Position.X + fleeDirection.X * Speed * 1.5f,
                Position.Y + fleeDirection.Y * Speed * 1.5f
            );

            CheckBounds();

            // Переход зависит от: dist(курсор) > FearDistance + 50f
            if (Distance(Position, cursorPosition) > FearDistance + 50f)
            {
                State = AntState.Collecting;
            }
        }

        // Collecting: к листику.
        // Переходы: Collecting → Fleeing если dist(курсор) < 100f;
        // Collecting → ReturningHome если dist(листик) < 10f.
        private void HandleCollectingState(Point cursorPosition, Leaf leaf)
        {
            //не слишком ли близко курсор - приоритетная проверка угрозы
            float distanceToCursor = Distance(Position, cursorPosition);
            // Переход зависит от: dist(курсор) < FearDistance
            if (distanceToCursor < FearDistance)
            {
                State = AntState.Fleeing;
                return;
            }

            //если есть листик, идем к нему
            if (leaf != null && !leaf.IsCollected)
            {
                Target = leaf.Position;
                MoveTowardsTarget();

                //проверяем, собрали ли листик - переход Collecting → ReturningHome
                // Зависит от: расстояния до листика (<10f)
                if (Distance(Position, leaf.Position) < 10f)
                {
                    leaf.IsCollected = true;
                    IsCarryingLeaf = true;
                    State = AntState.ReturningHome;
                    Target = HomePosition;
                }
            }
        }

        // ReturningHome: к норке (игнор курсора).
        // Переход: ReturningHome → Collecting если dist(норка) < 10f.
        private void HandleReturningHomeState(Point cursorPosition)
        {

            // идкм домой
            MoveTowardsTarget();

            //дошли ли до дома? - переход ReturningHome → Collecting
            // Зависит от: расстояния до норки (<10f)
            if (Distance(Position, HomePosition) < 10f)
            {
                IsCarryingLeaf = false;
                CollectedLeaves++;
                State = AntState.Collecting; //возвращаемся к сбору
            }
        }

        // MoveTowardsTarget: движение к цели.
        private void MoveTowardsTarget() //////////  идем к цели (листик или норка)
        {
            Vector2 direction = new Vector2(Target.X - Position.X, Target.Y - Position.Y);
            if (direction.Length() > 1f)
            {
                direction = Normalize(direction);
                Position = new PointF(
                    Position.X + direction.X * Speed,
                    Position.Y + direction.Y * Speed
                );
            }
            CheckBounds();
        }

        // Distance: евклидово расстояние - основа переходов.
        private float Distance(PointF a, PointF b)
        {
            return (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        // Normalize: единичный вектор для направления.
        private Vector2 Normalize(Vector2 vector)
        {
            float length = vector.Length();
            if (length > 0)
            {
                return new Vector2(vector.X / length, vector.Y / length);
            }
            return vector;
        }
    }

    // Leaf: цель для сбора.
    public class Leaf
    {
        public PointF Position { get; set; }
        public bool IsCollected { get; set; }

        public Leaf(PointF position)
        {
            Position = position;
            IsCollected = false;
        }
    }

    // Vector2: векторная математика.
    public struct Vector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y);
        }
    }
}