using System;
using System.ComponentModel;

// Состояния банковского счёта
public enum AccountState
{
    Good,        // Счёт положительный, можно снимать деньги
    Overdrawn,   // Счёт ушёл в минус, требуется погашение долга
    Closed       // Счёт закрыт, начальное и конечное состояние
}

// Класс банковского счёта с автоматом состояния и уведомлениями UI
public class BankAccount : INotifyPropertyChanged
{
    private AccountState _currentState;
    private decimal _balance;
    private string _status;

    public BankAccount()
    {
        // Инициализация счёта: закрыт, баланс 0
        CurrentState = AccountState.Closed;
        Balance = 0;
        Status = "Счет закрыт";
    }

    // Текущее состояние счёта
    public AccountState CurrentState
    {
        get => _currentState;
        private set
        {
            _currentState = value;
            OnPropertyChanged(nameof(CurrentState));
            OnPropertyChanged(nameof(StateDescription));
        }
    }

    // Баланс счёта
    public decimal Balance
    {
        get => _balance;
        private set
        {
            _balance = value;
            OnPropertyChanged(nameof(Balance));
        }
    }

    // Текстовое описание текущего состояния
    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    // Читабельное описание состояния для интерфейса
    public string StateDescription => CurrentState switch
    {
        AccountState.Good => "Счет Хороший",
        AccountState.Overdrawn => "Превышены Расходы по Счету",
        AccountState.Closed => "Счет Закрыт",
        _ => "Неизвестное состояние"
    };

    // Открытие счёта с начальными средствами
    public void OpenAccount(decimal initialBalance)
    {
        if (CurrentState != AccountState.Closed)
        {
            Status = "Ошибка: Счет уже открыт";
            return;
        }

        if (initialBalance <= 0)
        {
            Status = "Ошибка: Начальный баланс должен быть положительным";
            return;
        }

        Balance = initialBalance;
        CurrentState = AccountState.Good;
        Status = $"Счет открыт с балансом: {Balance}";
    }

    // Внесение денег на счёт
    public void Deposit(decimal amount)
    {
        if (CurrentState == AccountState.Closed)
        {
            Status = "Ошибка: Счет закрыт";
            return;
        }

        if (amount <= 0)
        {
            Status = "Ошибка: Сумма вклада должна быть положительной";
            return;
        }

        Balance += amount;

        // Если счёт был в минусе и баланс стал положительным — переводим в Good
        if (CurrentState == AccountState.Overdrawn && Balance >= 0)
        {
            CurrentState = AccountState.Good;
            Status = $"Долг погашен. Баланс: {Balance}";
        }
        else
        {
            Status = $"Вклад принят. Баланс: {Balance}";
        }
    }

    // Снятие денег со счёта
    public void Withdraw(decimal amount)
    {
        if (CurrentState == AccountState.Closed)
        {
            Status = "Ошибка: Счет закрыт";
            return;
        }

        if (amount <= 0)
        {
            Status = "Ошибка: Сумма снятия должна быть положительной";
            return;
        }

        decimal newBalance = Balance - amount;

        if (newBalance >= 0)
        {
            // Обычное снятие — баланс остаётся положительным
            Balance = newBalance;
            Status = $"Снятие выполнено. Баланс: {Balance}";
        }
        else
        {
            // Снятие с уходом в минус — переводим в Overdrawn
            Balance = newBalance;

            if (CurrentState == AccountState.Good)
            {
                CurrentState = AccountState.Overdrawn;
                Status = $"Превышены расходы по счету! Баланс: {Balance}";
            }
            else
            {
                Status = $"Снятие в состоянии превышения. Баланс: {Balance}";
            }
        }
    }

    // Погашение долга
    public void RepayDebt()
    {
        if (CurrentState != AccountState.Overdrawn)
        {
            Status = "Ошибка: Нет долга для погашения";
            return;
        }

        if (Balance >= 0)
        {
            CurrentState = AccountState.Good;
            Status = $"Долг погашен. Баланс: {Balance}";
        }
        else
        {
            Status = $"Недостаточно средств для погашения долга. Баланс: {Balance}";
        }
    }

    // Закрытие счёта
    public void CloseAccount()
    {
        if (CurrentState == AccountState.Closed)
        {
            Status = "Счет уже закрыт";
            return;
        }

        CurrentState = AccountState.Closed;
        Balance = 0;
        Status = "Счет закрыт";
    }

    // Событие уведомления UI о смене свойства
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
