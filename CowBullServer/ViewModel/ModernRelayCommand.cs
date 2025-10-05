using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CowBullServer.ViewModel
{
    /// <summary>
    /// Enhanced RelayCommand with support for async operations
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Predicate<T> _canExecute;
        private readonly Action<T> _execute;
        private readonly Func<T, Task> _executeAsync;
        private bool _isExecuting = false;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Func<T, Task> executeAsync, Predicate<T> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        #region ICommand Members

        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
                return false;
                
            return _canExecute == null || _canExecute((T)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();

                if (_executeAsync != null)
                {
                    await _executeAsync((T)parameter);
                }
                else
                {
                    _execute((T)parameter);
                }
            }
            catch (Exception ex)
            {
                // In a real application, handle exceptions appropriately
                // For now, we'll just log to console
                Console.WriteLine($"Command execution error: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion ICommand Members
    }

    /// <summary>
    /// Non-generic version of RelayCommand
    /// </summary>
    public class RelayCommand : RelayCommand<object>
    {
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
            : base(execute, canExecute)
        {
        }

        public RelayCommand(Func<object, Task> executeAsync, Predicate<object> canExecute = null)
            : base(executeAsync, canExecute)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : base(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
            : base(async _ => await executeAsync(), canExecute != null ? _ => canExecute() : null)
        {
        }
    }

    /// <summary>
    /// Async-specific RelayCommand for better async/await support
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting = false;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
                return false;
                
            return _canExecute?.Invoke() ?? true;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _executeAsync();
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately in a real application
                Console.WriteLine($"Async command execution error: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Generic async RelayCommand with parameter support
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _executeAsync;
        private readonly Predicate<T> _canExecute;
        private bool _isExecuting = false;

        public AsyncRelayCommand(Func<T, Task> executeAsync, Predicate<T> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
                return false;
                
            return _canExecute?.Invoke((T)parameter) ?? true;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _executeAsync((T)parameter);
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately in a real application
                Console.WriteLine($"Async command execution error: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}