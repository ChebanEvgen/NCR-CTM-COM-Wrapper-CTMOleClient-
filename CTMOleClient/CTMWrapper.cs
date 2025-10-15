using CTMOnCSharp;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CTMOleClient
{
    // ---------------------------------------------------------------------
    // Interface visible to COM clients (1C)
    // ---------------------------------------------------------------------
    [ComVisible(true)]
    [Guid("D36A29C9-0B48-4F39-BB51-8F3B738AA111")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ICTMWrapper
    {
        // --- Lifecycle management ---
        string Initialize(string clientId, string overrideHost = null, string overridePort = null);
        void Uninitialize();
        string Reinitialize(string clientId, string overrideHost = null, string overridePort = null); // Новый
        string GetLastError();

        // --- Configuration ---
        string GetConfig(string key); // Реализован

        // --- Callbacks ---
        void AddCallbacks(); // Ручное (автоматически в Initialize)

        // --- Transaction management ---
        bool BeginCustomerTransaction(string txnId);
        bool EndCustomerTransaction(string txnId);

        // --- Cash operations ---
        bool AcceptCash(int amount);
        bool StopAcceptingCash();
        bool DispenseCash(int amount);

        // --- Inventory queries ---
        string GetDispensableCashCounts();
        string GetNonDispensableCashCounts();

        // --- METHODS FOR POLLING STATE ---
        string GetLastDeviceError();
        string GetCurrentDeviceStatus();
        string GetLastCashAcceptInfo();
        bool IsCashAcceptComplete();

        // Additional for full polling
        string GetFullDeviceState();
        bool PollDeviceState();
    }

    // ---------------------------------------------------------------------
    // Implementation class - stores state in private fields for polling
    // ---------------------------------------------------------------------
    [ComVisible(true)]
    [Guid("5C6E18AF-3B0F-4639-90B0-B04D1B9FF999")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CTMWrapper : ICTMWrapper
    {
        // --- STORED STATE (updated by callbacks or polling) ---
        private CTMDeviceError? _lastDeviceError;
        private CTMDeviceStatus? _lastDeviceStatus;
        private CTMAcceptEvent? _lastCashAcceptEvent;
        private bool _isCashAcceptCompleteFlag = false;
        private string _currentTransactionId = string.Empty; // Additional transaction state
        private DateTime _lastPollTime = DateTime.MinValue; // Time of last poll
        private readonly object _lock = new object(); // Thread safety

        // Fields for callbacks (protection from GC)
        private CtmCClient.OnDeviceErrorCallBack _deviceErrorCallback;
        private CtmCClient.OnCashAcceptCallBack _cashAcceptCallback;
        private CtmCClient.OnCashAcceptCompleteCallBack _cashAcceptCompleteCallback;
        private CtmCClient.OnDeviceStatusCallBack _deviceStatusCallback;

        private string _lastError = string.Empty;

        // ------------------ Lifecycle methods ------------------
        public string GetLastError() => _lastError;

        public string Initialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            _lastError = ""; // Clear
            Utils utils = Utils.Instance;
            string serviceLocation = overrideHost ?? (utils.Properties.ContainsKey("rpc.host") ? utils.Properties["rpc.host"] : "localhost");
            string portNumber = overridePort ?? (utils.Properties.ContainsKey("rpc.port") ? utils.Properties["rpc.port"] : "3636");
            string serviceConnection = $"ctm://{serviceLocation}:{portNumber}";

            _lastError += $"Connecting to: {serviceConnection}\n"; // Log (remove after testing)

            var result = CtmCClient.Initialize(serviceConnection, clientId, CTMClientType.CTM_POS);

            if (result == CTMInitializationResult.CTM_INIT_SUCCESS)
            {
                AddCallbacks(); // Automatic callback registration
                _lastError = "OK";
                _lastPollTime = DateTime.UtcNow;
                return "OK";
            }
            _lastError = result.ToString();
            return _lastError;
        }

        public void Uninitialize()
        {
            lock (_lock)
            {
                // Clear state on shutdown
                _lastDeviceError = null;
                _lastDeviceStatus = null;
                _lastCashAcceptEvent = null;
                _isCashAcceptCompleteFlag = false;
                _currentTransactionId = string.Empty;
            }
            CtmCClient.Uninitialize();
        }

        public string Reinitialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            Uninitialize(); // Clear old
            return Initialize(clientId, overrideHost, overridePort); // New
        }

        // ------------------ Callbacks ------------------
        public void AddCallbacks()
        {
            _deviceErrorCallback = HandleDeviceError;
            _cashAcceptCallback = HandleCashAccept;
            _cashAcceptCompleteCallback = HandleCashAcceptComplete;
            _deviceStatusCallback = HandleDeviceStatus;

            CtmCClient.AddDeviceErrorEventHandler(_deviceErrorCallback);
            CtmCClient.AddCashAcceptEventHandler(_cashAcceptCallback);
            CtmCClient.AddCashAcceptCompleteEventHandler(_cashAcceptCompleteCallback);
            CtmCClient.AddDeviceStatusEventHandler(_deviceStatusCallback);
        }

        // ------------------ Transactions ------------------
        public bool BeginCustomerTransaction(string txnId)
        {
            var resultStruct = CtmCClient.BeginCustomerTransaction(txnId);
            var resultCode = resultStruct.error;
            int rawValue = (int)resultCode;
            _lastError = $"{resultCode} ({rawValue})";
            if (resultCode == CTMBeginTransactionError.CTM_BEGIN_TRX_SUCCESS)
            {
                lock (_lock) { _currentTransactionId = txnId; }
                return true;
            }
            return false;
        }

        public bool EndCustomerTransaction(string txnId)
        {
            var result = CtmCClient.EndTransaction(txnId);
            _lastError = result.ToString();
            if (result == CTMEndTransactionResult.CTM_END_TRX_SUCCESS)
            {
                lock (_lock) { _currentTransactionId = string.Empty; }
                return true;
            }
            return false;
        }

        // ------------------ Cash operations ------------------
        public bool AcceptCash(int amount)
        {
            var result = CtmCClient.AcceptCash(amount);
            _lastError = result.ToString();
            return result == CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_SUCCESS;
        }

        public bool StopAcceptingCash()
        {
            var result = CtmCClient.StopAcceptingCash();
            _lastError = result.ToString();
            return result == CTMStopAcceptingCashResult.CTM_STOP_ACCEPTING_CASH_SUCCESS;
        }

        public bool DispenseCash(int amount)
        {
            CTMDispenseCashResult result = CtmCClient.DispenseCash(amount);

            string details = result.cashUnitSet.count != 0
                ? $"\n Actual Amount Dispensed: {result.amountDispensed}\n See table below for dispensed cashlist."
                : $"\n Actual Amount Dispensed: {result.amountDispensed}";

            _lastError = $"Dispense Cash Result: {result.error}\n Amount to Dispense: {amount}{details}";
            return result.error == 0; // Success if error == 0
        }

        // ------------------ Configuration ------------------
        public string GetConfig(string key)
        {
            try
            {
                CTMGetConfigResult configResult = CtmCClient.GetConfig(key);
                var config = configResult.config;
                string value = string.Empty;
                IntPtr ptr = config.intPtr;
                int size = Marshal.SizeOf(typeof(CTMConfigurationKeyValue));
                for (int i = 0; i < config.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMConfigurationKeyValue kv = (CTMConfigurationKeyValue)Marshal.PtrToStructure(itemPtr, typeof(CTMConfigurationKeyValue));
                    if (kv.key == key)
                    {
                        value = kv.value;
                        break;
                    }
                }
                _lastError = "OK";
                return value;
            }
            catch (Exception ex)
            {
                _lastError = $"Error for key '{key}': {ex.Message}";
                return string.Empty;
            }
        }

        // ------------------ Inventory queries ------------------
        public string GetDispensableCashCounts()
        {
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetDispensableCashCounts();
                if (countsResult.error != 0)
                {
                    _lastError = $"Error: {countsResult.error}";
                    return string.Empty;
                }

                var cashUnitSet = countsResult.cashUnitSet;
                var sb = new StringBuilder("Dispensable Cash Counts:\n");
                IntPtr ptr = cashUnitSet.intPtr;
                int size = Marshal.SizeOf(typeof(CTMCashUnit));
                for (int i = 0; i < cashUnitSet.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                    sb.AppendLine($"Denomination: {unit.denomination}, Count: {unit.count}, Type: {unit.type}, Currency: {unit.currencyCode}");
                }
                _lastError = "OK";
                lock (_lock) { _lastPollTime = DateTime.UtcNow; }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _lastError = $"Error: {ex.Message}";
                return string.Empty;
            }
        }

        public string GetNonDispensableCashCounts()
        {
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetNonDispensableCashCounts();
                if (countsResult.error != 0)
                {
                    _lastError = $"Error: {countsResult.error}";
                    return string.Empty;
                }

                var cashUnitSet = countsResult.cashUnitSet;
                var sb = new StringBuilder("Non-Dispensable Cash Counts:\n");
                IntPtr ptr = cashUnitSet.intPtr;
                int size = Marshal.SizeOf(typeof(CTMCashUnit));
                for (int i = 0; i < cashUnitSet.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                    sb.AppendLine($"Denomination: {unit.denomination}, Count: {unit.count}, Type: {unit.type}, Currency: {unit.currencyCode}");
                }
                _lastError = "OK";
                lock (_lock) { _lastPollTime = DateTime.UtcNow; }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _lastError = $"Error: {ex.Message}";
                return string.Empty;
            }
        }

        // ------------------ POLLING METHODS (state polling) ------------------
        // These methods allow 1C to periodically call for updates
        public string GetLastDeviceError()
        {
            lock (_lock)
            {
                if (_lastDeviceError.HasValue)
                {
                    var error = _lastDeviceError.Value;
                    string errorInfo = $"Ошибка: Model={error.deviceInfo.deviceModel}, Code={error.resultCode}, Extended={error.extendedResultCode}";
                    _lastDeviceError = null; // Clear after reading
                    _lastPollTime = DateTime.UtcNow;
                    return errorInfo;
                }
                return "No errors";
            }
        }

        public string GetCurrentDeviceStatus()
        {
            lock (_lock)
            {
                if (_lastDeviceStatus.HasValue)
                {
                    var status = _lastDeviceStatus.Value;
                    string statusInfo = $"Статус: Model={status.deviceInfo.deviceModel}, State={status.status}";
                    _lastPollTime = DateTime.UtcNow;
                    return statusInfo;
                }
                return "Status unknown";
            }
        }

        public string GetLastCashAcceptInfo()
        {
            lock (_lock)
            {
                if (_lastCashAcceptEvent.HasValue)
                {
                    var evt = _lastCashAcceptEvent.Value;
                    string info = $"Принято: {evt.cashUnit.denomination}, Сумма в txn: {evt.amount}";
                    _lastCashAcceptEvent = null; // Clear
                    _lastPollTime = DateTime.UtcNow;
                    return info;
                }
                return "No accept events";
            }
        }

        public bool IsCashAcceptComplete()
        {
            lock (_lock)
            {
                if (_isCashAcceptCompleteFlag)
                {
                    _isCashAcceptCompleteFlag = false;
                    _lastPollTime = DateTime.UtcNow;
                    return true;
                }
                return false;
            }
        }

        // New method: Full device state for a single call from 1C
        public string GetFullDeviceState()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Full device state ===");
                sb.AppendLine(GetCurrentDeviceStatus());
                sb.AppendLine(GetLastDeviceError());
                sb.AppendLine(GetLastCashAcceptInfo());
                sb.AppendLine($"Accept complete: {IsCashAcceptComplete()}");
                sb.AppendLine($"Transaction: {_currentTransactionId}");
                sb.AppendLine($"Last poll: {_lastPollTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("=== Inventory ===");
                sb.Append(GetDispensableCashCounts());
                sb.Append(GetNonDispensableCashCounts());
                _lastPollTime = DateTime.UtcNow;
                return sb.ToString();
            }
        }

        // New method: Forced polling
        public bool PollDeviceState()
        {
            try
            {
                lock (_lock) { _lastPollTime = DateTime.UtcNow; }
                _lastError = "Polling OK";
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"Polling error: {ex.Message}";
                return false;
            }
        }

        // ------------------ Callback handlers (update stored state) ------------------
        private void HandleDeviceError(CTMEventInfo evtInfo, CTMDeviceError deviceError)
        {
            lock (_lock) { _lastDeviceError = deviceError; }
        }

        private void HandleCashAccept(CTMEventInfo evtInfo, CTMAcceptEvent acceptEvent)
        {
            lock (_lock) { _lastCashAcceptEvent = acceptEvent; }
        }

        private void HandleCashAcceptComplete(CTMEventInfo evtInfo)
        {
            lock (_lock) { _isCashAcceptCompleteFlag = true; }
        }

        private void HandleDeviceStatus(CTMEventInfo evtInfo, CTMDeviceStatus deviceStatus)
        {
            lock (_lock) { _lastDeviceStatus = deviceStatus; }
        }
    }
}