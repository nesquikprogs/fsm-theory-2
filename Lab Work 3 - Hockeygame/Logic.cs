using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HockeyGame
{
    //Перечисление для идентификации команд (левая и правая)
    public enum Team { Left, Right }

    //Класс для работы с 2D векторами - основа всех вычислений позиций и движений
    public class Vector2D
    {
        //Координаты X и Y вектора
        public double X { get; set; }
        public double Y { get; set; }

        //Конструктор с параметрами по умолчанию (0,0)
        public Vector2D(double x = 0, double y = 0)
        {
            X = x;
            Y = y;
        }

        //Длина вектора (модуль) - расстояние от начала координат до точки
        public double Length => Math.Sqrt(X * X + Y * Y);

        //Квадрат длины вектора - используется для оптимизации сравнений расстояний
        public double LengthSquared => X * X + Y * Y;

        //Нормализация вектора - приведение к длине 1 при сохранении направления
        public Vector2D Normalize()
        {
            if (Length > 0) return new Vector2D(X / Length, Y / Length);
            return new Vector2D(); //Возвращаем нулевой вектор если длина 0
        }


        public static Vector2D operator +(Vector2D a, Vector2D b) => new Vector2D(a.X + b.X, a.Y + b.Y);


        public static Vector2D operator -(Vector2D a, Vector2D b) => new Vector2D(a.X - b.X, a.Y - b.Y);


        public static Vector2D operator *(Vector2D v, double scalar) => new Vector2D(v.X * scalar, v.Y * scalar);


        public static Vector2D operator /(Vector2D v, double scalar) => new Vector2D(v.X / scalar, v.Y / scalar);


        public static double Distance(Vector2D a, Vector2D b) => (a - b).Length;

        //Статический метод вычисления квадрата расстояния (оптимизированная версия)
        public static double DistanceSquared(Vector2D a, Vector2D b) => (a - b).LengthSquared;
    }

    //Класс конечного автомата на основе стека для управления состояниями ИИ
    public class StackFSM
    {
        //Стек для хранения состояний 
        private Stack<Action> states = new Stack<Action>();

        //Основной метод обновления - выполняется каждый кадр
        public void Update()
        {
            //Выполняем текущее активное состояние (верхнее в стеке)
            if (states.Count > 0) states.Peek()?.Invoke();
        }


        public void PushState(Action state) => states.Push(state);


        public void PopState() { if (states.Count > 0) states.Pop(); }


        public void Clear() => states.Clear();


        public int Count => states.Count;
    }

    //Класс для управления движением объектов с физикой (игроки, шайба)
    public class Boid
    {
        //Текущая позиция объекта
        public Vector2D Position { get; set; }
        //Текущая скорость объекта
        public Vector2D Velocity { get; set; }
        //Сила руления (накопленная за кадр)
        public Vector2D Steering { get; set; }
        //Масса объекта (влияет на ускорение)
        public double Mass { get; set; }
        //Максимальная скорость объекта
        public double MaxSpeed { get; set; } = 200;

        //Конструктор с начальной позицией и массой
        public Boid(double x, double y, double mass)
        {
            Position = new Vector2D(x, y);
            Velocity = new Vector2D(); //Начальная скорость = 0
            Steering = new Vector2D(); //Начальная сила = 0
            Mass = mass;
        }

        //Основной метод обновления физики движения
        public void Update(double deltaTime)
        {
            //Применяем физику: F = m*a => a = F/m
            var acceleration = Steering / Mass;
            //Обновляем скорость: v = v0 + a*t
            Velocity = Velocity + acceleration * deltaTime;

            //Ограничиваем максимальную скорость
            if (Velocity.Length > MaxSpeed)
                Velocity = Velocity.Normalize() * MaxSpeed;

            //Обновляем позицию: s = s0 + v*t
            Position = Position + Velocity * deltaTime;

            //Сбрасываем силу руления для следующего кадра
            Steering = new Vector2D();
        }

        //Поведение "преследование" - движение к цели
        public Vector2D Seek(Vector2D target, double slowingRadius = 0)
        {
            //Вектор направления к цели
            var desiredVelocity = target - Position;
            var distance = desiredVelocity.Length;

            if (distance <= slowingRadius && slowingRadius > 0)
            {
                //Режим плавного замедления при приближении к цели
                desiredVelocity = desiredVelocity.Normalize() * MaxSpeed * (distance / slowingRadius);
            }
            else
            {
                //Режим полной скорости
                desiredVelocity = desiredVelocity.Normalize() * MaxSpeed;
            }

            //Возвращаем силу, необходимую для достижения желаемой скорости
            return desiredVelocity - Velocity;
        }

        //Поведение "прибытие" - плавное замедление у цели
        public Vector2D Arrive(Vector2D target, double slowingRadius = 50) => Seek(target, slowingRadius);

        //Поведение "блуждание" - случайное движение
        public Vector2D Wander()
        {
            //Создаем точку впереди по направлению движения
            var circleCenter = Velocity.Length > 0 ? Velocity.Normalize() * 50 : new Vector2D(1, 0);
            //Случайное смещение от точки
            var displacement = new Vector2D(0, -1) * 30;
            return circleCenter + displacement;
        }

        //Поведение "преследование с предсказанием" - перехват движущейся цели
        public Vector2D Pursuit(Boid target)
        {
            //Вычисляем время до перехвата
            var distance = Vector2D.Distance(Position, target.Position);
            var time = distance / MaxSpeed;
            //Предсказываем будущую позицию цели
            var futurePosition = target.Position + target.Velocity * time;
            return Seek(futurePosition);
        }

        //Поведение "следование за лидером" - поддержка атаки
        public Vector2D FollowLeader(Boid leader)
        {
            //Вычисляем позицию позади лидера
            var behind = leader.Position - leader.Velocity.Normalize() * 40;
            return Arrive(behind, 30);
        }

        //Поведение "разделение" - избежание столкновений с товарищами
        public Vector2D Separation(List<Athlete> athletes, double separationDistance = 50)
        {
            var steer = new Vector2D();
            int count = 0;

            foreach (var other in athletes)
            {
                if (other.Boid != this) //Не проверяем себя
                {
                    var distance = Vector2D.Distance(Position, other.Boid.Position);
                    if (distance > 0 && distance < separationDistance)
                    {
                        //Вычисляем вектор отталкивания
                        var diff = Position - other.Boid.Position;
                        diff = diff.Normalize() / distance; //Сила обратно пропорциональна расстоянию
                        steer = steer + diff;
                        count++;
                    }
                }
            }

            //Усредняем силу отталкивания
            if (count > 0) steer = steer / count;
            //Преобразуем в силу руления
            if (steer.Length > 0) steer = steer.Normalize() * MaxSpeed - Velocity;

            return steer;
        }

        //Поведение "избежание столкновений" с противниками
        public Vector2D CollisionAvoidance(List<Athlete> opponents, double avoidanceDistance = 100)
        {
            var steer = new Vector2D();
            int count = 0;

            foreach (var opponent in opponents)
            {
                var distance = Vector2D.Distance(Position, opponent.Boid.Position);
                if (distance < avoidanceDistance)
                {
                    var diff = Position - opponent.Boid.Position;
                    diff = diff.Normalize() / distance;
                    steer = steer + diff;
                    count++;
                }
            }

            if (count > 0) steer = steer / count;
            if (steer.Length > 0) steer = steer.Normalize() * MaxSpeed;

            return steer;
        }
    }

    //Класс шайбы - основного игрового объекта
    public class Puck
    {
        //Текущая позиция шайбы
        public Vector2D Position { get; set; }
        //Текущая скорость шайбы
        public Vector2D Velocity { get; set; }
        //Игрок, владеющий шайбой (null если шайба свободна)
        public Athlete Owner { get; private set; }
        //Радиус шайбы для отрисовки и коллизий
        public double Radius { get; set; } = 10;

        //Конструктор с начальной позицией
        public Puck(double x, double y)
        {
            Position = new Vector2D(x, y);
            Velocity = new Vector2D();
        }

        //Установка владельца шайбы
        public void SetOwner(Athlete owner)
        {
            if (Owner != owner)
            {
                Owner = owner;
                Velocity = new Vector2D(); //Шайба останавливается при взятии
            }
        }

        //Очистка владельца шайбы
        public void ClearOwner() => Owner = null;

        //Обновление состояния шайбы
        public void Update(double deltaTime)
        {
            if (Owner != null)
            {
                //Шайба следует за игроком-владельцем
                PlaceAheadOfOwner();
            }
            else
            {
                //Свободное движение шайбы
                Position = Position + Velocity * deltaTime;
                Velocity = Velocity * 0.98; //Трение льда (замедление)
            }
        }

        //Размещение шайбы перед игроком-владельцем
        private void PlaceAheadOfOwner()
        {
            if (Owner != null && Owner.Boid.Velocity.Length > 0)
            {
                //Вычисляем позицию на 30 единиц вперед по направлению движения игрока
                var ahead = Owner.Boid.Velocity.Normalize() * 30;
                Position = Owner.Boid.Position + ahead;
            }
        }

        //Удар по шайбе клюшкой
        public void GoFromStickHit(Athlete athlete, Vector2D destination, double speed = 160)
        {
            //Убеждаемся, что шайба перед игроком
            PlaceAheadOfOwner();
            //Освобождаем шайбу
            ClearOwner();

            //Вычисляем направление удара
            var newVelocity = destination - Position;
            if (newVelocity.Length > 0)
            {
                //Задаем новую скорость шайбы
                Velocity = newVelocity.Normalize() * speed;
            }
        }
    }

    //Класс ворот - цели для забития голов
    public class Goal
    {
        //Позиция центра ворот
        public Vector2D Position { get; set; }
        //Ширина ворот
        public double Width { get; set; } = 10;
        //Высота ворот
        public double Height { get; set; } = 60;
        //Команда, которой принадлежат ворота
        public Team Team { get; set; }
        //Количество забитых голов
        public int Score { get; set; }

        //Конструктор с позицией и принадлежностью команде
        public Goal(double x, double y, Team team)
        {
            Position = new Vector2D(x, y);
            Team = team;
        }

        //Проверка попадания точки (шайбы) в ворота
        public bool ContainsPoint(Vector2D point)
        {
            return point.X >= Position.X - Width / 2 &&
                   point.X <= Position.X + Width / 2 &&
                   point.Y >= Position.Y - Height / 2 &&
                   point.Y <= Position.Y + Height / 2;
        }
    }

    //Класс хоккейного катка - игрового поля
    public class Rink
    {
        //Ширина катка
        public double Width { get; set; }
        //Высота катка
        public double Height { get; set; }

        //Конструктор с размерами катка
        public Rink(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    //Класс хоккеиста - основной управляемой единицы в игре
    public class Athlete
    {
        //Физическое тело игрока
        public Boid Boid { get; private set; }
        //Уникальный идентификатор игрока
        public int Id { get; private set; }
        //Флаг управления (true - ИИ, false - игрок)
        public bool IsControlledByAI { get; set; } = true;
        //Команда игрока
        public Team Team { get; private set; }
        //Начальная позиция игрока
        public Vector2D InitialPosition { get; private set; }
        //Роль игрока в команде
        public PlayerRole Role { get; private set; }

        private StackFSM brain;
        //Ссылка на игровое состояние
        private PlayState playState;

        private Random random = new Random();
        //Текущее состояние для отладки и логики
        private string currentStateName = "Idle";

        //Роли игроков для стратегического поведения
        public enum PlayerRole { Forward, Defender, Neutral }

        //Конструктор игрока с параметрами
        public Athlete(int id, double x, double y, double mass, Team team, PlayState playState, PlayerRole role = PlayerRole.Neutral)
        {
            Id = id;
            Boid = new Boid(x, y, mass);
            Team = team;
            this.playState = playState;
            InitialPosition = new Vector2D(x, y);
            Role = role;

            //Инициализация конечного автомата с начальным состоянием
            brain = new StackFSM();
            brain.PushState(Idle);
            currentStateName = "Idle";
        }
        //Принудительный переход к празднованию гола
        public void ForceCelebrateGoal()
        {
            brain.Clear();
            brain.PushState(CelebrateGoal);
            currentStateName = "CelebrateGoal";
        }

        //Состояние "празднование гола" - после забития гола
        private void CelebrateGoal()
        {
            currentStateName = "CelebrateGoal";


        }

        //Основной метод обновления состояния игрока
        public void Update(double deltaTime)
        {
            //Сбрасываем силы руления для нового кадра
            Boid.Steering = new Vector2D();

            if (IsControlledByAI)
            {
                //Обновление логики ИИ через конечный автомат
                brain.Update();
            }
            else
            {
                //Управление игроком с помощью мыши
                FollowMouseCursor();
            }

            //Обновление физики движения
            Boid.Update(deltaTime);
        }

        //Управление игроком с помощью мыши
        private void FollowMouseCursor()
        {
            //Сбрасываем скорость при ручном управлении для более точного контроля
            if (Boid.Velocity.Length > Boid.MaxSpeed * 0.5)
            {
                Boid.Velocity = Boid.Velocity * 0.9; //Плавное замедление
            }

            var mousePos = playState.GetMousePosition();
            if (mousePos != null)
            {
                //Двигаемся к позиции курсора с плавным замедлением
                Boid.Steering = Boid.Steering + Boid.Arrive(mousePos, 50);
            }
        }

        //Состояние "ожидание" - базовое состояние наблюдения за шайбой
        private void Idle()
        {
            currentStateName = "Idle";
            var puck = playState.GetPuck();

            //Поворачиваемся к шайбе
            StopAndLookAt(puck.Position);

            //Если игра на паузе - выходим
            if (playState.StandStill) return;

            var puckOwner = playState.GetPuckOwner();
            if (puckOwner != null)
            {
                //Шайба у кого-то есть - переходим к соответствующему состоянию
                brain.PopState();
                if (playState.DoesTeamHaveThePuck(Team))
                {
                    //Шайба у нашей команды - атакуем
                    brain.PushState(Attack);
                    currentStateName = "Attack";
                }
                else
                {
                    //Шайба у противника - отбираем
                    brain.PushState(StealPuck);
                    currentStateName = "StealPuck";
                }
            }
            else if (Vector2D.Distance(Boid.Position, puck.Position) < 150)
            {
                //Шайба свободна и близко - проверяем, должны ли мы ее преследовать
                if (playState.ShouldIPursuePuck(this))
                {
                    brain.PopState();
                    brain.PushState(PursuePuck);
                    currentStateName = "PursuePuck";
                }
                else
                {
                    //Занимаем позицию для поддержки
                    SupportPuckCarrier();
                }
            }
        }

        //Состояние "преследование шайбы" - когда шайба свободна
        private void PursuePuck()
        {
            currentStateName = "PursuePuck";
            var puck = playState.GetPuck();
            var teammates = playState.GetTeammates(Team, this);

            //Избегаем столкновений с товарищами
            Boid.Steering = Boid.Steering + Boid.Separation(teammates);

            //Проверяем, остались ли мы ближайшими к шайбе
            if (!playState.IsClosestToPuck(this) && Vector2D.Distance(Boid.Position, puck.Position) > 50)
            {

                brain.PopState();
                brain.PushState(Idle);
                currentStateName = "Idle";
                return;
            }

            if (Vector2D.Distance(Boid.Position, puck.Position) > 150)
            {
                //Шайба слишком далеко - возвращаемся к ожиданию
                brain.PopState();
                brain.PushState(Idle);
                currentStateName = "Idle";
            }
            else
            {
                if (puck.Owner == null)
                {
                    //Шайба все еще свободна - продолжаем преследование
                    Boid.Steering = Boid.Steering + Boid.Seek(puck.Position);
                }
                else
                {
                    //Шайбу кто-то подобрал - переходим к соответствующему состоянию
                    brain.PopState();
                    if (playState.DoesTeamHaveThePuck(Team))
                    {
                        brain.PushState(Attack);
                        currentStateName = "Attack";
                    }
                    else
                    {
                        brain.PushState(StealPuck);
                        currentStateName = "StealPuck";
                    }
                }
            }
        }

        //Состояние "атака" - когда наша команда владеет шайбой
        private void Attack()
        {
            currentStateName = "Attack";
            var puckOwner = playState.GetPuckOwner();

            if (puckOwner != null)
            {
                if (playState.DoesTeamHaveThePuck(Team))
                {
                    if (this == puckOwner)
                    {
                        //Я владею шайбой - двигаюсь к воротам противника
                        var goalPos = playState.GetOpponentGoalPosition(Team);
                        Boid.Steering = Boid.Steering + Boid.Seek(goalPos);

                        //Уклоняюсь от противников
                        var opponents = playState.GetOpponents(Team);
                        Boid.Steering = Boid.Steering + Boid.CollisionAvoidance(opponents);
                    }
                    else
                    {
                        //Шайба у товарища - поддерживаю атаку
                        if (IsAheadOfMe(puckOwner.Boid))
                        {
                            //Лидер впереди - следую за ним
                            Boid.Steering = Boid.Steering + Boid.FollowLeader(puckOwner.Boid);
                            var teammates = playState.GetTeammates(Team, this);
                            Boid.Steering = Boid.Steering + Boid.Separation(teammates);
                        }
                        else
                        {
                            //Лидер сзади - занимаю оборонительную позицию
                            var teammates = playState.GetTeammates(Team, this);
                            Boid.Steering = Boid.Steering + Boid.Separation(teammates);
                        }
                    }
                }
                else
                {
                    //Шайба перешла к противнику - переходим к отбору
                    brain.PopState();
                    brain.PushState(StealPuck);
                    currentStateName = "StealPuck";
                }
            }
            else
            {
                //Шайба свободна - преследуем
                brain.PopState();
                brain.PushState(PursuePuck);
                currentStateName = "PursuePuck";
            }
        }

        //Состояние "отбор шайбы" - когда противник владеет шайбой
        private void StealPuck()
        {
            currentStateName = "StealPuck";
            var puckOwner = playState.GetPuckOwner();

            if (puckOwner != null)
            {
                if (playState.DoesTeamHaveThePuck(Team))
                {
                    //Шайба снова у нас - переходим к атаке
                    brain.PopState();
                    brain.PushState(Attack);
                    currentStateName = "Attack";
                }
                else
                {
                    if (Vector2D.Distance(Boid.Position, puckOwner.Boid.Position) < 150)
                    {
                        //Противник близко - пытаемся отобрать шайбу с предсказанием
                        Boid.Steering = Boid.Steering + Boid.Pursuit(puckOwner.Boid);
                        var teammates = playState.GetTeammates(Team, this);
                        Boid.Steering = Boid.Steering + Boid.Separation(teammates, 50);
                    }
                    else
                    {
                        //Противник далеко - переходим к защите
                        brain.PopState();
                        brain.PushState(Defend);
                        currentStateName = "Defend";
                    }
                }
            }
            else
            {
                //Шайба свободна - переходим к преследованию
                brain.PopState();
                brain.PushState(PursuePuck);
                currentStateName = "PursuePuck";
            }
        }

        //Состояние "защита" - возврат на свою позицию и охрана зоны
        private void Defend()
        {
            currentStateName = "Defend";

            //Возвращаемся на начальную позицию с плавным замедлением
            Boid.Steering = Boid.Steering + Boid.Arrive(InitialPosition, 80);

            var puckOwner = playState.GetPuckOwner();
            if (puckOwner != null)
            {
                if (playState.DoesTeamHaveThePuck(Team))
                {
                    //Шайба у нашей команды - переходим к атаке
                    brain.PopState();
                    brain.PushState(Attack);
                    currentStateName = "Attack";
                }
                else if (Vector2D.Distance(Boid.Position, puckOwner.Boid.Position) < 150)
                {
                    //Противник с шайбой близко - пытаемся отобрать
                    brain.PopState();
                    brain.PushState(StealPuck);
                    currentStateName = "StealPuck";
                }
            }
            else
            {
                //Шайба свободна - переходим к преследованию
                brain.PopState();
                brain.PushState(PursuePuck);
                currentStateName = "PursuePuck";
            }

            //Проверяем, достигли ли начальной позиции
            if (Vector2D.Distance(Boid.Position, InitialPosition) <= 5)
            {
                //Достигли позиции - переходим к патрулированию
                brain.PopState();
                brain.PushState(Patrol);
                currentStateName = "Patrol";
            }
        }

        //Состояние "патрулирование" - движение вокруг своей позиции
        private void Patrol()
        {
            currentStateName = "Patrol";

            //Случайное блуждание в зоне ответственности
            Boid.Steering = Boid.Steering + Boid.Wander();

            //Проверяем, не ушли ли слишком далеко от своей позиции
            if (Vector2D.Distance(Boid.Position, InitialPosition) > 10)
            {
                //Ушли далеко - возвращаемся к защите
                brain.PopState();
                brain.PushState(Defend);
                currentStateName = "Defend";
            }
        }

        //Метод поддержки игрока с шайбой - занятие выгодной позиции
        private void SupportPuckCarrier()
        {
            var puck = playState.GetPuck();
            var closestTeammate = playState.FindClosestPlayerToPuck(Team);

            if (closestTeammate != null && closestTeammate != this)
            {
                //Вычисляем позицию для поддержки
                var supportPosition = CalculateSupportPosition(closestTeammate);
                //Двигаемся к позиции поддержки
                Boid.Steering = Boid.Steering + Boid.Arrive(supportPosition, 30);
            }
        }

        //Расчет позиции для поддержки игрока с шайбой
        private Vector2D CalculateSupportPosition(Athlete puckCarrier)
        {
            var goalPos = playState.GetOpponentGoalPosition(Team);
            //Направление к воротам противника
            var directionToGoal = (goalPos - puckCarrier.Boid.Position).Normalize();
            //Смещение в сторону (влево или вправо в зависимости от команды)
            var sideOffset = Team == Team.Left ? new Vector2D(-30, 0) : new Vector2D(30, 0);
            //Позиция поддержки: сбоку и немного впереди несущего шайбу
            return puckCarrier.Boid.Position + directionToGoal * 40 + sideOffset;
        }

        //Остановка и поворот к указанной точке (без движения)
        private void StopAndLookAt(Vector2D point)
        {
            var direction = point - Boid.Position;
            if (direction.Length > 0)
            {
                //Устанавливаем минимальную скорость для поворота
                Boid.Velocity = direction.Normalize() * 0.01;
            }
        }

        //Проверка, находится ли цель впереди меня относительно ворот противника
        private bool IsAheadOfMe(Boid otherBoid)
        {
            var goalPos = playState.GetOpponentGoalPosition(Team);
            var targetDistance = Vector2D.Distance(goalPos, otherBoid.Position);
            var myDistance = Vector2D.Distance(goalPos, Boid.Position);
            //Цель впереди, если она ближе к воротам противника
            return targetDistance <= myDistance;
        }

        //Принудительный переход к преследованию шайбы (используется при сбросе позиций)
        public void ForcePursuePuck()
        {
            brain.Clear();
            brain.PushState(PursuePuck);
            currentStateName = "PursuePuck";
        }

        //Сброс в состояние ожидания
        public void ResetToIdle()
        {
            brain.Clear();
            brain.PushState(Idle);
            currentStateName = "Idle";
        }

        //Подготовка к матчу - возврат на защитную позицию
        public void PrepareForMatch()
        {
            brain.Clear();
            brain.PushState(Defend);
            currentStateName = "Defend";
        }

        //Проверка текущего состояния игрока
        public bool IsInState(string stateName)
        {
            return currentStateName == stateName;
        }

        //Получение имени текущего состояния
        public string GetCurrentStateName() => currentStateName;
    }

    //Основной класс игрового состояния - управляет всей игровой логикой
    public class PlayState
    {
        //Canvas для отрисовки игровых объектов
        private Canvas gameCanvas;
        //Игровое поле
        private Rink rink;
        //Шайба
        private Puck puck;
        //Команда слева
        private List<Athlete> leftTeam;
        //Команда справа
        private List<Athlete> rightTeam;
        //Ворота левой команды
        private Goal leftGoal;
        //Ворота правой команды
        private Goal rightGoal;
        //Генератор случайных чисел
        private Random random = new Random();
        //Позиция мыши для управления
        private Vector2D mousePosition;

        //Флаг паузы игры
        public bool StandStill { get; set; } = false;
        //Событие изменения счета
        public event Action<int, int> OnScoreChanged;
        //Событие изменения состояния
        public event Action<string> OnStateChanged;

        //Конструктор с передачей Canvas для отрисовки
        public PlayState(Canvas canvas)
        {
            gameCanvas = canvas;
            InitializeGame();
        }
        //Проверка забития гола
        private void CheckGoal()
        {
            if (leftGoal.ContainsPoint(puck.Position))
            {
                //Гол в левые ворота - очко правой команде
                rightGoal.Score++;

                //Переводим всех игроков в состояние празднования
                SetAllPlayersToCelebrate();

                RandomizePositions();
                OnScoreChanged?.Invoke(leftGoal.Score, rightGoal.Score);
            }
            else if (rightGoal.ContainsPoint(puck.Position))
            {
                //Гол в правые ворота - очко левой команде
                leftGoal.Score++;

                //Переводим всех игроков в состояние празднования
                SetAllPlayersToCelebrate();

                RandomizePositions();
                OnScoreChanged?.Invoke(leftGoal.Score, rightGoal.Score);
            }
        }

        //Перевод всех игроков в состояние празднования гола
        private void SetAllPlayersToCelebrate()
        {
            foreach (var athlete in leftTeam.Concat(rightTeam))
            {
                athlete.ForceCelebrateGoal();
            }

            //Устанавливаем паузу на 2 секунды для празднования
            StandStill = true;
            Task.Delay(2000).ContinueWith(t =>
            {
                StandStill = false;
                //После паузы сбрасываем игроков в обычное состояние
                foreach (var athlete in leftTeam.Concat(rightTeam))
                {
                    athlete.ResetToIdle();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        //Методы доступа к размерам поля для состояния празднования
        public double GetRinkWidth() => gameCanvas.ActualWidth;
        public double GetRinkHeight() => gameCanvas.ActualHeight;

        //Инициализация игровых объектов
        private void InitializeGame()
        {
            //Создаем каток по размеру Canvas
            rink = new Rink(gameCanvas.ActualWidth, gameCanvas.ActualHeight);

            //Создаем ворота по краям поля
            leftGoal = new Goal(5, gameCanvas.ActualHeight / 2, Team.Left);
            rightGoal = new Goal(gameCanvas.ActualWidth - 5, gameCanvas.ActualHeight / 2, Team.Right);

            //Шайба в центре поля
            puck = new Puck(gameCanvas.ActualWidth / 2, gameCanvas.ActualHeight / 2);

            //Создаем команды
            leftTeam = CreateTeam(Team.Left, 5);
            rightTeam = CreateTeam(Team.Right, 6);

            //Первый игрок левой команды управляется игроком
            if (leftTeam.Count > 0)
                leftTeam[0].IsControlledByAI = false;


            RandomizePositions();
        }

        //Создание команды игроков
        private List<Athlete> CreateTeam(Team team, int playerCount)
        {
            var athletes = new List<Athlete>();
            //Начальная позиция команды (левая или правая часть поля)
            double startX = team == Team.Left ? 200 : gameCanvas.ActualWidth - 200;

            for (int i = 0; i < playerCount; i++)
            {
                //Равномерное распределение игроков по вертикали
                double y = gameCanvas.ActualHeight / (playerCount + 1) * (i + 1);

                //Назначаем роли в зависимости от позиции в команде
                var role = i switch
                {
                    0 or 1 => Athlete.PlayerRole.Forward,    //Первые два - нападающие
                    2 or 3 => Athlete.PlayerRole.Neutral,    //Средние - нейтральные
                    _ => Athlete.PlayerRole.Defender         //Последние - защитники
                };

                athletes.Add(new Athlete(i, startX, y, 1, team, this, role));
            }

            return athletes;
        }

        //Основной метод обновления игрового состояния
        public void Update()
        {
            //Обновляем всех игроков
            foreach (var athlete in leftTeam.Concat(rightTeam))
                athlete.Update(0.016); //deltaTime для 60 FPS

            //Обновляем шайбу
            puck.Update(0.016);

            //Проверяем столкновения
            CheckCollisions();
            //Проверяем голы
            CheckGoal();
            //Применяем ограничения поля
            ApplyRinkConstraints();
            //Обновляем текстовую информацию
            UpdateStateText();
        }

        //Обновление текстовой информации о состоянии
        private void UpdateStateText()
        {
            if (leftTeam.Count > 0 && !leftTeam[0].IsControlledByAI)
                OnStateChanged?.Invoke("Player Control");
            else
                OnStateChanged?.Invoke("AI Control");
        }

        //Проверка столкновений между игровыми объектами
        private void CheckCollisions()
        {
            //Столкновения игроков с шайбой
            foreach (var athlete in leftTeam.Concat(rightTeam))
            {
                var distance = Vector2D.Distance(athlete.Boid.Position, puck.Position);
                if (distance < 20 && puck.Owner == null)
                {
                    //Игрок подбирает шайбу если она близко и свободна
                    puck.SetOwner(athlete);
                    break; //Только один игрок может взять шайбу за кадр
                }
            }

            //Столкновения игроков друг с другом
            foreach (var athlete1 in leftTeam.Concat(rightTeam))
            {
                foreach (var athlete2 in leftTeam.Concat(rightTeam))
                {
                    if (athlete1 != athlete2) //Не проверяем столкновение с собой
                    {
                        var distance = Vector2D.Distance(athlete1.Boid.Position, athlete2.Boid.Position);
                        if (distance < 30)
                        {
                            //Простая физика столкновения
                            var dir = athlete1.Boid.Position - athlete2.Boid.Position;
                            if (dir.Length > 0)
                            {
                                dir = dir.Normalize();
                                //Отталкивание игроков друг от друга
                                athlete1.Boid.Velocity = athlete1.Boid.Velocity + dir * 2;
                                athlete2.Boid.Velocity = athlete2.Boid.Velocity - dir * 2;
                            }

                            //Шайба может перейти к другому игроку при столкновении (30% chance)
                            if (puck.Owner == athlete1 && random.NextDouble() < 0.3)
                                puck.SetOwner(athlete2);
                            else if (puck.Owner == athlete2 && random.NextDouble() < 0.3)
                                puck.SetOwner(athlete1);
                        }
                    }
                }
            }
        }



        //Ограничение движения объектов в пределах катка
        private void ApplyRinkConstraints()
        {
            //Удерживаем игроков в пределах поля (отступ 20px от краев)
            foreach (var athlete in leftTeam.Concat(rightTeam))
            {
                athlete.Boid.Position.X = Math.Max(20, Math.Min(gameCanvas.ActualWidth - 20, athlete.Boid.Position.X));
                athlete.Boid.Position.Y = Math.Max(20, Math.Min(gameCanvas.ActualHeight - 20, athlete.Boid.Position.Y));
            }

            //Удерживаем шайбу в пределах поля (отступ 10px от краев)
            puck.Position.X = Math.Max(10, Math.Min(gameCanvas.ActualWidth - 10, puck.Position.X));
            puck.Position.Y = Math.Max(10, Math.Min(gameCanvas.ActualHeight - 10, puck.Position.Y));
        }

        //Отрисовка всех игровых объектов
        public void Render()
        {
            //Очищаем Canvas перед новой отрисовкой
            gameCanvas.Children.Clear();

            //Рисуем каток (белый прямоугольник с черной границей)
            var rinkRect = new Rectangle
            {
                Width = gameCanvas.ActualWidth,
                Height = gameCanvas.ActualHeight,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            gameCanvas.Children.Add(rinkRect);

            //Рисуем центральную линию (красная пунктирная)
            var centerLine = new Line
            {
                X1 = gameCanvas.ActualWidth / 2,
                Y1 = 0,
                X2 = gameCanvas.ActualWidth / 2,
                Y2 = gameCanvas.ActualHeight,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 10, 5 }
            };
            gameCanvas.Children.Add(centerLine);

            //Рисуем ворота
            DrawGoal(leftGoal);
            DrawGoal(rightGoal);

            //Рисуем игроков команд (синие и красные)
            foreach (var athlete in leftTeam)
                DrawAthlete(athlete, Brushes.Blue);
            foreach (var athlete in rightTeam)
                DrawAthlete(athlete, Brushes.Red);

            //Рисуем шайбу
            DrawPuck();
        }

        //Отрисовка ворот
        private void DrawGoal(Goal goal)
        {
            var goalRect = new Rectangle
            {
                Width = goal.Width,
                Height = goal.Height,
                Fill = Brushes.LightGray,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Canvas.SetLeft(goalRect, goal.Position.X - goal.Width / 2);
            Canvas.SetTop(goalRect, goal.Position.Y - goal.Height / 2);
            gameCanvas.Children.Add(goalRect);
        }

        //Отрисовка игрока
        private void DrawAthlete(Athlete athlete, Brush color)
        {
            //Рисуем игрока как круг
            var ellipse = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = color
            };
            Canvas.SetLeft(ellipse, athlete.Boid.Position.X - 10);
            Canvas.SetTop(ellipse, athlete.Boid.Position.Y - 10);
            gameCanvas.Children.Add(ellipse);

            //Рисуем направление движения (линия от центра)
            if (athlete.Boid.Velocity.Length > 0)
            {
                var direction = athlete.Boid.Velocity.Normalize() * 15;
                var line = new Line
                {
                    X1 = athlete.Boid.Position.X,
                    Y1 = athlete.Boid.Position.Y,
                    X2 = athlete.Boid.Position.X + direction.X,
                    Y2 = athlete.Boid.Position.Y + direction.Y,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                gameCanvas.Children.Add(line);
            }

            //Отображаем роль игрока (F - нападающий, D - защитник, N - нейтральный)
            var roleText = new TextBlock
            {
                Text = athlete.Role.ToString()[0].ToString(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 10
            };
            Canvas.SetLeft(roleText, athlete.Boid.Position.X - 5);
            Canvas.SetTop(roleText, athlete.Boid.Position.Y - 6);
            gameCanvas.Children.Add(roleText);
        }

        //Отрисовка шайбы
        private void DrawPuck()
        {
            var ellipse = new Ellipse
            {
                Width = puck.Radius * 2,
                Height = puck.Radius * 2,
                Fill = Brushes.Black
            };
            Canvas.SetLeft(ellipse, puck.Position.X - puck.Radius);
            Canvas.SetTop(ellipse, puck.Position.Y - puck.Radius);
            gameCanvas.Children.Add(ellipse);
        }

        //Обновление позиции мыши для управления
        public void UpdateMousePosition(Point position) => mousePosition = new Vector2D(position.X, position.Y);

        //Обработка клика мыши (удар по шайбе)
        public void HandleMouseClick(Point position)
        {
            mousePosition = new Vector2D(position.X, position.Y);
            var player = leftTeam.FirstOrDefault(a => !a.IsControlledByAI);
            //Если игрок владеет шайбой - бьем по ней
            if (player != null && puck.Owner == player)
                puck.GoFromStickHit(player, mousePosition);
        }

        //Методы доступа к игровым объектам
        public Vector2D GetMousePosition() => mousePosition;
        public Puck GetPuck() => puck;
        public Athlete GetPuckOwner() => puck.Owner;

        //Проверка владения шайбой командой
        public bool DoesTeamHaveThePuck(Team team) => puck.Owner != null && puck.Owner.Team == team;

        //Получение позиции ворот противника
        public Vector2D GetOpponentGoalPosition(Team team) => team == Team.Left ? rightGoal.Position : leftGoal.Position;

        //Получение списка товарищей по команде
        public List<Athlete> GetTeammates(Team team, Athlete exclude = null)
        {
            var teamList = team == Team.Left ? leftTeam : rightTeam;
            return teamList.Where(a => a != exclude).ToList();
        }

        //Получение списка противников
        public List<Athlete> GetOpponents(Team team) => team == Team.Left ? rightTeam : leftTeam;

        //Поиск ближайшего к шайбе игрока команды
        public Athlete FindClosestPlayerToPuck(Team team)
        {
            var teamPlayers = team == Team.Left ? leftTeam : rightTeam;
            Athlete closestPlayer = null;
            double minDistance = double.MaxValue;

            foreach (var player in teamPlayers)
            {
                var distance = Vector2D.Distance(player.Boid.Position, puck.Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPlayer = player;
                }
            }

            return closestPlayer;
        }

        //Проверка, является ли игрок ближайшим к шайбе в своей команде
        public bool IsClosestToPuck(Athlete athlete)
        {
            var closest = FindClosestPlayerToPuck(athlete.Team);
            return closest != null && closest.Id == athlete.Id;
        }

        //Интеллектуальная проверка - должен ли игрок преследовать шайбу
        public bool ShouldIPursuePuck(Athlete athlete)
        {
            var closestTeammate = FindClosestPlayerToPuck(athlete.Team);

            //Если я ближайший - определенно да
            if (closestTeammate != null && closestTeammate.Id == athlete.Id)
                return true;

            if (closestTeammate != null)
            {
                var distanceToClosest = Vector2D.Distance(closestTeammate.Boid.Position, puck.Position);
                var myDistance = Vector2D.Distance(athlete.Boid.Position, puck.Position);

                //Если ближайший товарищ далеко (>200), а я близко (<100) - я могу идти
                if (distanceToClosest > 100 && myDistance < 1000)
                    return true;
            }

            //Если в команде никто не преследует шайбу, и я достаточно близко
            if (!IsAnyonePursuingPuck(athlete.Team) &&
                Vector2D.Distance(athlete.Boid.Position, puck.Position) < 120)
                return true;

            return false;
        }

        //Проверка, есть ли уже игроки в состоянии преследования
        private bool IsAnyonePursuingPuck(Team team)
        {
            var teamPlayers = team == Team.Left ? leftTeam : rightTeam;
            return teamPlayers.Any(p => p.IsInState("PursuePuck"));
        }

        //Случайное распределение позиций игроков (сброс раунда)
        public void RandomizePositions()
        {
            //Сбрасываем шайбу в центр поля
            puck.Position = new Vector2D(gameCanvas.ActualWidth / 2, gameCanvas.ActualHeight / 2);
            puck.ClearOwner();
            puck.Velocity = new Vector2D();

            double fieldWidth = gameCanvas.ActualWidth;
            double fieldHeight = gameCanvas.ActualHeight;
            double halfWidth = fieldWidth / 2;

            //Случайные позиции для всех игроков
            foreach (var athlete in leftTeam.Concat(rightTeam))
            {
                double minX, maxX;

                // Определяем зону для команды
                if (athlete.Team == Team.Left) //Синяя команда (слева)
                {
                    minX = halfWidth - 100;
                    maxX = halfWidth; //Левая половина поля
                }
                else //Красная команда (справа)
                {
                    minX = halfWidth;
                    maxX = halfWidth + 100; //Правая половина поля
                }

                athlete.Boid.Position = new Vector2D(
                    random.Next((int)minX, (int)maxX),
                    random.Next(300, (int)fieldHeight - 300)
                );
                //Сбрасываем всех в состояние Idle
                athlete.ResetToIdle();
            }
        }



        //Полный перезапуск игры (сброс счета и полная переинициализация)
        public void ResetGame()
        {
            //Сбрасываем счет
            leftGoal.Score = 0;
            rightGoal.Score = 0;
            //Полная переинициализация игры
            InitializeGame();
            //Оповещаем об изменении счета
            OnScoreChanged?.Invoke(0, 0);
        }
    }
}