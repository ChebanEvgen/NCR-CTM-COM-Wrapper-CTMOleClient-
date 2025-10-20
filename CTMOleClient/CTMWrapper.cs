using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using CTMOnCSharp;

namespace CTMOleClient
{
    // Event interface (для событий в 1C)
    [ComVisible(true)]
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface ICTMWrapperEvents
    {
        [DispId(1)] void OnDeviceError([In] string errorInfo);
        [DispId(2)] void OnCashAccept([In] string acceptInfo);
        [DispId(3)] void OnCashAcceptComplete();
        [DispId(4)] void OnDeviceStatus([In] string statusInfo);
    }

    // Интерфейс методов (для 1C)
    [ComVisible(true)]
    [Guid("D36A29C9-0B48-4F39-BB51-8F3B738AA111")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ICTMWrapper
    {
        string Initialize(string clientId, string overrideHost = null, string overridePort = null);
        void Uninitialize();
        string Reinitialize(string clientId, string overrideHost = null, string overridePort = null);
        string GetLastError();
        string GetConfig(string key);
        bool BeginCustomerTransaction(string txnId);
        bool EndCustomerTransaction(string txnId);
        bool AcceptCash(int amount);
        bool StopAcceptingCash();
        bool DispenseCash(int amount);
        string GetDispensableCashCounts();
        string GetNonDispensableCashCounts();
        void AdviseEvents();
        void UnadviseEvents();
    }

    // Основной класс (наследует StandardAddIn, реализует ICTMWrapper)
    [ComVisible(true)]
    [Guid("5C6E18AF-3B0F-4639-90B0-B04D1B9FF999")]
    [ProgId("CTMOleClient.CTMWrapper")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(ICTMWrapperEvents))]
    public class CTMWrapper : StandardAddIn, ICTMWrapper
    {
        public CTMWrapper() : base() { }  // Конструктор

        // События
        public event Action<string> OnDeviceErrorEvent;
        public event Action<string> OnCashAcceptEvent;
        public event Action OnCashAcceptCompleteEvent;
        public event Action<string> OnDeviceStatusEvent;

        private CtmCClient.OnDeviceErrorCallBack _deviceErrorCallback;
        private CtmCClient.OnCashAcceptCallBack _cashAcceptCallback;
        private CtmCClient.OnCashAcceptCompleteCallBack _cashAcceptCompleteCallback;
        private CtmCClient.OnDeviceStatusCallBack _deviceStatusCallback;

        private string _lastError = string.Empty;
        private string _currentTransactionId = string.Empty;
        private bool _eventsEnabled = false;

        // Реализация методов ICTMWrapper
        public string GetLastError() => _lastError;

