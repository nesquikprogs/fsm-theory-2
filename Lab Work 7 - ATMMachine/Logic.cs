using System;
using System.Windows.Threading;
namespace ATMMachine
{
    // Базовый класс состояния автомата 
    public abstract class ATMState
    {
        protected ATMContext context;
        protected string stateName;

        public ATMState(ATMContext context, string stateName)
        {
            this.context = context;
            this.stateName = stateName;
        }

        // Вызывается при входе в состояние
        public virtual void Enter()
        {
            context?.LogStateChange(stateName); // Логируем переход
        }

        // Обработчики событий — переопределяются в наследниках
        public virtual void HandleCardInserted() { }
        public virtual void HandlePinEntered(string pin) { }
        public virtual void HandleWithdrawalSelected() { }
        public virtual void HandleBalanceSelected() { }
        public virtual void HandleTransferSelected() { }
        public virtual void HandleAmountConfirmed(decimal amount) { }
        public virtual void HandleCancel() { }
        public virtual void HandleContinue() { }
        public virtual void HandleCardEjected() { }
        public virtual void HandleTimeout() { }
        public virtual void HandleReceiptPrinted() { }

        // Переход в новое состояние
        protected void ChangeState(ATMState newState)
        {
            context?.ChangeState(newState);
        }

        public string StateName => stateName;
    }

    // Контекст 
    public class ATMContext
    {
        private ATMState currentState;
        public MainWindow MainWindow { get; private set; }
        public Dispatcher Dispatcher => MainWindow?.Dispatcher;

        public ATMContext(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            currentState = new IdleState(this); // Начальное состояние
            currentState.Enter();
        }

        // Смена состояния
        public void ChangeState(ATMState newState)
        {
            currentState = newState;
            currentState?.Enter();
        }

        // Логирование в историю
        public void LogStateChange(string stateName)
        {
            MainWindow?.AddStateToHistory(stateName);
        }

        // Обновление интерфейса
        public void UpdateUI(string message, string details = "",
                           bool showCardInsert = false,
                           bool showPinEntry = false,
                           bool showTransactionSelection = false,
                           bool showAmountEntry = false,
                           bool showCardEject = false,
                           bool showContinue = false,
                           bool showPrintReceipt = false)
        {
            MainWindow?.UpdateDisplay(message, details, showCardInsert, showPinEntry,
                                   showTransactionSelection, showAmountEntry, showCardEject,
                                   showContinue, showPrintReceipt);
        }

        // Делегирование событий текущему состоянию
        public void HandleCardInserted() => currentState?.HandleCardInserted();
        public void HandlePinEntered(string pin) => currentState?.HandlePinEntered(pin);
        public void HandleWithdrawalSelected() => currentState?.HandleWithdrawalSelected();
        public void HandleBalanceSelected() => currentState?.HandleBalanceSelected();
        public void HandleTransferSelected() => currentState?.HandleTransferSelected();
        public void HandleAmountConfirmed(decimal amount) => currentState?.HandleAmountConfirmed(amount);
        public void HandleCancel() => currentState?.HandleCancel();
        public void HandleContinue() => currentState?.HandleContinue();
        public void HandleCardEjected() => currentState?.HandleCardEjected();
        public void HandleTimeout() => currentState?.HandleTimeout();
        public void HandleReceiptPrinted() => currentState?.HandleReceiptPrinted();

        public string CurrentStateName => currentState?.StateName ?? "Неизвестно";
    }

