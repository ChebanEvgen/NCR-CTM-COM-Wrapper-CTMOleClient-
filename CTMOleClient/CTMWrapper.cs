using CTMOnCSharp;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.CompilerServices;
using System.IO;

namespace CTMOleClient
{


    // ---------------------------------------------------------------------
    // Event interface for COM events (visible to 1C clients)
    // ---------------------------------------------------------------------
    [ComVisible(true)]
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")] // New GUID for events
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface ICTMWrapperEvents
    {
        [DispId(1)]
        void OnDeviceError([In] string errorInfo);

        [DispId(2)]
        void OnCashAccept([In] string acceptInfo);

        [DispId(3)]
        void OnCashAcceptComplete();

        [DispId(4)]
        void OnDeviceStatus([In] string statusInfo);
    }

    // ---------------------------------------------------------------------
    // Interface visible to COM clients (1C) - updated to remove polling, add event subscription
    // ---------------------------------------------------------------------
    [ComVisible(true)]
    [Guid("D36A29C9-0B48-4F39-BB51-8F3B738AA111")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ICTMWrapper
    {
        // --- Lifecycle management ---
        string Initialize(string clientId, string overrideHost = null, string overridePort = null);
        void Uninitialize();
        string Reinitialize(string clientId, string overrideHost = null, string overridePort = null);
        string GetLastError();

        // --- Configuration ---
        string GetConfig(string key);

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

        // --- Event management (new: for 1C to subscribe to events) ---
        void AdviseEvents(); // Enable event firing (connect)
        void UnadviseEvents(); // Disable event firing (disconnect)
    }

    // ---------------------------------------------------------------------
    // Implementation class - now event-driven instead of polling
    // ---------------------------------------------------------------------
    [ComVisible(true)]
    [Guid("5C6E18AF-3B0F-4639-90B0-B04D1B9FF999")]
    [ProgId("CTMOleClient.CTMWrapper")]  // ProgID for 1C AddIn creation
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(ICTMWrapperEvents))] // Expose events via COM connection points
    public class CTMWrapper : ICTMWrapper
    {
        // --- EVENTS (C# events mapped to COM events) ---
        public event Action<string> OnDeviceErrorEvent;
        public event Action<string> OnCashAcceptEvent;
        public event Action OnCashAcceptCompleteEvent;
        public event Action<string> OnDeviceStatusEvent;

        // Fields for callbacks (protection from GC)
        private CtmCClient.OnDeviceErrorCallBack _deviceErrorCallback;
        private CtmCClient.OnCashAcceptCallBack _cashAcceptCallback;
        private CtmCClient.OnCashAcceptCompleteCallBack _cashAcceptCompleteCallback;
        private CtmCClient.OnDeviceStatusCallBack _deviceStatusCallback;

        private string _lastError = string.Empty;
        private string _currentTransactionId = string.Empty;
        private bool _eventsEnabled = false; // Flag to control event firing

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
                _currentTransactionId = string.Empty;
                LogToFile($"[{DateTime.Now}] Initialize: SUCCESS. Events enabled: {_eventsEnabled}. Subscribers: DeviceError={OnDeviceErrorEvent?.GetInvocationList().Length ?? 0}, CashAccept={OnCashAcceptEvent?.GetInvocationList().Length ?? 0}, etc.");
                return "OK";
            }
            _lastError = result.ToString();
            LogToFile($"[{DateTime.Now}] Initialize: FAILED ({result}). Events enabled: {_eventsEnabled}.");
            return _lastError;
        }

        public void Uninitialize()
        {
            UnadviseEvents(); // Disable events
            CtmCClient.Uninitialize();
            _currentTransactionId = string.Empty;
            _lastError = "Uninitialized";
            LogToFile($"[{DateTime.Now}] Uninitialize: Called. Events enabled: {_eventsEnabled}.");
        }

        public string Reinitialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            Uninitialize(); // Clear old
            return Initialize(clientId, overrideHost, overridePort); // New
        }

        // ------------------ Event management ------------------
        public void AdviseEvents()
        {
            _eventsEnabled = true;
            _lastError = "Events enabled";
            LogToFile($"[{DateTime.Now}] AdviseEvents: Called. Events now ENABLED. Current subscribers: DeviceError={OnDeviceErrorEvent?.GetInvocationList().Length ?? 0}, CashAccept={OnCashAcceptEvent?.GetInvocationList().Length ?? 0}, Complete={OnCashAcceptCompleteEvent?.GetInvocationList().Length ?? 0}, Status={OnDeviceStatusEvent?.GetInvocationList().Length ?? 0}.");
        }

        public void UnadviseEvents()
        {
            _eventsEnabled = false;
            _lastError = "Events disabled";
            LogToFile($"[{DateTime.Now}] UnadviseEvents: Called. Events now DISABLED. Current subscribers: DeviceError={OnDeviceErrorEvent?.GetInvocationList().Length ?? 0}, etc.");
        }

        // ------------------ Callbacks (now fire COM events) ------------------
        private void AddCallbacks()
        {
            _deviceErrorCallback = HandleDeviceError;
            _cashAcceptCallback = HandleCashAccept;
            _cashAcceptCompleteCallback = HandleCashAcceptComplete;
            _deviceStatusCallback = HandleDeviceStatus;

            CtmCClient.AddDeviceErrorEventHandler(_deviceErrorCallback);
            CtmCClient.AddCashAcceptEventHandler(_cashAcceptCallback);
            CtmCClient.AddCashAcceptCompleteEventHandler(_cashAcceptCompleteCallback);
            CtmCClient.AddDeviceStatusEventHandler(_deviceStatusCallback);
            LogToFile($"[{DateTime.Now}] AddCallbacks: Native handlers registered. Events enabled: {_eventsEnabled}.");
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
                _currentTransactionId = txnId;
                LogToFile($"[{DateTime.Now}] BeginCustomerTransaction: SUCCESS for txnId={txnId}.");
                return true;
            }
            LogToFile($"[{DateTime.Now}] BeginCustomerTransaction: FAILED ({resultCode}) for txnId={txnId}.");
            return false;
        }

        public bool EndCustomerTransaction(string txnId)
        {
            var result = CtmCClient.EndTransaction(txnId);
            _lastError = result.ToString();
            if (result == CTMEndTransactionResult.CTM_END_TRX_SUCCESS)
            {
                _currentTransactionId = string.Empty;
                LogToFile($"[{DateTime.Now}] EndCustomerTransaction: SUCCESS for txnId={txnId}.");
                return true;
            }
            LogToFile($"[{DateTime.Now}] EndCustomerTransaction: FAILED ({result}) for txnId={txnId}.");
            return false;
        }

        // ------------------ Cash operations ------------------
        public bool AcceptCash(int amount)
        {
            var result = CtmCClient.AcceptCash(amount);
            _lastError = result.ToString();
            LogToFile($"[{DateTime.Now}] AcceptCash: {result} for amount={amount}.");
            return result == CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_SUCCESS;
        }

        public bool StopAcceptingCash()
        {
            var result = CtmCClient.StopAcceptingCash();
            _lastError = result.ToString();
            LogToFile($"[{DateTime.Now}] StopAcceptingCash: {result}.");
            return result == CTMStopAcceptingCashResult.CTM_STOP_ACCEPTING_CASH_SUCCESS;
        }

        public bool DispenseCash(int amount)
        {
            CTMDispenseCashResult result = CtmCClient.DispenseCash(amount);

            string details = result.cashUnitSet.count != 0
                ? $"\n Actual Amount Dispensed: {result.amountDispensed}\n See table below for dispensed cashlist."
                : "";
            _lastError = $"Dispense: {result.error}{details}";
            LogToFile($"[{DateTime.Now}] DispenseCash: {result.error} for amount={amount}. Details: {details}");
            return result.error == CTMDispenseCashError.CTM_DISPENSE_CASH_SUCCESS;
        }

        // ------------------ Configuration ------------------
        public string GetConfig(string key)
        {
            try
            {
                var configResult = CtmCClient.GetConfig();
                if (configResult.config.count == 0)
                {
                    _lastError = "No config available";
                    LogToFile($"[{DateTime.Now}] GetConfig: No config for key={key}.");
                    return string.Empty;
                }

                string value = string.Empty;
                IntPtr ptr = configResult.config.intPtr;
                int size = Marshal.SizeOf(typeof(CTMConfigurationKeyValue));
                for (int i = 0; i < configResult.config.count; i++)
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
                LogToFile($"[{DateTime.Now}] GetConfig: SUCCESS for key={key}, value={value}.");
                return value;
            }
            catch (Exception ex)
            {
                _lastError = $"Error for key '{key}': {ex.Message}";
                LogToFile($"[{DateTime.Now}] GetConfig: EXCEPTION for key={key}: {ex.Message}.");
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
                    LogToFile($"[{DateTime.Now}] GetDispensableCashCounts: FAILED ({countsResult.error}).");
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
                LogToFile($"[{DateTime.Now}] GetDispensableCashCounts: SUCCESS. Counts: {sb.ToString()}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _lastError = $"Error: {ex.Message}";
                LogToFile($"[{DateTime.Now}] GetDispensableCashCounts: EXCEPTION {ex.Message}.");
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
                    LogToFile($"[{DateTime.Now}] GetNonDispensableCashCounts: FAILED ({countsResult.error}).");
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
                LogToFile($"[{DateTime.Now}] GetNonDispensableCashCounts: SUCCESS. Counts: {sb.ToString()}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _lastError = $"Error: {ex.Message}";
                LogToFile($"[{DateTime.Now}] GetNonDispensableCashCounts: EXCEPTION {ex.Message}.");
                return string.Empty;
            }
        }

        // Helper method for consistent logging
        private void LogToFile(string message)
        {
            try
            {
                string logPath = @"C:\Temp\CTMLogs.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] CTMWrapper OLE: {message}{Environment.NewLine}");
            }
            catch (Exception logEx)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"LOG ERROR: {logEx.Message}. Message: {message}");
            }
        }

        // ------------------ Callback handlers (now fire events if enabled) ------------------
        private void HandleDeviceError(CTMEventInfo evtInfo, CTMDeviceError deviceError)
        {
            string logMsg = $"Native callback: DeviceError hit. Model={deviceError.deviceInfo.deviceModel}, Code={deviceError.resultCode}";
            Console.WriteLine($"[{DateTime.Now}] {logMsg}"); // Для VS Output
            LogToFile(logMsg);

            if (_eventsEnabled)
            {
                string errorInfo = $"Ошибка: Model={deviceError.deviceInfo.deviceModel}, Code={deviceError.resultCode}, Extended={deviceError.extendedResultCode}";
                bool hasSubscribers = OnDeviceErrorEvent?.GetInvocationList().Length > 0;
                LogToFile($"Attempting to fire OnDeviceError to 1C. Has subscribers: {hasSubscribers}. ErrorInfo: {errorInfo}");
                try
                {
                    OnDeviceErrorEvent?.Invoke(errorInfo); // COM-событие в 1С
                    LogToFile("OnDeviceError: SUCCESSFULLY FIRED to 1C (no exception).");
                }
                catch (Exception fireEx)
                {
                    LogToFile($"OnDeviceError: FAILED to fire to 1C. Exception: {fireEx.Message}");
                }
            }
            else
            {
                LogToFile("OnDeviceError: SKIPPED (events disabled).");
            }
        }

        private void HandleCashAccept(CTMEventInfo evtInfo, CTMAcceptEvent acceptEvent)
        {
            string logMsg = $"Native callback: CashAccept hit. Amount={acceptEvent.amount}, Denom={acceptEvent.cashUnit.denomination}";
            Console.WriteLine($"[{DateTime.Now}] {logMsg}");
            LogToFile(logMsg);

            if (_eventsEnabled)
            {
                string info = $"Принято: {acceptEvent.cashUnit.denomination}, Сумма в txn: {acceptEvent.amount}";
                bool hasSubscribers = OnCashAcceptEvent?.GetInvocationList().Length > 0;
                LogToFile($"Attempting to fire OnCashAccept to 1C. Has subscribers: {hasSubscribers}. Info: {info}");
                try
                {
                    OnCashAcceptEvent?.Invoke(info);
                    LogToFile("OnCashAccept: SUCCESSFULLY FIRED to 1C (no exception).");
                }
                catch (Exception fireEx)
                {
                    LogToFile($"OnCashAccept: FAILED to fire to 1C. Exception: {fireEx.Message}");
                }
            }
            else
            {
                LogToFile("OnCashAccept: SKIPPED (events disabled).");
            }
        }

        private void HandleCashAcceptComplete(CTMEventInfo evtInfo)
        {
            string logMsg = $"Native callback: CashAcceptComplete hit.";
            Console.WriteLine($"[{DateTime.Now}] {logMsg}");
            LogToFile(logMsg);

            if (_eventsEnabled)
            {
                bool hasSubscribers = OnCashAcceptCompleteEvent?.GetInvocationList().Length > 0;
                LogToFile($"Attempting to fire OnCashAcceptComplete to 1C. Has subscribers: {hasSubscribers}.");
                try
                {
                    OnCashAcceptCompleteEvent?.Invoke();
                    LogToFile("OnCashAcceptComplete: SUCCESSFULLY FIRED to 1C (no exception).");
                }
                catch (Exception fireEx)
                {
                    LogToFile($"OnCashAcceptComplete: FAILED to fire to 1C. Exception: {fireEx.Message}");
                }
            }
            else
            {
                LogToFile("OnCashAcceptComplete: SKIPPED (events disabled).");
            }
        }

        private void HandleDeviceStatus(CTMEventInfo evtInfo, CTMDeviceStatus deviceStatus)
        {
            string logMsg = $"Native callback: DeviceStatus hit. Model={deviceStatus.deviceInfo.deviceModel}, Status={deviceStatus.status}";
            Console.WriteLine($"[{DateTime.Now}] {logMsg}");
            LogToFile(logMsg);

            if (_eventsEnabled)
            {
                string statusInfo = $"Статус: Model={deviceStatus.deviceInfo.deviceModel}, State={deviceStatus.status}";
                bool hasSubscribers = OnDeviceStatusEvent?.GetInvocationList().Length > 0;
                LogToFile($"Attempting to fire OnDeviceStatus to 1C. Has subscribers: {hasSubscribers}. StatusInfo: {statusInfo}");
                try
                {
                    OnDeviceStatusEvent?.Invoke(statusInfo);
                    LogToFile("OnDeviceStatus: SUCCESSFULLY FIRED to 1C (no exception).");
                }
                catch (Exception fireEx)
                {
                    LogToFile($"OnDeviceStatus: FAILED to fire to 1C. Exception: {fireEx.Message}");
                }
            }
            else
            {
                LogToFile("OnDeviceStatus: SKIPPED (events disabled).");
            }
        }
    }
}