        public string Initialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            try
            {
                _lastError = "";
                Utils utils = Utils.Instance;
                string serviceLocation = overrideHost ?? (utils.Properties.ContainsKey("rpc.host") ? utils.Properties["rpc.host"] : "localhost");
                string portNumber = overridePort ?? (utils.Properties.ContainsKey("rpc.port") ? utils.Properties["rpc.port"] : "3636");
                string serviceConnection = $"ctm://{serviceLocation}:{portNumber}";

                var result = CtmCClient.Initialize(serviceConnection, clientId, CTMClientType.CTM_POS);

                if (result == CTMInitializationResult.CTM_INIT_SUCCESS)
                {
                    AddCallbacks();
                    _lastError = "OK";
                    _currentTransactionId = string.Empty;
                    LogToFile($"[{DateTime.Now}] Initialize: SUCCESS.");
                    return "OK";
                }
                _lastError = result.ToString();
                LogToFile($"[{DateTime.Now}] Initialize: FAILED ({result}).");
                return _lastError;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"[{DateTime.Now}] Initialize: EXCEPTION {ex.Message}.");
                return _lastError;
            }
        }

        public void Uninitialize()
        {
            UnadviseEvents();
            CtmCClient.Uninitialize();
            _currentTransactionId = string.Empty;
            _lastError = "Uninitialized";
            LogToFile($"[{DateTime.Now}] Uninitialize: Called.");
        }

        public string Reinitialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            Uninitialize();
            return Initialize(clientId, overrideHost, overridePort);
        }

        public string GetConfig(string key)
        {
            try
            {
                var configResult = CtmCClient.GetConfig();
                if (configResult.config.count == 0)
                {
                    _lastError = "No config available";
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
                return value;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return string.Empty;
            }
        }

        public bool BeginCustomerTransaction(string txnId)
        {
            var resultStruct = CtmCClient.BeginCustomerTransaction(txnId);
            var resultCode = resultStruct.error;
            _lastError = resultCode.ToString();
            if (resultCode == CTMBeginTransactionError.CTM_BEGIN_TRX_SUCCESS)
            {
                _currentTransactionId = txnId;
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
                _currentTransactionId = string.Empty;
                return true;
            }
            return false;
        }

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
            _lastError = result.error.ToString();
            return result.error == CTMDispenseCashError.CTM_DISPENSE_CASH_SUCCESS;
        }

        public string GetDispensableCashCounts()
        {
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetDispensableCashCounts();
                if (countsResult.error != 0) return string.Empty;

                var sb = new StringBuilder("Dispensable Cash Counts:\n");
                var cashUnitSet = countsResult.cashUnitSet;
                IntPtr ptr = cashUnitSet.intPtr;
                int size = Marshal.SizeOf(typeof(CTMCashUnit));
                for (int i = 0; i < cashUnitSet.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                    sb.AppendLine($"Denom: {unit.denomination}, Count: {unit.count}");
                }
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetNonDispensableCashCounts()
        {
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetNonDispensableCashCounts();
                if (countsResult.error != 0) return string.Empty;

                var sb = new StringBuilder("Non-Dispensable Cash Counts:\n");
                var cashUnitSet = countsResult.cashUnitSet;
                IntPtr ptr = cashUnitSet.intPtr;
                int size = Marshal.SizeOf(typeof(CTMCashUnit));
                for (int i = 0; i < cashUnitSet.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                    sb.AppendLine($"Denom: {unit.denomination}, Count: {unit.count}");
                }
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public void AdviseEvents()
        {
            _eventsEnabled = true;
            LogToFile($"[{DateTime.Now}] AdviseEvents: ENABLED.");
        }

        public void UnadviseEvents()
        {
            _eventsEnabled = false;
            LogToFile($"[{DateTime.Now}] UnadviseEvents: DISABLED.");
        }

        // Callbacks
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
        }

        private void HandleDeviceError(CTMEventInfo evtInfo, CTMDeviceError deviceError)
        {
            string errorInfo = $"Ошибка: Model={deviceError.deviceInfo.deviceModel}, Code={deviceError.resultCode}";
            LogToFile($"DeviceError: {errorInfo}");
            if (_eventsEnabled) OnDeviceErrorEvent?.Invoke(errorInfo);
        }

        private void HandleCashAccept(CTMEventInfo evtInfo, CTMAcceptEvent acceptEvent)
        {
            string info = $"Принято: {acceptEvent.cashUnit.denomination}, Сумма: {acceptEvent.amount}";
            LogToFile($"CashAccept: {info}");
            if (_eventsEnabled) OnCashAcceptEvent?.Invoke(info);
        }

        private void HandleCashAcceptComplete(CTMEventInfo evtInfo)
        {
            LogToFile("CashAcceptComplete");
            if (_eventsEnabled) OnCashAcceptCompleteEvent?.Invoke();
        }

        private void HandleDeviceStatus(CTMEventInfo evtInfo, CTMDeviceStatus deviceStatus)
        {
            string statusInfo = $"Статус: Model={deviceStatus.deviceInfo.deviceModel}, State={deviceStatus.status}";
            LogToFile($"DeviceStatus: {statusInfo}");
            if (_eventsEnabled) OnDeviceStatusEvent?.Invoke(statusInfo);
        }

        private void LogToFile(string message)
        {
            try
            {
                string logPath = @"C:\Temp\CTMLogs.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { /* Ignore */ }
        }
    }
}