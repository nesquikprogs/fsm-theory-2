using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
namespace ATMMachine
{
    public partial class MainWindow : Window
    {
        private ATMContext atmContext;
        private ObservableCollection<string> statesHistory = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();

            // Привязка истории к UI
            StatesHistoryList.ItemsSource = statesHistory;

            // Инициализация UI вручную
            CurrentStateText.Text = "Простаивает";
            StatusText.Text = "Простаивает";
            MessageText.Text = "Вставьте карту для начала работы";
            DetailsText.Text = "Добро пожаловать в банкомат";

            AddStateToHistory("Простаивает");

            // Запуск автомата
            atmContext = new ATMContext(this);
        }

        // Добавление в историю (потокобезопасно)
        public void AddStateToHistory(string stateName)
        {
            Dispatcher.Invoke(() =>
            {
                statesHistory.Add(stateName);
            });
        }

        // Обновление UI из состояний
        public void UpdateDisplay(string message, string details = "",
                               bool showCardInsert = false,
                               bool showPinEntry = false,
                               bool showTransactionSelection = false,
                               bool showAmountEntry = false,
                               bool showCardEject = false,
                               bool showContinue = false,
                               bool showPrintReceipt = false)
        {
            Dispatcher.Invoke(() =>
            {
                // Обновляем текущее состояние
                if (atmContext != null && !string.IsNullOrEmpty(atmContext.CurrentStateName))
                {
                    CurrentStateText.Text = atmContext.CurrentStateName;
                    StatusText.Text = atmContext.CurrentStateName;

                    if (statesHistory.Count == 0 || statesHistory[statesHistory.Count - 1] != atmContext.CurrentStateName)
                    {
                        AddStateToHistory(atmContext.CurrentStateName);
                    }
                }

                MessageText.Text = message;
                DetailsText.Text = details;

                // Управление видимостью кнопок
                InsertCardBtn.IsEnabled = showCardInsert;
                PinGroup.Visibility = showPinEntry ? Visibility.Visible : Visibility.Collapsed;
                TransactionGroup.Visibility = showTransactionSelection ? Visibility.Visible : Visibility.Collapsed;
                AmountGroup.Visibility = showAmountEntry ? Visibility.Visible : Visibility.Collapsed;
                EjectCardBtn.IsEnabled = showCardEject;
                ContinueBtn.Visibility = showContinue ? Visibility.Visible : Visibility.Collapsed;
                PrintReceiptBtn.Visibility = showPrintReceipt ? Visibility.Visible : Visibility.Collapsed;

                // Очистка полей
                if (!showPinEntry) PinBox.Clear();
                if (!showAmountEntry) AmountBox.Text = "1000";
            });
        }

        // Обработчики кнопок 

        private void InsertCardBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleCardInserted();
        }

        private void EnterPinBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PinBox.Password))
            {
                atmContext?.HandlePinEntered(PinBox.Password);
            }
            else
            {
                MessageText.Text = "Введите ПИН-код";
            }
        }

        private void WithdrawBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleWithdrawalSelected();
        }

        private void BalanceBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleBalanceSelected();
        }

        private void TransferBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleTransferSelected();
        }

        private void ConfirmAmountBtn_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(AmountBox.Text, out decimal amount))
            {
                atmContext?.HandleAmountConfirmed(amount);
            }
            else
            {
                MessageText.Text = "Неверный формат суммы";
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleCancel();
        }

        private void ContinueBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleContinue();
        }

        private void EjectCardBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleCardEjected();
        }

        private void PrintReceiptBtn_Click(object sender, RoutedEventArgs e)
        {
            atmContext?.HandleReceiptPrinted();
        }
    }
}