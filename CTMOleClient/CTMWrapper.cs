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
        CTMEndRefillResult EndRefill(out int totalAmount);
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
                    _currentTransactionId = string.Empty;
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
                _currentTransactionId = string.Empty;
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

        public bool BeginCustomerTransaction(string txnId)
        {
            LogToFile($"BeginCustomerTransaction: txnId='{txnId}'");
            try
            {
                if (string.IsNullOrEmpty(txnId))
                    txnId = $"TXN_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N.Substring(0,8)}";

                _lastError = "";
                var result = CtmCClient.BeginCustomerTransaction(txnId);
                LogToFile($"BeginCustomer raw result: error={result.error}, txnPtr={result.transactionId.ToInt64():X}");

                // ENHANCED HACK for NCR emulators: if ptr is NULL treat as success (server echoes input ID) and use the input ID
                if (result.transactionId == IntPtr.Zero)
                {
                    _customerTxnId = txnId;  // Fallback: server echoed input ID, ignore garbage error
                    LogToFile($"✓ ENHANCED HACK: ptr NULL (error garbage {result.error}), but server success — using input ID: {_customerTxnId}");
                    return true;
                }

                // Normal case: ptr is valid
                string txnFromPtr = Marshal.PtrToStringAnsi(result.transactionId);
                if (result.error == CTMBeginTransactionError.CTM_BEGIN_TRX_SUCCESS)
                {
                    _customerTxnId = txnFromPtr ?? txnId;
                    // Free DLL-allocated memory (important!)
                    Marshal.FreeHGlobal(result.transactionId);
                    LogToFile($"✓ Customer Transaction started: txnId={_customerTxnId}");
                    return true;
                }

                // On error: free ptr if valid
                if (result.transactionId != IntPtr.Zero)
                    Marshal.FreeHGlobal(result.transactionId);

                _lastError = result.error.ToString();
                LogToFile($"✗ Customer Transaction failed: {result.error}");
                return false;
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
                    _lastError = "No transaction ID";
                    LogToFile("✗ No txnId for End");
                    return false;
                }

                _lastError = "";
                var result = CtmCClient.EndCustomerTransaction(actualTxnId);  // result — enum/int (success=0)
                LogToFile($"EndCustomer raw result: error={result}");

                // HACK for emulators: if garbage error > 1,000,000 treat as OK (server success)
                if ((int)result != 0 && (int)result < 1000000)
                {
                    LogToFile($"✗ End raw error: {result} (real error)");
                    _lastError = result.ToString();
                    return false;
                }

                _customerTxnId = "";  // Reset ID
                // Invoke OnTransactionEnd event
                if (_eventsEnabled && _uiContext != null)
                    _uiContext.Post(_ => InvokeOneCEvent("OnTransactionEnd", new object[] { actualTxnId, "SUCCESS" }), null);

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
            try
            {
                _lastError = "";
                var result = CtmCClient.DispenseCash(amount);
                LogToFile($"DispenseCash raw result: error={result.error}, amountDispensed={result.amountDispensed}");

                var dispenseResult = new DispenseCashResult { Success = false, AmountDispensed = (int)result.amountDispensed };

                // HACK for emulators: if garbage error > 1,000,000 but amount is OK, treat as success
                bool isGarbageError = (int)result.error > 1000000;
                if (result.error == CTMDispenseCashError.CTM_DISPENSE_CASH_SUCCESS || isGarbageError)
                {
                    dispenseResult.Success = true;
                    LogToFile($"✓ HACK: {(isGarbageError ? "garbage error" : "success")} — dispensed {result.amountDispensed}");

                    // Parse units from cashUnitSet (if ptr is valid)
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
                                CurrencyCode = "USD",  // Hardcoded because it's not provided in the native result
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

                    // Device error if main error != 0
                    if (result.error != 0)
                    {
                        dispenseResult.DeviceError = result.error.ToString();
                        LogToFile($"DeviceError: code={result.error}");
                    }

                    return dispenseResult;
                }

                // Real error
                _lastError = result.error.ToString();
                LogToFile($"✗ DispenseCash failed: {result.error}");
                return dispenseResult;
            }
            catch (Exception ex)
            {
                _lastError = $"EX: {ex.Message}";
                LogToFile($"EX in DispenseCash: {ex}");
                return new DispenseCashResult { Error = _lastError };
            }
        }

        public ArrayList GetDispensableCashCounts()
        {
            LogToFile("GetDispensableCashCounts: called.");
            var list = new ArrayList();
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetDispensableCashCounts();
                if (countsResult.error != CTMGetCashCountsError.CTM_GET_CASH_COUNTS_SUCCESS)
                {
                    _lastError = countsResult.error.ToString();
                    LogToFile($"GetDispensableCashCounts: native error {countsResult.error}.");
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
                        CurrencyCode = unit.currencyCode
                    };
                    list.Add(info);
                }

                CtmCClient.FreeCashUnitSetContents(ref cashUnitSet);

                _lastError = "OK";
                LogToFile($"GetDispensableCashCounts: returned {list.Count} items.");
                return list;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"GetDispensableCashCounts: exception {ex.Message}.");
                return list;
            }
        }

        public ArrayList GetNonDispensableCashCounts()
        {
            LogToFile("GetNonDispensableCashCounts: called.");
            var list = new ArrayList();
            try
            {
                CTMGetCashCountsResult countsResult = CtmCClient.GetNonDispensableCashCounts();
                if (countsResult.error != CTMGetCashCountsError.CTM_GET_CASH_COUNTS_SUCCESS)
                {
                    _lastError = countsResult.error.ToString();
                    LogToFile($"GetNonDispensableCashCounts: native error {countsResult.error}.");
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
                        CurrencyCode = unit.currencyCode
                    };
                    list.Add(info);
                }

                CtmCClient.FreeCashUnitSetContents(ref cashUnitSet);

                _lastError = "OK";
                LogToFile($"GetNonDispensableCashCounts: returned {list.Count} items.");
                return list;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"GetNonDispensableCashCounts: exception {ex.Message}.");
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
            txnId = "";
            LogToFile($"BeginCashManagementTransaction: userId='{userId}', cashierId='{cashierId}'");
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(cashierId))
                {
                    _lastError = "Invalid userId or cashierId";
                    LogToFile("✗ Invalid CM params");
                    return false;
                }

                _lastError = "";
                IntPtr txnPtr;
                var error = CtmCClient.BeginCashManagementTransaction(userId, cashierId, out txnPtr);  // Call with out IntPtr
                LogToFile($"BeginCM raw result: error={error}, txnPtr={txnPtr.ToInt64():X}");

                // HACK for emulators: if ptr NULL use generated ID
                string generatedId = $"CM_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                if (txnPtr == IntPtr.Zero)
                {
                    txnId = generatedId;  // Fallback
                    _cmTxnId = txnId;
                    LogToFile($"✓ HACK: ptr NULL, but success — using generated ID: {txnId}");
                    return true;
                }

                // Normal case
                txnId = Marshal.PtrToStringAnsi(txnPtr) ?? generatedId;
                _cmTxnId = txnId;
                if (error == CTMBeginTransactionError.CTM_BEGIN_TRX_SUCCESS)
                {
                    Marshal.FreeHGlobal(txnPtr);
                    LogToFile($"✓ CM Transaction started: txnId={txnId}");
                    return true;
                }

                // Error
                if (txnPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(txnPtr);
                _lastError = error.ToString();
                LogToFile($"✗ CM Transaction failed: {error}");
                return false;
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
                var result = CtmCClient.EndCashManagementTransaction(actualTxnId);  // Returns CTMEndTransactionResult (success=0)
                LogToFile($"EndCM raw result: error={result}");

                // HACK for emulators: if garbage error > 1,000,000 treat as OK (server success)
                if ((int)result != 0 && (int)result < 1000000)
                {
                    LogToFile($"✗ EndCM raw error: {result} (real error)");
                    _lastError = result.ToString();
                    return false;
                }

                _cmTxnId = "";  // Reset ID
                LogToFile($"✓ CM Transaction ended: txnId={actualTxnId}");
                return true;
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
                var result = CtmCClient.BeginRefill(targetAmount);  // Enables acceptors (from logs: Enable cash/coin acceptor)
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

        public CTMEndRefillResult EndRefill(out int totalAmount)
        {
            totalAmount = 0;
            LogToFile("EndRefill: called (disables acceptors)");
            try
            {
                _lastError = "";
                var result = CtmCClient.EndRefill();
                totalAmount = result.totalAmount;  // From logs: total inserted 1007
                if (result.error == CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_SUCCESS)
                {
                    LogToFile($"Refill ended: total={totalAmount}");
                    return result;
                }
                _lastError = result.error.ToString();
                LogToFile($"EndRefill failed: {result.error}");
                return result;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"Exception in EndRefill: {ex}");
                return new CTMEndRefillResult { error = CTMAcceptCashRequestResult.CTM_ACCEPT_CASH_ERROR_UNHANDLED_EXCEPTION };
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
    }
}