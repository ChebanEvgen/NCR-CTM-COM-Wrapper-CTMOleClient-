using CTMOnCSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using static PaymentChecker;


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

        object TransferAllToCashbox();
        object TransferAllNotesToCashbox_old();
        object TransferFromBinToCashbox(object cashUnitsObj);

        CTMResetCountsResult ResetDispensableCoinCounts();
        CTMResetCountsResult ResetNonDispensableCoinCounts();
        CTMResetCountsResult ResetNonDispensableNoteCounts();
        CTMResetCountsResult ResetCoinHopperCounts();

        object PurgeCoins(CTMPurgeCoinsLocation purgeCoinsLocation);
        object TransferAllNotesToCashbox();
        object DispenseCashByDenomination(object cashUnitsObj);

        string CheckAllPayments(object levelsFrom1C, double maxAmount, out bool success);

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


        public CTMWrapper() : base() { }

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
            LogToFile($"SetConnection: UI Context captured for 1C 8.3 form ({_uiContext.GetType().Name}).");
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
            string assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string buildTime = System.IO.File.GetLastWriteTime(typeof(CTMWrapper).Assembly.Location).ToString("yyyy-MM-dd HH:mm:ss");

            LogToFile($"--- INITIALIZE CALLED --- Assembly Version: {assemblyVersion}, Build Time: {buildTime}");
            LogToFile($"Initialize: called with clientId='{clientId}', overrideHost='{overrideHost ?? "null"}', overridePort='{overridePort ?? "null"}'."); try
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

                bool success = (result == CTMEndTransactionResult.CTM_END_TRX_SUCCESS);

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

        public ArrayList GetNonDispensableCashCounts_old()
        {
            LogToFile("GetNonDispensableCashCounts: called.");

            var list = new ArrayList();
            CTMCashUnitSet cashUnitSet = new CTMCashUnitSet();  // Для ref в finally
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
                cashUnitSet = countsResult.cashUnitSet;  // Сохрани для finally
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
                        Type = (int)unit.type, // 0 = COIN, 1 = NOTE
                        CurrencyCode = unit.currencyCode ?? string.Empty
                    };
                    list.Add(info);
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
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                LogToFile("GetNonDispensableCashCounts: GC.Collect called after free.");
                try
                {
                    if (cashUnitSet.intPtr != IntPtr.Zero)
                    {
                        CtmCClient.FreeCashUnitSetContents(ref cashUnitSet);
                        LogToFile("GetNonDispensableCashCounts: memory freed in finally.");
                    }
                }
                catch (Exception freeEx)
                {
                    LogToFile($"GetNonDispensableCashCounts: free error in finally {freeEx.Message} — memory may leak.");
                }
            }
        }

        public ArrayList GetNonDispensableCashCounts()
        {
            LogToFile("GetNonDispensableCashCounts: called. ClientId=" + _clientId + ", Connected?=" + (_clientId != ""));
            if (string.IsNullOrEmpty(_clientId))
            {
                _lastError = "Not initialized";
                LogToFile("GetNonDispensableCashCounts: FAILED - not initialized");
                return new ArrayList();
            }

            try
            {
                LogToFile("GetNonDispensableCashCounts: Calling native DLL...");
                var result = CtmCClient.GetNonDispensableCashCounts();
                LogToFile("GetNonDispensableCashCounts: Native call returned. Error=" + result.error + ", Count=" + result.cashUnitSet.count);

                ArrayList units = new ArrayList();
                if (result.error == CTMGetCashCountsError.CTM_GET_CASH_COUNTS_SUCCESS && result.cashUnitSet.count > 0)
                {
                    // Парсинг cashUnitSet (добавь, если нужно, по аналогии с другими методами)
                    IntPtr ptr = result.cashUnitSet.intPtr;
                    int size = Marshal.SizeOf(typeof(CTMCashUnit));
                    for (int i = 0; i < result.cashUnitSet.count; i++)
                    {
                        IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                        CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                        // Добавь CashUnitInfo в ArrayList
                        CashUnitInfo info = new CashUnitInfo();
                        info.FromUnmanaged(unit);
                        units.Add(info);
                        LogToFile($"GetNonDispensableCashCounts: Added unit - Denom={unit.denomination}, Count={unit.count}");
                    }
                }
                else
                {
                    _lastError = result.error.ToString();
                    LogToFile("GetNonDispensableCashCounts: FAILED - " + result.error);
                }
                LogToFile("GetNonDispensableCashCounts: Returning " + units.Count + " units. COMPLETE.");
                return units;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile("GetNonDispensableCashCounts: EXCEPTION - " + ex.Message + "\nStack: " + ex.StackTrace);
                return new ArrayList();
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
                int type = (acceptEvent.cashUnit.type == CTMCashType.CTM_CASH_TYPE_NOTE) ? 0 : 1;
                // int type = (int) acceptEvent.cashUnit.type;

                string curr = acceptEvent.cashUnit.currencyCode ?? "USD";

                LogToFile($"CashAccept: Принято: {amount} {curr}, Сумма: {denom}, Итого: {amountDue}");

                if (_eventsEnabled && _uiContext != null && _oneCObject != null)
                {
                    object[] params1C = { (int)amount, (int)amountDue, denom, curr, type };
                    //object[] params1C = { (int)amount, (int)amountDue, denom, curr };
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
            int deviceId = deviceStatus.deviceInfo.deviceId == IntPtr.Zero ? 0 : Marshal.ReadInt32(deviceStatus.deviceInfo.deviceId);


            // Создаем COM-объект для 1C
            DeviceStatusInfo statusObj = new DeviceStatusInfo
            {
                Timestamp = evtInfo.timestamp,
                DeviceType = (int)deviceStatus.deviceInfo.deviceType,
                DeviceId = deviceId,
                DeviceModel = deviceStatus.deviceInfo.deviceModel ?? "N/A",
                DeviceSubModel = deviceStatus.deviceInfo.deviceSubModel ?? "N/A",
                Status = deviceStatus.status
            };

            string logInfo = $"DeviceStatus: Type={statusObj.DeviceType}, ID={statusObj.DeviceId}, Model={statusObj.DeviceModel}, Status={statusObj.Status} ";
            LogToFile(logInfo);
            _deviceStatuses[deviceStatus.deviceInfo.deviceType] = deviceStatus.status;
            LogToFile($"Device {deviceStatus.deviceInfo.deviceType} status updated: {deviceStatus.status} (ready if >0)");
            if (_eventsEnabled && _uiContext != null)
            {
                _uiContext.Post(_ => InvokeOneCEvent("OnDeviceStatus", new object[] { statusObj }), null);
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

        public object TransferAllToCashbox()
        {
            LogToFile($"TransferAllToCashbox: called (CM txn: {_cmTxnId})");
            try
            {
                if (string.IsNullOrEmpty(_cmTxnId))
                {
                    _lastError = "No active CM transaction";
                    LogToFile("✗ TransferAllToCashbox: No CM txn");
                    return new { Success = false, Error = _lastError, TransferredAmount = 0 };
                }

                _lastError = "";
                var result = CtmCClient.TransferAllFromLoaderToCashbox();
                var transferResult = new
                {
                    Success = (result.error == CTMTransferCashError.CTM_TRANSFER_SUCCESS),
                    TransferredAmount = result.transferredCash.transferredAmount,
                    Error = result.error.ToString()
                };

                if (transferResult.Success)
                {
                    LogToFile($"✓ Transferred {transferResult.TransferredAmount} to cashbox");
                }
                else
                {
                    _lastError = transferResult.Error;
                    LogToFile($"✗ TransferAllToCashbox failed: {result.error}");
                }

                return transferResult;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"EX in TransferAllToCashbox: {ex}");
                return new { Success = false, Error = _lastError, TransferredAmount = 0 };
            }
        }

        public object TransferAllNotesToCashbox_old()
        {
            LogToFile($"TransferAllNotesToCashbox: called (CM txn: {_cmTxnId})");
            try
            {
                if (string.IsNullOrEmpty(_cmTxnId))
                {
                    _lastError = "No active CM transaction";
                    LogToFile("✗ TransferAllNotesToCashbox: No CM txn");
                    return new { Success = false, Error = _lastError, TransferredAmount = 0 };
                }

                _lastError = "";
                var result = CtmCClient.TransferAllNotesToCashbox();
                var transferResult = new
                {
                    Success = (result.error == CTMTransferCashError.CTM_TRANSFER_SUCCESS),
                    TransferredAmount = result.transferredCash.transferredAmount,
                    Error = result.error.ToString()
                };

                if (transferResult.Success)
                {
                    LogToFile($"✓ Transferred {transferResult.TransferredAmount} notes to cashbox");
                }
                else
                {
                    _lastError = transferResult.Error;
                    LogToFile($"✗ TransferAllNotesToCashbox failed: {result.error}");
                }

                return transferResult;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"EX in TransferAllNotesToCashbox: {ex}");
                return new { Success = false, Error = _lastError, TransferredAmount = 0 };
            }
        }

        public object TransferFromBinToCashbox(object cashUnitsObj)
        {
            LogToFile($"TransferFromBinToCashbox: called with obj type={cashUnitsObj?.GetType().Name ?? "null"} (CM txn: {_cmTxnId})");
            try
            {
                if (string.IsNullOrEmpty(_cmTxnId))
                {
                    _lastError = "No active CM transaction";
                    LogToFile("✗ TransferFromBinToCashbox: No CM txn");
                    var failResult = new TransferBinResult { Success = false, Error = _lastError };
                    return failResult;
                }

                ArrayList cashUnits = new ArrayList();
                if (cashUnitsObj != null)
                {
                    // Итерация по COM-массиву/объекту (1C Массив как __ComObject)
                    dynamic comArray = cashUnitsObj;
                    foreach (dynamic item in comArray)
                    {
                        // Читаем свойства через dynamic (без каста)
                        int denom = item.Denomination ?? 0;
                        int cnt = item.Count ?? 0;
                        string curr = item.CurrencyCode ?? "USD";
                        int typ = item.Type ?? 0;

                        if (cnt > 0)
                        {
                            var info = new CashUnitInfo { Denomination = denom, Count = cnt, CurrencyCode = curr, Type = typ };
                            cashUnits.Add(info);
                            LogToFile($"Parsed unit: denom={denom}, count={cnt}, type={typ}");
                        }
                    }
                }

                if (cashUnits.Count == 0)
                {
                    _lastError = "Empty cashUnits after parsing";
                    LogToFile("✗ TransferFromBinToCashbox: No units after parse");
                    var failResult = new TransferBinResult { Success = false, Error = _lastError };
                    return failResult;
                }

                // Создаём и заполняем CTMCashUnitSet (без изменений)
                int count = cashUnits.Count;
                int unitSize = Marshal.SizeOf(typeof(CTMCashUnit));
                IntPtr ptr = Marshal.AllocHGlobal(count * unitSize);
                CTMCashUnitSet unitSet = new CTMCashUnitSet { count = count, intPtr = ptr };

                for (int i = 0; i < count; i++)
                {
                    var info = (CashUnitInfo)cashUnits[i];
                    CTMCashUnit unit = new CTMCashUnit
                    {
                        type = (CTMCashType)info.Type,
                        denomination = info.Denomination,
                        count = info.Count,
                        currencyCode = info.CurrencyCode ?? "USD"
                    };
                    IntPtr unitPtr = IntPtr.Add(ptr, i * unitSize);
                    Marshal.StructureToPtr(unit, unitPtr, false);
                    LogToFile($"Unit {i}: type={unit.type}, denom={unit.denomination}, count={unit.count}");
                }

                _lastError = "";
                var result = CtmCClient.TransferFromBinToCashbox(unitSet);
                var transferResult = new TransferBinResult
                {
                    Success = (result.error == CTMTransferCashError.CTM_TRANSFER_SUCCESS),
                    TransferredAmount = result.transferredCash.transferredAmount,
                    Error = result.error.ToString()
                };

                Marshal.FreeHGlobal(ptr);

                if (transferResult.Success)
                {
                    LogToFile($"✓ Transferred {transferResult.TransferredAmount} from bin to cashbox");
                }
                else
                {
                    _lastError = transferResult.Error;
                    LogToFile($"✗ TransferFromBinToCashbox failed: {result.error}");
                }

                return transferResult;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"EX in TransferFromBinToCashbox: {ex}");
                var failResult = new TransferBinResult { Success = false, Error = _lastError };
                return failResult;
            }
        }


        public CTMResetCountsResult ResetDispensableCoinCounts()
        {
            LogToFile("ResetDispensableCoinCounts: called.");
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    _lastError = "Not initialized";
                    LogToFile("ResetDispensableCoinCounts: FAILED - not initialized");
                    return CTMResetCountsResult.CTM_RESET_COUNTS_NOT_CONNECTED;
                }
                var result = CtmCClient.ResetCountsDispensableCoins();
                _lastError = result.ToString();
                LogToFile($"ResetDispensableCoinCounts: result = {result}");
                return result;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"ResetDispensableCoinCounts: EXCEPTION {ex.Message}");
                return CTMResetCountsResult.CTM_RESET_COUNTS_UNHANDLED_EXCEPTION;
            }
        }

        public CTMResetCountsResult ResetNonDispensableCoinCounts()
        {
            LogToFile("ResetNonDispensableCoinCounts: called.");
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    _lastError = "Not initialized";
                    LogToFile("ResetNonDispensableCoinCounts: FAILED - not initialized");
                    return CTMResetCountsResult.CTM_RESET_COUNTS_NOT_CONNECTED;
                }
                var result = CtmCClient.ResetCountsNonDispensableCoins();
                _lastError = result.ToString();
                LogToFile($"ResetNonDispensableCoinCounts: result = {result}");
                return result;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"ResetNonDispensableCoinCounts: EXCEPTION {ex.Message}");
                return CTMResetCountsResult.CTM_RESET_COUNTS_UNHANDLED_EXCEPTION;
            }
        }

        public CTMResetCountsResult ResetNonDispensableNoteCounts()
        {
            LogToFile("ResetNonDispensableNoteCounts: called.");
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    _lastError = "Not initialized";
                    LogToFile("ResetNonDispensableNoteCounts: FAILED - not initialized");
                    return CTMResetCountsResult.CTM_RESET_COUNTS_NOT_CONNECTED;
                }
                var result = CtmCClient.ResetCountsNonDispensableNotes();
                _lastError = result.ToString();
                LogToFile($"ResetNonDispensableNoteCounts: result = {result}");
                return result;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"ResetNonDispensableNoteCounts: EXCEPTION {ex.Message}");
                return CTMResetCountsResult.CTM_RESET_COUNTS_UNHANDLED_EXCEPTION;
            }
        }

        public CTMResetCountsResult ResetCoinHopperCounts()
        {
            LogToFile("ResetCoinHopperCounts: called.");
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    _lastError = "Not initialized";
                    LogToFile("ResetCoinHopperCounts: FAILED - not initialized");
                    return CTMResetCountsResult.CTM_RESET_COUNTS_NOT_CONNECTED;
                }
                var result = CtmCClient.ResetCountsCoinHoppers();
                _lastError = result.ToString();
                LogToFile($"ResetCoinHopperCounts: result = {result}");
                return result;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"ResetCoinHopperCounts: EXCEPTION {ex.Message}");
                return CTMResetCountsResult.CTM_RESET_COUNTS_UNHANDLED_EXCEPTION;
            }
        }

        public object PurgeCoins(CTMPurgeCoinsLocation purgeCoinsLocation)
        {
            LogToFile($"PurgeCoins: called with location={purgeCoinsLocation}.");
            var purgeResult = new PurgeCoinsResult();  // Новый объект
            CTMPurgeCoinsResult nativeResult = new CTMPurgeCoinsResult();  // Для native
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    _lastError = "Not initialized";
                    LogToFile("PurgeCoins: FAILED - not initialized");
                    purgeResult.Error = CTMPurgeCoinsError.CTM_PURGE_COINS_NOT_CONNECTED;
                    return purgeResult;
                }
                LogToFile("PurgeCoins: Calling native DLL...");
                nativeResult = CtmCClient.PurgeCoins(purgeCoinsLocation);
                LogToFile($"PurgeCoins: Native call returned. Error={nativeResult.error}, Count={nativeResult.purgeCoinCounts.count}");

                purgeResult.Error = nativeResult.error;
                purgeResult.Success = (nativeResult.error == CTMPurgeCoinsError.CTM_PURGE_COINS_SUCCESS);

                if (purgeResult.Success && nativeResult.purgeCoinCounts.count > 0)
                {
                    // Парсим в ArrayList для 1C
                    IntPtr ptr = nativeResult.purgeCoinCounts.intPtr;
                    int size = Marshal.SizeOf(typeof(CTMCashUnit));
                    for (int i = 0; i < nativeResult.purgeCoinCounts.count; i++)
                    {
                        IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                        CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                        CashUnitInfo info = new CashUnitInfo();
                        info.FromUnmanaged(unit);
                        purgeResult.PurgedUnits.Add(info);
                        LogToFile($"PurgeCoins: Added unit - Denom={unit.denomination}, Count={unit.count}");
                    }
                }
                else
                {
                    _lastError = nativeResult.error.ToString();
                    LogToFile("PurgeCoins: FAILED - " + nativeResult.error);
                }
                LogToFile("PurgeCoins: COMPLETE.");
                return purgeResult;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"PurgeCoins: EXCEPTION - {ex.Message}\nStack: {ex.StackTrace}");
                purgeResult.Error = CTMPurgeCoinsError.CTM_PURGE_COINS_UNHANDLED_EXCEPTION;
                return purgeResult;
            }
            finally
            {
                if (nativeResult.purgeCoinCounts.intPtr != IntPtr.Zero)
                {
                    CtmCClient.FreeCashUnitSetContents(ref nativeResult.purgeCoinCounts);
                    LogToFile("PurgeCoins: Memory freed in finally.");
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public object TransferAllNotesToCashbox()
        {
            LogToFile("TransferAllNotesToCashbox: called.");
            var transferResult = new TransferAllNotesResult();
            CTMTransferAllNotesToCashboxResult result = new CTMTransferAllNotesToCashboxResult();  // ← Добавь это
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    _lastError = "Not initialized";
                    LogToFile("TransferAllNotesToCashbox: FAILED - not initialized");
                    transferResult.Error = "Not connected";
                    return transferResult;
                }
                LogToFile("TransferAllNotesToCashbox: Calling native DLL...");
                result = CtmCClient.TransferAllNotesToCashbox();  // ← Теперь в scope
                LogToFile($"TransferAllNotesToCashbox: Native result. Error={result.error}, Amount={result.transferredCash.transferredAmount}, Units count={result.transferredCash.cashUnitSet.count}");

                transferResult.Success = (result.error == CTMTransferCashError.CTM_TRANSFER_SUCCESS);
                transferResult.TransferredAmount = result.transferredCash.transferredAmount;
                transferResult.Error = result.error.ToString();

                if (transferResult.Success && result.transferredCash.cashUnitSet.count > 0)
                {
                    IntPtr ptr = result.transferredCash.cashUnitSet.intPtr;
                    int size = Marshal.SizeOf(typeof(CTMCashUnit));
                    for (int i = 0; i < result.transferredCash.cashUnitSet.count; i++)
                    {
                        IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                        CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                        CashUnitInfo info = new CashUnitInfo();
                        info.FromUnmanaged(unit);
                        transferResult.TransferredUnits.Add(info);
                        LogToFile($"TransferAllNotesToCashbox: Added unit - Denom={unit.denomination}, Count={unit.count}");
                    }
                }
                else if (!transferResult.Success)
                {
                    _lastError = transferResult.Error;
                    LogToFile("TransferAllNotesToCashbox: FAILED - " + result.error);
                }
                else
                {
                    LogToFile("TransferAllNotesToCashbox: SUCCESS, no units");
                }
                return transferResult;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"TransferAllNotesToCashbox: EXCEPTION {ex}");
                transferResult.Error = _lastError;
                return transferResult;
            }
            finally
            {
                if (result.transferredCash.cashUnitSet.intPtr != IntPtr.Zero)
                {
                    CtmCClient.FreeCashUnitSetContents(ref result.transferredCash.cashUnitSet);
                    LogToFile("TransferAllNotesToCashbox: Memory freed.");
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public object DispenseCashByDenomination(object cashUnitsObj)
        {
            LogToFile($"DispenseCashByDenomination: called with obj type={cashUnitsObj?.GetType().Name ?? "null"}");
            var dispenseResult = new DispenseCashResult();
            CTMDispenseCashResult nativeResult = new CTMDispenseCashResult();
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    _lastError = "Not initialized";
                    LogToFile("✗ DispenseCashByDenomination: Not initialized");
                    dispenseResult.Error = _lastError;
                    dispenseResult.Success = false;
                    return dispenseResult;
                }

                ArrayList cashUnits = new ArrayList();
                if (cashUnitsObj != null)
                {
                    // Итерация по COM-массиву/объекту (1C Массив как __ComObject)
                    dynamic comArray = cashUnitsObj;
                    foreach (dynamic item in comArray)
                    {
                        // Читаем свойства через dynamic (без каста)
                        int denom = item.Denomination ?? 0;
                        int cnt = item.Count ?? 0;
                        string curr = item.CurrencyCode ?? "UAH";
                        int typ = item.Type ?? 0;
                        if (cnt > 0)
                        {
                            var info = new CashUnitInfo { Denomination = denom, Count = cnt, CurrencyCode = curr, Type = typ };
                            cashUnits.Add(info);
                            LogToFile($"Parsed unit: denom={denom}, count={cnt}, type={typ}");
                        }
                    }
                }
                if (cashUnits.Count == 0)
                {
                    _lastError = "Empty cashUnits after parsing";
                    LogToFile("✗ DispenseCashByDenomination: No units after parse");
                    dispenseResult.Error = _lastError;
                    dispenseResult.Success = false;
                    return dispenseResult;
                }

                // Создаём и заполняем CTMCashUnitSet
                int count = cashUnits.Count;
                int unitSize = Marshal.SizeOf(typeof(CTMCashUnit));
                IntPtr ptr = Marshal.AllocHGlobal(count * unitSize);
                CTMCashUnitSet unitSet = new CTMCashUnitSet { count = count, intPtr = ptr };
                for (int i = 0; i < count; i++)
                {
                    var info = (CashUnitInfo)cashUnits[i];
                    CTMCashUnit unit = new CTMCashUnit
                    {
                        type = (CTMCashType)info.Type,
                        denomination = info.Denomination,
                        count = info.Count,
                        currencyCode = info.CurrencyCode ?? "UAH"
                    };
                    IntPtr unitPtr = IntPtr.Add(ptr, i * unitSize);
                    Marshal.StructureToPtr(unit, unitPtr, false);
                    LogToFile($"Unit {i}: type={unit.type}, denom={unit.denomination}, count={unit.count}");
                }

                _lastError = "";
                nativeResult = CtmCClient.DispenseCashByDenomination(unitSet);
                LogToFile($"DispenseCashByDenomination: Native result. Amount={nativeResult.amountDispensed}, Error={nativeResult.error}, Units count={nativeResult.cashUnitSet.count}");

                dispenseResult.AmountDispensed = (int)nativeResult.amountDispensed;
                dispenseResult.Success = (nativeResult.error == CTMDispenseCashError.CTM_DISPENSE_CASH_SUCCESS);  // Если enum не существует, замените на (nativeResult.error == 0)
                dispenseResult.Error = nativeResult.error.ToString();

                if (dispenseResult.Success && nativeResult.cashUnitSet.count > 0)
                {
                    IntPtr respPtr = nativeResult.cashUnitSet.intPtr;
                    for (int i = 0; i < nativeResult.cashUnitSet.count; i++)
                    {
                        IntPtr itemPtr = IntPtr.Add(respPtr, i * unitSize);
                        CTMCashUnit unit = (CTMCashUnit)Marshal.PtrToStructure(itemPtr, typeof(CTMCashUnit));
                        CashUnitInfo info = new CashUnitInfo();
                        info.FromUnmanaged(unit);
                        dispenseResult.DispensedUnits.Add(info);
                        LogToFile($"DispenseCashByDenomination: Added dispensed unit - Denom={unit.denomination}, Count={unit.count}");
                    }
                }
                else if (!dispenseResult.Success)
                {
                    _lastError = dispenseResult.Error;
                    LogToFile($"✗ DispenseCashByDenomination failed: {nativeResult.error}");
                }
                else
                {
                    LogToFile("✓ DispenseCashByDenomination: SUCCESS, no units");
                }
                return dispenseResult;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                LogToFile($"EX in DispenseCashByDenomination: {ex}");
                dispenseResult.Error = _lastError;
                dispenseResult.Success = false;
                return dispenseResult;
            }
            finally
            {
                if (nativeResult.cashUnitSet.intPtr != IntPtr.Zero)
                {
                    CtmCClient.FreeCashUnitSetContents(ref nativeResult.cashUnitSet);
                    LogToFile("DispenseCashByDenomination: Memory freed.");
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }


        public string CheckAllPayments(object levelsFrom1C, double maxAmount, out bool success)
        {
            success = false;
            var levels = new List<CashLevel>();

            try
            {
                if (levelsFrom1C == null)
                    return "Ошибка: levelsFrom1C = null";

                // --- Универсальный разбор того, что приходит из 1С ---
                IEnumerable enumerable = levelsFrom1C as IEnumerable;
                if (enumerable == null)
                    return "Ошибка: не удалось получить перечисление из levelsFrom1C. Тип: " + levelsFrom1C.GetType().FullName;

                foreach (object item in enumerable)
                {
                    if (item == null) continue;

                    try
                    {
                        dynamic dyn = item;

                        // Пробуем получить свойства value и stored
                        object valObj = dyn.value;
                        object storedObj = dyn.stored;

                        decimal value = Convert.ToDecimal(valObj);
                        int stored = Convert.ToInt32(storedObj);

                        if (value > 0 && stored > 0)
                        {
                            levels.Add(new CashLevel
                            {
                                Value = value,
                                Stored = stored
                            });
                        }
                    }
                    catch
                    {
                        // Если структура пришла в другом виде — пропускаем или логируем
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                return "Ошибка разбора levels: " + ex.Message + " | Тип объекта: " + (levelsFrom1C?.GetType().FullName ?? "null");
            }

            if (levels.Count == 0)
                return "Ошибка: не удалось извлечь ни одного уровня (value, stored)";

            // --- Основная проверка ---
            var result = PaymentChecker.CheckAllAmounts(levels, (decimal)maxAmount);
            success = result.Success;

            if (result.Success)
                return "";

            //return string.Join(";", result.Missing.Select(x => x.ToString("0.0")));
            //return string.Join("; ", result.Missing.Select(x => x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            return string.Join(";", result.Missing.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }


    }

}

public static class PaymentChecker
{
    /// <summary>
    /// Проверяет, можно ли набрать все суммы от 0.50 до maxAmount грн с шагом 0.50
    /// </summary>
    /// <param name="levels">Остатки кассмашины</param>
    /// <param name="maxAmount">Максимальная сумма в грн (по умолчанию 5000)</param>
    /// <returns>
    /// (success, missingAmounts) 
    /// success = true, если все суммы достижимы
    /// </returns>
    public static (bool Success, List<decimal> Missing) CheckAllAmounts(
      IEnumerable<CashLevel> levels,
      decimal maxAmount = 5000m)
    {
        const int Scale = 100;   // копейки
        const int Step = 50;     // 0.5 грн

        int requestedMaxSum = (int)(maxAmount * Scale);
        requestedMaxSum = (requestedMaxSum / Step) * Step; // выравниваем вниз

        // Реальная сумма денег в кассе
        long totalMoney = 0;
        foreach (var lvl in levels)
        {
            int nom = (int)Math.Round(lvl.Value * Scale);
            if (nom > 0 && lvl.Stored > 0)
                totalMoney += (long)nom * lvl.Stored;
        }

        // DP делаем только до min(requested, totalMoney)
        int dpMaxSum = (int)Math.Min(requestedMaxSum, totalMoney);
        dpMaxSum = (dpMaxSum / Step) * Step;

        int size = dpMaxSum / Step + 1;
        bool[] dp = new bool[size];
        if (size > 0) dp[0] = true;

        // Binary Split
        var virtualCoins = new List<int>();

        foreach (var lvl in levels)
        {
            int nom = (int)Math.Round(lvl.Value * Scale);
            int count = lvl.Stored;

            if (nom <= 0 || count <= 0) continue;

            int power = 1;
            while (count > 0)
            {
                int take = Math.Min(power, count);
                int virt = nom * take;

                if (virt <= dpMaxSum && virt % Step == 0)
                    virtualCoins.Add(virt);

                count -= take;
                power <<= 1;
            }
        }

        // 0-1 рюкзак
        foreach (int coin in virtualCoins)
        {
            int coinSteps = coin / Step;
            for (int s = size - 1; s >= coinSteps; s--)
            {
                if (dp[s - coinSteps])
                    dp[s] = true;
            }
        }

        // Собираем недостающие на всём запрошенном диапазоне
        var missing = new List<decimal>();

        // 1. То, что не набралось внутри реальных денег
        for (int i = 1; i < size; i++)
        {
            if (!dp[i])
                missing.Add(i * 0.5m);
        }

        // 2. Всё, что выше реальной суммы денег — тоже недостающее
        int firstMissingAbove = (dpMaxSum / Step) + 1;
        int lastRequested = requestedMaxSum / Step;

        for (int i = firstMissingAbove; i <= lastRequested; i++)
        {
            missing.Add(i * 0.5m);
        }

        return (missing.Count == 0, missing);
    }

    public class CashLevel
        {
            public decimal Value { get; set; }   // номинал в грн (например 0.5, 1, 2, 5...)
            public int Stored { get; set; }      // количество
        }

}