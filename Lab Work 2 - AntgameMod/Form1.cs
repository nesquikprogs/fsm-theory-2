using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AntGame
{
    // Enum: 3 состояния PDA муравья. Переходы с использованием стека (магазин для памяти).
    public enum AntState
    {
        Fleeing, // убегание от курсора
        Collecting, // сбор листика
        ReturningHome // возврат домой
    }

    public partial class Form1 : Form
    {
        private Ant ant; // PDA муравья
        private Leaf leaf; // цель для Collecting
        private Random random; // спавн листиков
        private Timer gameTimer; // цикл ~50 FPS
        private PointF homePosition; // норка

        public Form1()
        {
            InitializeComponent();
            InitializeGame();
        }

        // Инициализация: поле, PDA (старт Collecting с push в стек), первый листик, таймер.
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

            // Создаем первый листик - начальный вход для Collecting
            SpawnLeaf();

            // Таймер для обновления игры
            gameTimer = new Timer();
            gameTimer.Interval = 20;
            gameTimer.Tick += GameTimer_Tick;
            gameTimer.Start();
        }

        // Тик: обновление PDA, спавн листика после цикла.
        private void GameTimer_Tick(object sender, EventArgs e)
        {
            ant.Update(PointToClient(Cursor.Position), leaf);

            // Если муравей вернулся домой с листиком (pop в ReturningHome), создаем новый листик
            if (ant.CurrentState == AntState.Collecting && leaf != null && leaf.IsCollected)
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

            // Рисуем дом (синий квадрат) - цель ReturningHome
            g.FillRectangle(Brushes.Blue, homePosition.X - 15, homePosition.Y - 15, 30, 30);

            // Рисуем листик (зеленый квадрат) - цель Collecting, если не собран
            if (leaf != null && !leaf.IsCollected)
            {
                g.FillRectangle(Brushes.Green, leaf.Position.X - 5, leaf.Position.Y - 5, 10, 10);
            }

            // Рисуем муравья (красный квадрат) - текущая позиция PDA
            g.FillRectangle(Brushes.Red, ant.Position.X - 8, ant.Position.Y - 8, 16, 16);

            // Если муравей несет листик, рисуем его над муравьем - флаг для ReturningHome
            if (ant.IsCarryingLeaf)
            {
                g.FillRectangle(Brushes.Green, ant.Position.X - 4, ant.Position.Y - 15, 8, 8);
            }

            // Отображаем счет - выход PDA: завершённые циклы
            g.DrawString($"Собрано листиков: {ant.CollectedLeaves}",
                new Font("Arial", 12), Brushes.Black, 10, 10);

            // Отображаем состояние - для демонстрации текущего состояния PDA
            string stateText;
            switch (ant.CurrentState)
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
                new Font("Arial", 12), Brushes.Black, 10, 30);
        }

        // Спавн: случайная позиция листика.
        private void SpawnLeaf()
        {
            leaf = new Leaf(new PointF(
                random.Next(50, Width - 50),
                random.Next(50, Height - 50)
            ));
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }

    // Ant: PDA муравья. Update - обработка с глобальным push/pop по курсору + логика состояний.
    public class Ant
    {
        public PointF Position { get; set; }
        public PointF Target { get; set; }
        public float Speed { get; set; }
        public AntState CurrentState { get; private set; }
        public int CollectedLeaves { get; set; }
        public float FearDistance { get; set; }
        public bool IsCarryingLeaf { get; set; }
        public PointF HomePosition { get; set; }

        private Stack<AntState> stateStack; /////////******************* стек как магазин PDA
        private Random random;
        public Size FieldSize { get; set; }

        // Конструктор: старт с push Collecting в стек.
        public Ant(PointF homePosition)
        {
            Position = homePosition;
            HomePosition = homePosition;
            Speed = 2f;
            FearDistance = 100f;
            IsCarryingLeaf = false;
            random = new Random();
            FieldSize = new Size(800, 600);

            //****************************************инициализируем стек состояний - магазин PDA
            stateStack = new Stack<AntState>();
            PushState(AntState.Collecting); // Начинаем со сбора - начальное состояние
        }

        // Update: глобальная проверка курсора (push/pop), затем логика по CurrentState.
        public void Update(Point cursorPosition, Leaf leaf)
        {
            // Всегда проверяем, не слишком ли близко курсор - приоритет угрозы
            float distanceToCursor = Distance(Position, cursorPosition);
            // Переход: push Fleeing если dist(курсор) < FearDistance (100f) и не в Fleeing
            if (distanceToCursor < FearDistance)
            {
                // Если не в состоянии бегства, сохраняем текущее состояние и переходим к бегству
                if (CurrentState != AntState.Fleeing)
                {
                    PushState(AntState.Fleeing);
                }
            }
            // Переход: pop если dist(курсор) > 150f и в Fleeing
            else if (CurrentState == AntState.Fleeing)
            {
                // Если убежали достаточно далеко, возвращаемся к предыдущему состоянию
                if (distanceToCursor > FearDistance + 50f)
                {
                    PopState();
                }
            }

            // Выполняем логику текущего состояния - таблица PDA
            switch (CurrentState)
            {
                case AntState.Fleeing:
                    HandleFleeingState(cursorPosition);
                    break;
                case AntState.Collecting:
                    HandleCollectingState(leaf);
                    break;
                case AntState.ReturningHome:
                    HandleReturningHomeState();
                    break;
            }
        }

        public void SetFieldSize(Size size)
        {
            FieldSize = size;
        }

        //*********************************************** Методы для работы со стеком состояний - операции PDA (push/pop)
        private void PushState(AntState newState)
        {
            if (CurrentState != newState)
            {
                stateStack.Push(CurrentState); // Сохраняем текущее в стек (LIFO)
                CurrentState = newState; // Переход к новому
            }
        }

        private void PopState()//********************************************* pop - возврат к предыдущему
        {
            if (stateStack.Count > 0)
            {
                CurrentState = stateStack.Pop(); // Восстанавливаем из стека
            }
            else
            {
                CurrentState = AntState.Collecting; // По умолчанию возвращаемся к сбору
            }
        }

        // CheckBounds: границы поля (margin=20px).
        private void CheckBounds()
        {
            float margin = 20f;

            if (Position.X < margin)
            {
                Position = new PointF(margin, Position.Y);
            }
            if (Position.X > FieldSize.Width - margin)
            {
                Position = new PointF(FieldSize.Width - margin, Position.Y);
            }
            if (Position.Y < margin)
            {
                Position = new PointF(Position.X, margin);
            }
            if (Position.Y > FieldSize.Height - margin)
            {
                Position = new PointF(Position.X, FieldSize.Height - margin);
            }
        }

        // Fleeing: движение от курсора (ускоренное). Выход via pop в Update.
        private void HandleFleeingState(Point cursorPosition)
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
        }

        // Collecting: к листику.
        // Переход: push ReturningHome если dist(листик) < 10f.
        private void HandleCollectingState(Leaf leaf)
        {
            // Если есть листик, идем к нему
            if (leaf != null && !leaf.IsCollected)
            {
                Target = leaf.Position;
                MoveTowardsTarget();

                // Проверяем, собрали ли листик - переход via push
                // Зависит от: dist(листик) < 10f
                if (Distance(Position, leaf.Position) < 10f)
                {
                    leaf.IsCollected = true;
                    IsCarryingLeaf = true;
                    PushState(AntState.ReturningHome); // Push ReturningHome (стек: [Collecting, ReturningHome])
                    Target = HomePosition;
                }
            }
        }

        // ReturningHome: к норке.
        // Переход: pop если dist(норка) < 10f (возврат к Collecting).
        private void HandleReturningHomeState()
        {
            // Двигаемся домой
            MoveTowardsTarget();

            // Проверяем, дошли ли до дома - переход via pop
            // Зависит от: dist(норка) < 10f
            if (Distance(Position, HomePosition) < 10f)
            {
                IsCarryingLeaf = false;
                CollectedLeaves++;
                PopState(); // Возвращаемся к предыдущему состоянию (скорее всего Collecting)
            }
        }

        // MoveTowardsTarget: движение к цели.
        private void MoveTowardsTarget()
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