    // Состояние: банкомат простаивает
    public class IdleState : ATMState
    {
        public IdleState(ATMContext context) : base(context, "Простаивает") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Вставьте карту для начала работы",
                           "Добро пожаловать в банкомат",
                           showCardInsert: true);
        }

        public override void HandleCardInserted()
        {
            ChangeState(new WaitingForPINState(context));
        }
    }

    // Надсостояние: отмена доступна
    public abstract class CustomerInputState : ATMState
    {
        public CustomerInputState(ATMContext context, string stateName) : base(context, stateName) { }

        public override void HandleCancel()
        {
            ChangeState(new CardEjectionState(context));
        }
    }

    // Ожидание ПИН-кода
    public class WaitingForPINState : CustomerInputState
    {
        private int pinAttempts = 0;
        private const int MAX_PIN_ATTEMPTS = 3;

        public WaitingForPINState(ATMContext context) : base(context, "Ожидание ПИН-кода") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Введите ПИН-код",
                           "ПИН-код должен состоять из 4 цифр",
                           showPinEntry: true);
        }

        public override void HandlePinEntered(string pin)
        {
            pinAttempts++;
            if (pin == "1234")
            {
                ChangeState(new WaitingForTransactionState(context));
            }
            else
            {
                if (pinAttempts >= MAX_PIN_ATTEMPTS)
                {
                    ChangeState(new ConfiscationState(context));
                }
                else
                {
                    context.UpdateUI($"Неверный ПИН-код",
                                   $"Осталось попыток: {MAX_PIN_ATTEMPTS - pinAttempts}",
                                   showPinEntry: true);
                }
            }
        }
    }

    // Ожидание выбора операции
    public class WaitingForTransactionState : CustomerInputState
    {
        public WaitingForTransactionState(ATMContext context) : base(context, "Ожидание Выбора Клиента") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Выберите операцию",
                           "Доступные операции: снятие наличных, запрос баланса, перевод",
                           showTransactionSelection: true);
        }

        public override void HandleWithdrawalSelected()
        {
            ChangeState(new AmountEntryState(context));
        }

        public override void HandleBalanceSelected()
        {
            ChangeState(new BalanceInquiryState(context));
        }

        public override void HandleTransferSelected()
        {
            ChangeState(new TransferState(context));
        }
    }

    // Ввод суммы
    public class AmountEntryState : CustomerInputState
    {
        public AmountEntryState(ATMContext context) : base(context, "Ввод суммы") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Введите сумму для снятия",
                           "Минимальная сумма: 100 руб, Максимальная: 10 000 руб",
                           showAmountEntry: true);
        }

        public override void HandleAmountConfirmed(decimal amount)
        {
            if (amount >= 100 && amount <= 10000)
            {
                ChangeState(new CashDispensingState(context, amount));
            }
            else
            {
                context.UpdateUI("Неверная сумма",
                               "Введите сумму от 100 до 10 000 рублей",
                               showAmountEntry: true);
            }
        }
    }

    // Имитация выдачи наличных
    public class CashDispensingState : ATMState
    {
        private decimal amount;

        public CashDispensingState(ATMContext context, decimal amount) : base(context, "Выдача наличных")
        {
            this.amount = amount;
        }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI($"Выдача {amount} рублей...",
                           "Пожалуйста, подождите");

            System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
            {
                context.Dispatcher.Invoke(() =>
                {
                    ChangeState(new ReceiptPrintingState(context, "снятие наличных", amount));
                });
            });
        }
    }

    // Печать чека
    public class ReceiptPrintingState : ATMState
    {
        private string operationType;
        private decimal amount;

        public ReceiptPrintingState(ATMContext context, string operationType, decimal amount = 0)
            : base(context, "Печать чека")
        {
            this.operationType = operationType;
            this.amount = amount;
        }

        public override void Enter()
        {
            base.Enter();
            string receiptMessage = "";
            string receiptDetails = "";
            switch (operationType)
            {
                case "снятие наличных":
                    receiptMessage = "Печать чека для операции снятия наличных";
                    receiptDetails = $"Сумма: {amount} руб.\nДата: {DateTime.Now:dd.MM.yyyy HH:mm}\nОстаток на счете: {new Random().Next(1000, 50000)} руб.";
                    break;
                case "перевод средств":
                    receiptMessage = "Печать чека для операции перевода";
                    receiptDetails = $"Перевод выполнен успешно\nДата: {DateTime.Now:dd.MM.yyyy HH:mm}\nСредства будут зачислены в течение 1 рабочего дня";
                    break;
                case "запрос баланса":
                    receiptMessage = "Печать чека с информацией о балансе";
                    receiptDetails = $"Баланс: {new Random().Next(1000, 50000)} руб.\nДата: {DateTime.Now:dd.MM.yyyy HH:mm}";
                    break;
            }
            context.UpdateUI(receiptMessage,
                           receiptDetails,
                           showPrintReceipt: true);
        }

        public override void HandleReceiptPrinted()
        {
            ChangeState(new CardEjectionState(context));
        }

        public override void HandleCancel()
        {
            ChangeState(new CardEjectionState(context));
        }
    }

    // Возврат карты
    public class CardEjectionState : ATMState
    {
        public CardEjectionState(ATMContext context) : base(context, "Возврат карты") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Заберите карту",
                           "Нажмите кнопку 'Вернуть карту'",
                           showCardEject: true);
        }

        public override void HandleCardEjected()
        {
            ChangeState(new CompletionState(context));
        }
    }

    // Завершение
    public class CompletionState : ATMState
    {
        public CompletionState(ATMContext context) : base(context, "Завершение") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Операция завершена",
                           "Спасибо за использование нашего банкомата!",
                           showContinue: true);
        }

        public override void HandleContinue()
        {
            ChangeState(new IdleState(context));
        }
    }

    // Конфискация
    public class ConfiscationState : ATMState
    {
        public ConfiscationState(ATMContext context) : base(context, "Конфискация") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Карта конфискована",
                           "Превышено количество неверных попыток ввода ПИН-кода. Обратитесь в отделение банка.",
                           showContinue: true);
        }

        public override void HandleContinue()
        {
            ChangeState(new IdleState(context));
        }
    }

    // Запрос баланса
    public class BalanceInquiryState : CustomerInputState
    {
        public BalanceInquiryState(ATMContext context) : base(context, "Запрос баланса") { }

        public override void Enter()
        {
            base.Enter();
            var balance = new Random().Next(1000, 50000);
            context.UpdateUI($"Ваш баланс: {balance} рублей",
                           $"Доступно для снятия: {balance} рублей\nДата: {DateTime.Now:dd.MM.yyyy}");
        }
    }

    // Перевод
    public class TransferState : CustomerInputState
    {
        public TransferState(ATMContext context) : base(context, "Перевод средств") { }

        public override void Enter()
        {
            base.Enter();
            context.UpdateUI("Перевод выполнен успешно",
                           "Средства будут зачислены в течение 1 рабочего дня");

            System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
            {
                context.Dispatcher.Invoke(() =>
                {
                    ChangeState(new ReceiptPrintingState(context, "перевод средств"));
                });
            });
        }
    }
}