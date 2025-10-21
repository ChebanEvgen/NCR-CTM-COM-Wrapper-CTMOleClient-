using CTMOnCSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CTMOleClient
{
    [ComVisible(true)]
    [Guid("D36A29C9-0B48-4F39-BB51-8F3B738AA111")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ICTMWrapper
    {
        bool Initialize(string clientId, string overrideHost = null, string overridePort = null);
        void Uninitialize();
        void SetLogPath(string logPath);
        string GetLogPath();
        bool Reinitialize(string clientId, string overrideHost = null, string overridePort = null);
        string GetLastError();
        string GetConfig(string key);
        bool BeginCustomerTransaction(string txnId);
        bool EndCustomerTransaction(string txnId);
        bool AcceptCash(int amount);
        bool StopAcceptingCash();
        bool DispenseCash(int amount);
        ArrayList GetDispensableCashCounts();
        ArrayList GetNonDispensableCashCounts();
        void AdviseEvents();
        void UnadviseEvents();
        void SetConnection(object pConnection);
        object GetFullConfig();
    }

    [ComVisible(true)]
    [Guid("5C6E18AF-3B0F-4639-90B0-B04D1B9FF999")]
    [ProgId("CTMOleClient.CTMWrapper")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CTMWrapper : StandardAddIn, ICTMWrapper
    {
        private string _logPath = null;

        public CTMWrapper() : base() {}


        private CtmCClient.OnDeviceErrorCallBack _deviceErrorCallback;
        private CtmCClient.OnCashAcceptCallBack _cashAcceptCallback;
        private CtmCClient.OnCashAcceptCompleteCallBack _cashAcceptCompleteCallback;
        private CtmCClient.OnDeviceStatusCallBack _deviceStatusCallback;
        private CtmCClient.OnSocketClosedCallBack _socketClosedCallback;
        private CtmCClient.OnChangeContextCallBack _changeContextCallback;
        private CtmCClient.OnAuthenticationCallBack _authenticationCallback;
        private CtmCClient.OnCMClosedCallBack _cmClosedCallback;

        private string _lastError = string.Empty;
        private string _currentTransactionId = string.Empty;
        private bool _eventsEnabled = false;
        private SynchronizationContext _uiContext;

        public override void Init(object pConnection)
        {
            SetConnection(pConnection);  
                                         
        }

        public void SetConnection(object pConnection)
        {
            _oneCObject = pConnection;  // ЭтаФорма из 1С
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SetConnection: UI Context captured for 1C 8.2 form ({_uiContext.GetType().Name}).");
        }

        public override void Done()
        {
            _uiContext = null;  
            GC.Collect();       
            GC.WaitForPendingFinalizers();

            base.Done();  
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Done: UI Context freed.");
        }

        public string GetLastError() => _lastError;

        public bool Initialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            try
            {
                _lastError = "";
                string serviceLocation = overrideHost ?? "localhost";
                string portNumber = overridePort ?? "3636";
                string serviceConnection = $"ctm://{serviceLocation}:{portNumber}";

                var result = CtmCClient.Initialize(serviceConnection, clientId, CTMClientType.CTM_POS);

                if (result == CTMInitializationResult.CTM_INIT_SUCCESS)
                {
                    AddCallbacks();
                    _lastError = "OK";
                    _currentTransactionId = string.Empty;
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Initialize: SUCCESS on x86 (host: {serviceLocation}, port: {portNumber}).");
                    return true;
                }
                _lastError = result.ToString();
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Initialize: FAILED ({result}) (host: {serviceLocation}, port: {portNumber}).");
                return false;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Initialize: EXCEPTION {ex.Message}.");
                return false;
            }
        }

        public void Uninitialize()
        {
            _uiContext = null; 
            GC.Collect();      
            GC.WaitForPendingFinalizers();

            UnadviseEvents();
            CtmCClient.Uninitialize();
            _currentTransactionId = string.Empty;
            _lastError = "Uninitialized";
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Uninitialize: Called. UI Context freed.");
        }

        public bool Reinitialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            Uninitialize();
            return Initialize(clientId, overrideHost, overridePort);
        }

        public string GetConfig(string key)
        {
            try
            {
                var configResult = CtmCClient.GetConfig();
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GetConfig: Called.");
                if (configResult.config.count == 0) return string.Empty;

                string value = string.Empty;
                IntPtr ptr = configResult.config.intPtr;
                int size = Marshal.SizeOf(typeof(CTMConfigurationKeyValue));
                for (int i = 0; i < configResult.config.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMConfigurationKeyValue kv = (CTMConfigurationKeyValue)Marshal.PtrToStructure(itemPtr, typeof(CTMConfigurationKeyValue));
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kv.key}: {kv.value} ");

                    if (kv.key == key)
                    {
                        value = kv.value;
                        break;
                    }
                }
                return value;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool BeginCustomerTransaction(string txnId)
        {
            var result = CtmCClient.BeginCustomerTransaction(txnId);
            _lastError = result.error.ToString();
            if (result.error == CTMBeginTransactionError.CTM_BEGIN_TRX_SUCCESS)
            {
                _currentTransactionId = txnId;
                return true;
            }
            return false;
        }

        public bool EndCustomerTransaction(string txnId)
        {
            var result = CtmCClient.EndCustomerTransaction(txnId);
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

        public ArrayList GetDispensableCashCounts()
        {
            var list = new ArrayList();
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetDispensableCashCounts();
                if (countsResult.error != CTMGetCashCountsError.CTM_GET_CASH_COUNTS_SUCCESS)
                {
                    _lastError = countsResult.error.ToString();
                    return list;
                }

                var cashUnitSet = countsResult.cashUnitSet;
                IntPtr ptr = cashUnitSet.intPtr;
                int size = Marshal.SizeOf(typeof(CTMCashUnit));

                for (int i = 0; i < cashUnitSet.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));

                    var info = new CashUnitInfo
                    {
                        Denomination = unit.denomination,
                        Count = unit.count,
                        Type = (int)unit.type,  // 0=COIN, 1=NOTE
                        CurrencyCode = unit.currencyCode
                    };
                    list.Add(info);
                }

                CtmCClient.FreeCashUnitSetContents(ref cashUnitSet);

                _lastError = "OK";
                return list;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return list;
            }
        }

        public ArrayList GetNonDispensableCashCounts()
        {
            var list = new ArrayList();
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetNonDispensableCashCounts();
                if (countsResult.error != CTMGetCashCountsError.CTM_GET_CASH_COUNTS_SUCCESS)
                {
                    _lastError = countsResult.error.ToString();
                    return list;
                }

                var cashUnitSet = countsResult.cashUnitSet;
                IntPtr ptr = cashUnitSet.intPtr;
                int size = Marshal.SizeOf(typeof(CTMCashUnit));

                for (int i = 0; i < cashUnitSet.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));

                    var info = new CashUnitInfo
                    {
                        Denomination = unit.denomination,
                        Count = unit.count,
                        Type = (int)unit.type,  // 0=COIN, 1=NOTE
                        CurrencyCode = unit.currencyCode
                    };
                    list.Add(info);
                }

                CtmCClient.FreeCashUnitSetContents(ref cashUnitSet);

                _lastError = "OK";
                return list;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return list;
            }
        }

        public void AdviseEvents()
        {
            _eventsEnabled = true;
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AdviseEvents: ENABLED for 1C x86.");
        }

        public void UnadviseEvents()
        {
            _eventsEnabled = false;
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnadviseEvents: DISABLED.");
        }

        private void AddCallbacks()
        {
            _deviceErrorCallback = HandleDeviceError;
            _cashAcceptCallback = HandleCashAccept;
            _cashAcceptCompleteCallback = HandleCashAcceptComplete;
            _deviceStatusCallback = HandleDeviceStatus;
            _socketClosedCallback = HandleSocketClosed;
            _changeContextCallback = HandleChangeContext;
            _authenticationCallback = HandleAuthentication;
            _cmClosedCallback = HandleCMClosed;

            CtmCClient.AddDeviceErrorEventHandler(_deviceErrorCallback);
            CtmCClient.AddCashAcceptEventHandler(_cashAcceptCallback);
            CtmCClient.AddCashAcceptCompleteEventHandler(_cashAcceptCompleteCallback);
            CtmCClient.AddDeviceStatusEventHandler(_deviceStatusCallback);
           
            CtmCClient.AddSocketClosedEventHandler(_socketClosedCallback);
            CtmCClient.AddChangeContextEventHandler(_changeContextCallback);
            CtmCClient.AddAuthenticationEventHandler(_authenticationCallback);
            CtmCClient.AddCMClosedEventHandler(_cmClosedCallback);
        }

        private void HandleDeviceError(CTMEventInfo evtInfo, CTMDeviceError deviceError)
        {
            string errorInfo = $"Ошибка: Model={deviceError.deviceInfo.deviceModel}, Code={deviceError.resultCode}";
            LogToFile($"DeviceError: {errorInfo}");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnDeviceError", new object[] { errorInfo }), null);
            }
        }

        private void HandleCashAccept(CTMEventInfo evtInfo, CTMAcceptEvent acceptEvent)
        {
            string info = $"Принято: {acceptEvent.cashUnit.denomination}, Сумма: {acceptEvent.amount}";
            LogToFile($"CashAccept: {info} -> dispatching to unmanaged form.");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnCashAccept", new object[] { info }), null);
            }
        }

        private void HandleCashAcceptComplete(CTMEventInfo evtInfo)
        {
            LogToFile("CashAcceptComplete");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnCashAcceptComplete", new object[] { }), null);
            }
        }

        private void HandleDeviceStatus(CTMEventInfo evtInfo, CTMDeviceStatus deviceStatus)
        {
            string statusInfo = $"Статус: Model={deviceStatus.deviceInfo.deviceModel}, State={deviceStatus.status}";
            LogToFile($"DeviceStatus: {statusInfo}");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnDeviceStatus", new object[] { statusInfo }), null);
            }
        }
       
        private void HandleSocketClosed(CTMEventInfo evtInfo)
        {
            string info = "Соединение с CTM-сервисом закрыто.";
            LogToFile($"SocketClosed: {info}");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnSocketClosed", new object[] { info }), null);
            }
        }

        private void HandleChangeContext(CTMEventInfo evtInfo, CTMContextEvent context)
        {
            string info = $"Смена контекста: {context.context}, Владелец: {context.clientOwner}";
            LogToFile($"ChangeContext: {info}");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnChangeContext", new object[] { info }), null);
            }
        }

        private void HandleAuthentication(CTMEventInfo evtInfo, CTMAuthenticationEvent authEvent)
        {
            try
            {
                bool isHC = (authEvent.isHCashier == CTMBoolean.CTM_TRUE);
                string info = $"Аутентификация: Пользователь={authEvent.cmUsername}, HCashier={isHC}";
                LogToFile($"Authentication: {info} (пароль скрыт для лога)");

                if (_eventsEnabled && _uiContext != null)
                {
                    _uiContext.Post(_ => InvokeOneCEvent("OnAuthentication", new object[] { authEvent.cmUsername, isHC }), null);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"HandleAuthentication: Ошибка обработки события - {ex.Message}");
            }
        }

        private void HandleCMClosed(CTMEventInfo evtInfo)
        {
            string info = "Cash Management приложение закрыто.";
            LogToFile($"CMClosed: {info}");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnCMClosed", new object[] { info }), null);
            }
        }

        private void InvokeOneCEvent(string eventName, object[] parameters)
        {
            if (_oneCObject == null) return;

            try
            {
                Type type = _oneCObject.GetType();  
                type.InvokeMember(eventName, BindingFlags.InvokeMethod, null, _oneCObject, parameters);
                LogToFile($"OneC Event {eventName} invoked OK on unmanaged form.");
            }
            catch (MissingMethodException ex)
            {
                LogToFile($"OneC missing {eventName} on form: {ex.Message}"); 
            }
            catch (Exception ex)
            {
                LogToFile($"InvokeOneCEvent {eventName} on unmanaged: {ex.Message}");
                if (_oneCObject != null)
                {
                    try
                    {
                        Type typeFallback = _oneCObject.GetType();
                        typeFallback.InvokeMember("Сообщить", BindingFlags.InvokeMethod, null, _oneCObject, new object[] { $"CTM Error in {eventName}: {ex.Message}" });
                    }
                    catch { /* Fallback fail */ }
                }
            }
        }

        private void LogToFile(string message)
        {
            if (string.IsNullOrEmpty(_logPath)) return; 

            try
            {
              
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
                File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }  // Silent fail
        }

        public void SetLogPath(string logPath)
        {
            _logPath = string.IsNullOrEmpty(logPath) ? null : logPath;
            LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Log path set to: {_logPath ?? "disabled"}.");
        }

        public string GetLogPath()
        {
            return _logPath ?? string.Empty;
        }

        public object GetFullConfig()
        {
            try
            {
                _lastError = "";
                var configResult = CtmCClient.GetConfig();
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GetFullConfig: Получен config с {configResult.config.count} записями.");

                if (configResult.config.count == 0)
                {
                    _lastError = "Config empty";
                    LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GetFullConfig: Пустой config.");
                    return new ConfigInfo(configResult.config);  
                }

                var configInfo = new ConfigInfo(configResult.config);
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GetFullConfig: Заполнены ключи - Notes: {configInfo.AcceptedNoteDenominations}, Coins: {configInfo.AcceptedCoinDenominations} и т.д.");

                _lastError = "OK";
                return configInfo;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GetFullConfig: EXCEPTION {ex.Message}.");
                return new ConfigInfo(new CTMConfiguration { count = 0 });  
            }
        }
    }
}