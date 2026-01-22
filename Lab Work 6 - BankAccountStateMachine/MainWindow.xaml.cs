using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BankAccountStateMachine
{
    public partial class MainWindow : Window
    {
        private BankAccount account;

        public MainWindow()
        {
            InitializeComponent();

            // Создаём объект банковского счёта и обновляем интерфейс
            account = new BankAccount();
            UpdateUI();
        }

        // Метод обновления интерфейса в зависимости от состояния счёта
        private void UpdateUI()
        {
            CurrentStateText.Text = account.CurrentState.ToString();
            BalanceText.Text = $"Баланс: {account.Balance} руб.";

            // Управляем доступностью кнопок
            OpenAccountBtn.IsEnabled = account.CurrentState == AccountState.Closed;
            DepositBtn.IsEnabled = account.CurrentState != AccountState.Closed;
            WithdrawBtn.IsEnabled = account.CurrentState == AccountState.Good;
            RepayDebtBtn.IsEnabled = account.CurrentState == AccountState.Overdraft;
            CloseAccountBtn.IsEnabled = account.CanCloseAccount();

            // Цвет кнопки закрытия счёта меняется в зависимости от возможности закрыть
            CloseAccountBtn.Background = CloseAccountBtn.IsEnabled
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.LightGray;

            // Обновляем историю операций
            HistoryListBox.ItemsSource = null;
            HistoryListBox.ItemsSource = account.GetOperationHistory();

            // Автопрокрутка к последней операции
            if (HistoryListBox.Items.Count > 0)
                HistoryListBox.ScrollIntoView(HistoryListBox.Items[HistoryListBox.Items.Count - 1]);
        }

        // Открытие счёта
        private void OpenAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            account.OpenAccount();
            UpdateUI();
        }

        // Внесение средств
        private void DepositBtn_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(DepositAmountBox.Text, out decimal amount) && amount > 0)
                account.MakeDeposit(amount);
            else
                MessageBox.Show("Введите корректную сумму вклада!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

            UpdateUI();
        }

        // Снятие средств
        private void WithdrawBtn_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(WithdrawAmountBox.Text, out decimal amount) && amount > 0)
                account.WithdrawMoney(amount);
            else
                MessageBox.Show("Введите корректную сумму снятия!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

            UpdateUI();
        }

        // Погашение задолженности
        private void RepayDebtBtn_Click(object sender, RoutedEventArgs e)
        {
            account.RepayDebt();
            UpdateUI();
        }

        // Закрытие счёта
        private void CloseAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите закрыть счет?",
                                         "Закрытие счета",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                account.CloseAccount();
                UpdateUI();
            }
        }

        // Включение или отключение возможности овердрафта
        private void AllowOverdraftCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            account.AllowOverdraft = AllowOverdraftCheckBox.IsChecked == true;
            UpdateUI();
        }
    }

    // Состояния банковского счёта
    public enum AccountState
    {
        Closed,    // Счёт закрыт, начальное и конечное состояние
        Opened,    // Счёт открыт, можно вносить деньги
        Good,      // Счёт положительный, можно снимать деньги
        Overdraft  // Счёт ушёл в минус, требуется погашение долга
    }

    // Класс банковского счёта с историей операций и логикой перехода состояний
    public class BankAccount
    {
        public AccountState CurrentState { get; private set; }
        public decimal Balance { get; private set; }
        public bool AllowOverdraft { get; set; }

        private List<string> operationHistory;
        private const decimal OverdraftLimit = 1000;

        public BankAccount()
        {
            CurrentState = AccountState.Closed;
            Balance = 0;
            AllowOverdraft = false;
            operationHistory = new List<string>();
            AddToHistory("Счет создан в состоянии: Закрыт");
        }

        // Метод открытия счёта
        public void OpenAccount()
        {
            if (CurrentState == AccountState.Closed)
            {
                CurrentState = AccountState.Opened;
                AddToHistory("Открытие счета");
                AddToHistory("Переход в состояние: Счет открыт");
            }
            else
                AddToHistory("Невозможно открыть счет: уже открыт");
        }

        // Метод внесения средств на счёт
        public void MakeDeposit(decimal amount)
        {
            switch (CurrentState)
            {
                case AccountState.Opened:
                    Balance += amount;
                    CurrentState = AccountState.Good;
                    AddToHistory($"Вклад на {amount} руб. Баланс: {Balance} руб.");
                    AddToHistory("Переход в состояние: Счет хороший");
                    break;

                case AccountState.Good:
                case AccountState.Overdraft:
                    Balance += amount;
                    AddToHistory($"Вклад на {amount} руб. Баланс: {Balance} руб.");
                    if (Balance >= 0 && CurrentState == AccountState.Overdraft)
                    {
                        CurrentState = AccountState.Good;
                        AddToHistory("Переход в состояние: Счет хороший");
                    }
                    break;

                default:
                    AddToHistory("Невозможно внести средства: счет закрыт");
                    break;
            }
        }

        // Метод снятия средств
        public void WithdrawMoney(decimal amount)
        {
            if (CurrentState != AccountState.Good)
            {
                AddToHistory("Снятие невозможно в текущем состоянии");
                return;
            }

            if (Balance >= amount)
            {
                Balance -= amount;
                AddToHistory($"Снятие {amount} руб. Баланс: {Balance} руб.");
            }
            else if (AllowOverdraft && Balance - amount >= -OverdraftLimit)
            {
                Balance -= amount;
                AddToHistory($"Снятие {amount} руб. Баланс: {Balance} руб.");
                if (Balance < 0)
                {
                    CurrentState = AccountState.Overdraft;
                    AddToHistory("Переход в состояние: Превышены расходы по счету");
                }
            }
            else
                AddToHistory("Недостаточно средств для снятия");
        }

        // Метод погашения долга
        public void RepayDebt()
        {
            if (CurrentState == AccountState.Overdraft)
            {
                decimal repayment = Math.Abs(Balance) + 100;
                Balance += repayment;
                CurrentState = AccountState.Good;
                AddToHistory($"Погашение долга. Внесено: {repayment} руб.");
                AddToHistory($"Баланс после погашения: {Balance} руб.");
                AddToHistory("Переход в состояние: Счет хороший");
            }
            else
                AddToHistory("Нет задолженности для погашения");
        }

        // Метод закрытия счёта
        public void CloseAccount()
        {
            if (CanCloseAccount())
            {
                if (Balance > 0)
                {
                    AddToHistory($"Вывод остатка: {Balance} руб.");
                    Balance = 0;
                }
                CurrentState = AccountState.Closed;
                AddToHistory("Счет закрыт");
            }
            else
                AddToHistory("Невозможно закрыть счет: есть долг");
        }

        // Проверка возможности закрытия счёта
        public bool CanCloseAccount() => (CurrentState == AccountState.Good) && Balance >= 0;

        // Получение истории операций
        public List<string> GetOperationHistory() => operationHistory;

        // Добавление записи в историю
        private void AddToHistory(string operation) => operationHistory.Add(operation);
    }
}
