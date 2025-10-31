using CTMOnCSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        string GetTxnId();
        string GetCustomerTxnId();
        bool BeginCustomerTransaction(string txnId);
        bool EndCustomerTransaction(string txnId);
        bool AcceptCash(int amount);
        bool StopAcceptingCash();
        object DispenseCash(int amount);
        ArrayList GetDispensableCashCounts();
        ArrayList GetNonDispensableCashCounts();
        void AdviseEvents();
        void UnadviseEvents();
        void SetConnection(object pConnection);
        object GetFullConfig();
        bool BeginCashManagementTransaction(string userId, string cashierId, out string txnId);
        bool EndCashManagementTransaction(string txnId);
        CTMAcceptCashRequestResult BeginRefill(int targetAmount = -1);
        bool EndRefill();
    }


    [ComVisible(true)]
    [Guid("5C6E18AF-3B0F-4639-90B0-B04D1B9FF999")]
    [ProgId("CTMOleClient.CTMWrapper")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CTMWrapper : StandardAddIn, ICTMWrapper
    {
        private string _logPath = null;
        private string _cmTxnId = string.Empty;
        private string _customerTxnId = string.Empty;
        private string _lastError = string.Empty;
        private bool _eventsEnabled = false;
        private string _clientId = "";


        public CTMWrapper() : base() {}

        private CtmCClient.OnDeviceErrorCallBack _deviceErrorCallback;
        private CtmCClient.OnCashAcceptCallBack _cashAcceptCallback;
        private CtmCClient.OnCashAcceptCompleteCallBack _cashAcceptCompleteCallback;
        private CtmCClient.OnDeviceStatusCallBack _deviceStatusCallback;
        private CtmCClient.OnSocketClosedCallBack _socketClosedCallback;
        private CtmCClient.OnChangeContextCallBack _changeContextCallback;
        private CtmCClient.OnAuthenticationCallBack _authenticationCallback;
        private CtmCClient.OnCMClosedCallBack _cmClosedCallback;
        private SynchronizationContext _uiContext;

        private Dictionary<CTMOnCSharp.CTMDeviceType, int> _deviceStatuses = new Dictionary<CTMOnCSharp.CTMDeviceType, int>();




        public override void Init(object pConnection)
        {
            LogToFile("Init: called.");
            SetConnection(pConnection);  
        }

        public void SetConnection(object pConnection)
        {
            _oneCObject = pConnection;  // This is the 1C form object
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            LogToFile($"SetConnection: UI Context captured for 1C 8.2 form ({_uiContext.GetType().Name}).");
        }

        public override void Done()
        {
            _uiContext = null;  
            GC.Collect();       
            GC.WaitForPendingFinalizers();

            base.Done();  
            LogToFile("Done: UI Context freed and finalizers executed.");
        }

        public string GetLastError()
        {
            LogToFile($"GetLastError: returning '{_lastError}'.");
            return _lastError;
        }

        public string GetCurrentClientId()
        {
            LogToFile($"GetCurrentClientId: returning '{_clientId}'.");
            return _clientId;
        }

        public string GetTxnId()
        {
            LogToFile($"GetCmTxnId: returning '{_cmTxnId}'.");
            return _cmTxnId;
        }

        public string GetCustomerTxnId()
        {
            LogToFile($"GetCustomerTxnId: returning '{_customerTxnId}'.");
            return _customerTxnId;
        }

        public bool Initialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            LogToFile($"Initialize: called with clientId='{clientId}', overrideHost='{overrideHost ?? "null"}', overridePort='{overridePort ?? "null"}'.");            try
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
                    _clientId = clientId;
                    LogToFile($"Initialize: SUCCESS (host: {serviceLocation}, port: {portNumber}).");
                    return true;
                }
                _lastError = result.ToString();
                LogToFile($"Initialize: FAILED ({result}) (host: {serviceLocation}, port: {portNumber}).");
                return false;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"Initialize: EXCEPTION {ex.Message}.");
                return false;
            }
        }

        public void Uninitialize()
        {
            LogToFile("Uninitialize: called.");
            try
            {
                UnadviseEvents();  
                CtmCClient.Uninitialize();  
                _lastError = "Uninitialized";
                _uiContext = null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();  

                LogToFile("Uninitialize: Complete, handlers freed, GC done.");
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"Uninitialize error: {ex.Message}");
            }
        }

        public bool Reinitialize(string clientId, string overrideHost = null, string overridePort = null)
        {
            LogToFile($"Reinitialize: called for clientId='{clientId}'.");
            Uninitialize();
            var result = Initialize(clientId, overrideHost, overridePort);
            LogToFile($"Reinitialize: result = {(result ? "SUCCESS" : "FAIL")}");
            return result;
        }

        public string GetConfig(string key)
        {
            LogToFile($"GetConfig: called for key='{key}'.");
            try
            {
                var configResult = CtmCClient.GetConfig();
                LogToFile("GetConfig: native call returned.");
                if (configResult.config.count == 0) return string.Empty;

                string value = string.Empty;
                IntPtr ptr = configResult.config.intPtr;
                int size = Marshal.SizeOf(typeof(CTMConfigurationKeyValue));
                for (int i = 0; i < configResult.config.count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                    CTMConfigurationKeyValue kv = (CTMConfigurationKeyValue)Marshal.PtrToStructure(itemPtr, typeof(CTMConfigurationKeyValue));
                    LogToFile($"{kv.key}: {kv.value}");

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
                LogToFile("GetConfig: exception occurred while retrieving key.");
                return string.Empty;
            }
        }
        
        public object GetFullConfig()
        {
            LogToFile("GetFullConfig: called.");
            try
            {
                _lastError = "";
                var configResult = CtmCClient.GetConfig();
                LogToFile($"GetFullConfig: received config with {configResult.config.count} entries.");

                if (configResult.config.count == 0)
                {
                    _lastError = "Config empty";
                    LogToFile("GetFullConfig: Config empty.");
                    return new ConfigInfo(configResult.config);
                }

                var configInfo = new ConfigInfo(configResult.config);
                LogToFile("GetFullConfig: ConfigInfo constructed.");

                _lastError = "OK";
                return configInfo;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"GetFullConfig: EXCEPTION {ex.Message}.");
                return new ConfigInfo(new CTMConfiguration { count = 0 });
            }
        }


        public bool BeginCustomerTransaction(string txnId)
        {
            LogToFile($"BeginCustomerTransaction: txnId='{txnId}' (client: {_clientId ?? "unknown"})");
            try
            {
                if (string.IsNullOrEmpty(txnId))
                {
                    _lastError = "Invalid txnId";
                    LogToFile("✗ Invalid txnId");
                    return false;
                }

                _lastError = "";
                var result = CtmCClient.BeginCustomerTransaction(txnId);  // Return struct
                LogToFile($"BeginCustomer raw result: error={result.error}, intPtr={result.intPtr.ToInt64():X}, txnId='{result.transactionId}'");
                if (result.error == CTMBeginTransactionError.CTM_BEGIN_TRX_SUCCESS)
                {
                    string actualId = !string.IsNullOrEmpty(result.transactionId) ? result.transactionId : txnId;
                    _customerTxnId = actualId;
                    _lastError = "OK";
                    LogToFile($"✓ Customer Transaction started: txnId={actualId}");
                    return true;
                }
                else
                {
                    _lastError = result.error.ToString();
                    LogToFile($"✗ BeginCustomerTransaction failed: {result.error}");
                    if (result.intPtr != IntPtr.Zero) Marshal.FreeHGlobal(result.intPtr);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _lastError = $"EX: {ex.Message}";
                LogToFile(_lastError + "\n" + ex.StackTrace);
                return false;
            }
        }


        public bool EndCustomerTransaction(string txnId)
        {
            LogToFile($"EndCustomerTransaction: txnId='{txnId ?? _customerTxnId}'");
            try
            {
                string actualTxnId = txnId ?? _customerTxnId;
                if (string.IsNullOrEmpty(actualTxnId))
                {
                    _lastError = "No active customer transaction ID";
                    LogToFile("✗ No customer txnId for End — skip DLL call");
                    return false;
                }

                _lastError = "";
                var result = CtmCClient.EndCustomerTransaction(actualTxnId);  // Вызов DLL
                LogToFile($"EndCustomer raw result: {result} (int: {(int)result})");

                bool success = (result == CTMEndTransactionResult.CTM_END_TRX_SUCCESS) ;  

                _lastError = success ? "OK" : result.ToString(); 
                _customerTxnId = string.Empty;  

                if (!success)
                {
                    LogToFile($"✗ EndCustomer real error: {result}");
                    return false;
                }

                LogToFile($"✓ Customer Transaction ended: txnId={actualTxnId}");
                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"EX: {ex.Message}";
                LogToFile(_lastError + "\n" + ex.StackTrace);
                return false;
            }
        }
        

        public bool AcceptCash(int amount)
        {
            LogToFile($"AcceptCash: requested amount={amount}.");
            var result = CtmCClient.AcceptCash(amount);
            _lastError = result.ToString();
            LogToFile($"AcceptCash: result={result}.");
            return result == CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_SUCCESS;
        }

        public bool StopAcceptingCash()
        {
            LogToFile("StopAcceptingCash: called.");
            var result = CtmCClient.StopAcceptingCash();
            _lastError = result.ToString();
            LogToFile($"StopAcceptingCash: result={result}.");
            return result == CTMStopAcceptingCashResult.CTM_STOP_ACCEPTING_CASH_SUCCESS;
        }

        public object DispenseCash(int amount)
        {
            LogToFile($"DispenseCash: requested amount={amount}.");

            CTMDispenseCashResult result = CtmCClient.DispenseCash(amount);
            LogToFile($"DispenseCash raw result: error={result.error}, amountDispensed={result.amountDispensed}");

            var dispenseResult = new DispenseCashResult { Success = false, AmountDispensed = (int)result.amountDispensed };
            if (result.error == CTMDispenseCashError.CTM_DISPENSE_CASH_SUCCESS)
            {
                dispenseResult.Success = true;
                LogToFile($" success — dispensed {result.amountDispensed}");
            }

            if (result.cashUnitSet.intPtr != IntPtr.Zero && result.cashUnitSet.count > 0)
            {
                int unitSize = Marshal.SizeOf(typeof(CTMCashUnit));
                for (int i = 0; i < result.cashUnitSet.count; i++)
                {
                    IntPtr unitPtr = IntPtr.Add(result.cashUnitSet.intPtr, i * unitSize);
                    var unit = (CTMCashUnit)Marshal.PtrToStructure(unitPtr, typeof(CTMCashUnit));
                    var cashUnit = new CashUnitInfo
                    {
                        Denomination = unit.denomination,
                        Count = unit.count,
                        CurrencyCode = "USD",  
                        Type = (int)unit.type
                    };
                    dispenseResult.DispensedUnits.Add(cashUnit);
                    LogToFile($"DispenseCash: cashUnit[{i}] type={unit.type}, denomination={unit.denomination}, count={unit.count}, currencyCode='USD'.");
                }
            }
            else
            {
                LogToFile("Warning: cashUnitSet ptr NULL, units empty");
            }
            return dispenseResult;
        }

        public ArrayList GetDispensableCashCounts()
        {
            LogToFile("GetDispensableCashCounts: called.");
            try
            {
                _lastError = "";
                var result = CtmCClient.GetDispensableCashCounts();
                if (result.error == CTMGetCashCountsError.CTM_GET_CASH_COUNTS_SUCCESS)
                {
                    var list = new ArrayList();
                    for (int i = 0; i < result.cashUnitSet.count; i++)
                    {
                        var unit = new CTMCashUnit();
                        IntPtr ptr = IntPtr.Add(result.cashUnitSet.intPtr, i * Marshal.SizeOf(typeof(CTMCashUnit)));
                        unit = (CTMCashUnit)Marshal.PtrToStructure(ptr, typeof(CTMCashUnit));
                        var info = new CashUnitInfo();
                        info.FromUnmanaged(unit);
                        list.Add(info);
                    }
                    LogToFile($"GetDispensableCashCounts: returned {list.Count} items.");
                    return list;
                }
                else
                {
                    _lastError = result.error.ToString();
                    LogToFile($"GetDispensableCashCounts: error {result.error} — return empty list.");
                    return new ArrayList();   
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"GetDispensableCashCounts: EXCEPTION {ex.Message}");
                return new ArrayList();   
            }
        }

        public ArrayList GetNonDispensableCashCounts()
        {
            LogToFile("GetNonDispensableCashCounts: called.");
            var list = new ArrayList();
            try
            {
                _lastError = "";
                CTMGetCashCountsResult countsResult = CtmCClient.GetNonDispensableCashCounts();
                if (countsResult.error != CTMGetCashCountsError.CTM_GET_CASH_COUNTS_SUCCESS)
                {
                    _lastError = countsResult.error.ToString();
                    LogToFile($"GetNonDispensableCashCounts: error {countsResult.error} — return empty list.");
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
                        Type = (int)unit.type,  // 0 = COIN, 1 = NOTE
                        CurrencyCode = unit.currencyCode ?? string.Empty
                    };
                    list.Add(info);
                }

                try
                {
                    if (cashUnitSet.intPtr != IntPtr.Zero)
                    {
                        CtmCClient.FreeCashUnitSetContents(ref cashUnitSet);
                        LogToFile("GetNonDispensableCashCounts: memory freed.");
                    }
                }
                catch (Exception freeEx)
                {
                    LogToFile($"GetNonDispensableCashCounts: free error {freeEx.Message} — memory may leak.");
                }

                _lastError = "OK";
                LogToFile($"GetNonDispensableCashCounts: returned {list.Count} items.");
                return list;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"GetNonDispensableCashCounts: EXCEPTION {ex.Message}");
                return list;
            }
        }

        public void AdviseEvents()
        {
            LogToFile("AdviseEvents: called.");
            _eventsEnabled = true;
            LogToFile("AdviseEvents: ENABLED for 1C x86.");
        }

        public void UnadviseEvents()
        {
            LogToFile("UnadviseEvents: called.");
            try
            {
                if (_deviceErrorCallback != null)
                {
                    CtmCClient.RemoveDeviceErrorEventHandler(_deviceErrorCallback);
                    _deviceErrorCallback = null;
                }
                if (_cashAcceptCallback != null)
                {
                    CtmCClient.RemoveCashAcceptEventHandler(_cashAcceptCallback);
                    _cashAcceptCallback = null;
                }
                if (_cashAcceptCompleteCallback != null)
                {
                    CtmCClient.RemoveCashAcceptCompleteEventHandler(_cashAcceptCompleteCallback);
                    _cashAcceptCompleteCallback = null;
                }
                if (_deviceStatusCallback != null)
                {
                    CtmCClient.RemoveDeviceStatusEventHandler(_deviceStatusCallback);
                    _deviceStatusCallback = null;
                }
                if (_socketClosedCallback != null)
                {
                    CtmCClient.RemoveSocketClosedEventHandler(_socketClosedCallback);
                    _socketClosedCallback = null;
                }
                if (_changeContextCallback != null)
                {
                    CtmCClient.RemoveChangeContextEventHandler(_changeContextCallback);
                    _changeContextCallback = null;
                }
                if (_authenticationCallback != null)
                {
                    CtmCClient.RemoveAuthenticationEventHandler(_authenticationCallback); 
                    _authenticationCallback = null;
                }
                if (_cmClosedCallback != null)
                {
                    CtmCClient.RemoveCMClosedEventHandler(_cmClosedCallback);
                    _cmClosedCallback = null;
                }

                _eventsEnabled = false;
                LogToFile("UnadviseEvents: All handlers removed.");
            }
            catch (Exception ex)
            {
                LogToFile($"UnadviseEvents error: {ex.Message}");
            }
        }

        private void AddCallbacks()
        {
            if (_eventsEnabled) return;

            _deviceErrorCallback = HandleDeviceError;
            _cashAcceptCallback = HandleCashAccept;
            _cashAcceptCompleteCallback = HandleCashAcceptComplete;
            _deviceStatusCallback = HandleDeviceStatus;
            _socketClosedCallback = HandleSocketClosed;
            _changeContextCallback = HandleChangeContext;
            _authenticationCallback = HandleAuthentication;
            _cmClosedCallback = HandleCMClosed;

            CtmCClient.AddDeviceErrorEventHandler(_deviceErrorCallback);
            LogToFile("DeviceErrorEvent handler added.");

            CtmCClient.AddCashAcceptEventHandler(_cashAcceptCallback);
            LogToFile("CashAccept handler added.");

            CtmCClient.AddCashAcceptCompleteEventHandler(_cashAcceptCompleteCallback);
            LogToFile("CashAcceptComplete handler added.");

            CtmCClient.AddDeviceStatusEventHandler(_deviceStatusCallback);
            LogToFile("DeviceStatus handler added.");

            CtmCClient.AddSocketClosedEventHandler(_socketClosedCallback);
            LogToFile("SocketClosed handler added.");
             
            CtmCClient.AddChangeContextEventHandler(_changeContextCallback);
            LogToFile("ChangeContext handler added.");

            CtmCClient.AddAuthenticationEventHandler(_authenticationCallback);
            LogToFile("Authentication handler added.");

            CtmCClient.AddCMClosedEventHandler(_cmClosedCallback);
            LogToFile("CMClosed handler added.");

            _eventsEnabled = true;
            LogToFile("✓ All callbacks registered");

        }

        private void RemoveCallbacks()
        {
            if (!_eventsEnabled) return;
            CtmCClient.RemoveDeviceErrorEventHandler(_deviceErrorCallback);
            CtmCClient.RemoveCashAcceptEventHandler(_cashAcceptCallback);
            CtmCClient.RemoveCashAcceptCompleteEventHandler(_cashAcceptCompleteCallback);
            CtmCClient.RemoveDeviceStatusEventHandler(_deviceStatusCallback);
            CtmCClient.RemoveSocketClosedEventHandler(_socketClosedCallback);
            CtmCClient.RemoveChangeContextEventHandler(_changeContextCallback);
            CtmCClient.RemoveAuthenticationEventHandler(_authenticationCallback);
            CtmCClient.RemoveCMClosedEventHandler(_cmClosedCallback);
            
            _eventsEnabled = false;
            LogToFile("✓ All callbacks unregistered");
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
            try
            {
                uint amount = acceptEvent.amount;
                uint amountDue = acceptEvent.amountDue;
                int denom = acceptEvent.cashUnit.denomination;
                string curr = acceptEvent.cashUnit.currencyCode ?? "USD";

                LogToFile($"CashAccept: Принято: {amount} {curr}, Сумма: {denom}, Итого: {amountDue}");

                if (_eventsEnabled && _uiContext != null && _oneCObject != null)
                {
                    object[] params1C = { (int)amount, (int)amountDue, denom, curr };
                    _uiContext.Post(_ => InvokeOneCEvent("OnCashAccept", params1C), null);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"OnCashAccept ERROR: {ex.Message}");
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
            _deviceStatuses[deviceStatus.deviceInfo.deviceType] = deviceStatus.status;
            LogToFile($"Device {deviceStatus.deviceInfo.deviceType} status updated: {deviceStatus.status} (ready if >0)");
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
            if (_oneCObject == null)
            {
                LogToFile($"InvokeOneCEvent: Skipped (no 1C object). Event={eventName}, Params={string.Join(", ", parameters ?? new object[0])}.");
                return;
            }

            try
            {
                Type type = _oneCObject.GetType();
                type.InvokeMember(eventName, BindingFlags.InvokeMethod, null, _oneCObject, parameters);
                LogToFile($"OneC Event {eventName} invoked OK. Params: {string.Join(", ", parameters ?? new object[0])}.");
            }
            catch (MissingMethodException ex)
            {
                LogToFile($"OneC missing {eventName}: {ex.Message}. Params: {string.Join(", ", parameters ?? new object[0])}.");
            }
            catch (Exception ex)
            {
                LogToFile($"InvokeOneCEvent {eventName} ERROR: {ex.Message}. Stack: {ex.StackTrace}. Params: {string.Join(", ", parameters ?? new object[0])}.");
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
        }

        public string GetLogPath()
        {
            LogToFile($"GetLogPath: returning '{_logPath ?? string.Empty}'.");
            return _logPath ?? string.Empty;
        }

        public bool BeginCashManagementTransaction(string userId, string cashierId, out string txnId)
        {
            txnId = string.Empty;  
            LogToFile($"BeginCashManagementTransaction: userId='{userId}', cashierId='{cashierId}'");

            try
            {
                _lastError = "";

                string tempBuffer = string.Empty;
                CTMBeginTransactionResult result = CtmCClient.BeginCashManagementTransaction(userId, cashierId, tempBuffer);

                LogToFile($"BeginCM raw result: error={result.error}, transactionId from struct={result.transactionId}");

                if (result.error == CTMBeginTransactionError.CTM_BEGIN_TRX_SUCCESS)
                {
                    _cmTxnId = result.transactionId;
                    txnId = result.transactionId;
                    LogToFile($"✓ CM Transaction started: txnId={_cmTxnId}");
                    return true;
                }
                else
                {
                    _lastError = result.error.ToString();
                    LogToFile($"✗ CM Transaction failed: {result.error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _lastError = $"EX: {ex.Message}";
                LogToFile(_lastError + "\n" + ex.StackTrace);
                return false;
            }
        }
       
        public bool EndCashManagementTransaction(string txnId)
        {
            LogToFile($"EndCashManagementTransaction: txnId='{txnId ?? _cmTxnId}'");
            try
            {
                string actualTxnId = txnId ?? _cmTxnId;
                if (string.IsNullOrEmpty(actualTxnId))
                {
                    _lastError = "No active CM transaction ID";
                    LogToFile("✗ No CM txnId for End — skip DLL call");
                    return false;
                }

                _lastError = "";
                CTMEndTransactionResult result = CtmCClient.EndCashManagementTransaction(actualTxnId);
                LogToFile($"EndCM raw result: {result} (int: {(int)result})");

                if (result == CTMEndTransactionResult.CTM_END_TRX_SUCCESS)
                {
                    _cmTxnId = "";  
                    LogToFile($"✓ CM Transaction ended: txnId={actualTxnId}");
                    return true;
                }
                else if (result == CTMEndTransactionResult.CTM_END_TRX_ERROR_NO_TRANSACTION_IN_PROGRESS)
                {
                    _cmTxnId = "";  
                    LogToFile($"✓ No active txn — graceful end: {actualTxnId}");
                    return true; 
                }
                else
                {
                    _lastError = result.ToString();
                    LogToFile($"✗ EndCM error: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _lastError = $"EX: {ex.Message}";
                LogToFile(_lastError + "\n" + ex.StackTrace);
                return false;
            }
        }
       
        public CTMAcceptCashRequestResult BeginRefill(int targetAmount = -1)
        {
            LogToFile($"BeginRefill: targetAmount={targetAmount} (CM txn: {_cmTxnId})");
            try
            {
                if (string.IsNullOrEmpty(_cmTxnId)) { _lastError = "No active CM transaction"; return CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_ERROR_NEEDS_OPEN_TRANSACTION_ID; }
                _lastError = "";
                var result = CtmCClient.BeginRefill(targetAmount);  
                if (result == CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_SUCCESS)
                {
                    LogToFile("Refill started: acceptors enabled");
                    return result;
                }
                _lastError = result.ToString();
                LogToFile($"BeginRefill failed: {result}");
                return result;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"Exception in BeginRefill: {ex}");
                return CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_ERROR_UNHANDLED_EXCEPTION;
            }
        }

        public bool EndRefill()
        {
            LogToFile("EndRefill: called (disables acceptors)");
            try
            {
                _lastError = "";
                CTMStopAcceptingCashResult result = CtmCClient.StopAcceptingCash();
                if (result == CTMStopAcceptingCashResult.CTM_STOP_ACCEPTING_CASH_SUCCESS)
                {
                    LogToFile($"✓ CM Refill ended");
                    return true;
                }
                else
                {
                    _lastError = result.ToString();
                    LogToFile($"✗ EndRefill error: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _lastError = $"EX: {ex.Message}";
                LogToFile(_lastError + "\n" + ex.StackTrace);
                return false;
            }

        }


    }
}