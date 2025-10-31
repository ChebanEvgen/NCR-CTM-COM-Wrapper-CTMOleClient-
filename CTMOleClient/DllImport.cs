using System;
using System.Runtime.InteropServices;

namespace CTMOnCSharp
{
    public partial class CtmCClient
    {

        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnCashAcceptCallBack(CTMEventInfo evtInfo, CTMAcceptEvent acceptEvent);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnCashAcceptCompleteCallBack(CTMEventInfo evtInfo);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnDeviceErrorCallBack(CTMEventInfo evtInfo, CTMDeviceError deviceError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnDeviceStatusCallBack(CTMEventInfo evtInfo, CTMDeviceStatus deviceStatus);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnChangeContextCallBack(CTMEventInfo evtInfo, CTMContextEvent context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnAuthenticationCallBack(CTMEventInfo evtInfo, CTMAuthenticationEvent authenticationEvent);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnSocketClosedCallBack(CTMEventInfo evtInfo);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnCMClosedCallBack(CTMEventInfo evtInfo);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_cash_accept_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCashAcceptEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnCashAcceptCallBack cashAcceptCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_cash_accept_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveCashAcceptEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnCashAcceptCallBack cashAcceptCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_cash_accept_complete_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCashAcceptCompleteEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnCashAcceptCompleteCallBack completeCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_cash_accept_complete_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveCashAcceptCompleteEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnCashAcceptCompleteCallBack completeCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_socket_closed_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddSocketClosedEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnSocketClosedCallBack socketClosedCallback);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_socket_closed_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveSocketClosedEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnSocketClosedCallBack socketClosedCallback);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_change_context_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddChangeContextEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnChangeContextCallBack changeContextCallback);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_change_context_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveChangeContextEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnChangeContextCallBack changeContextCallback);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_cm_closed_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCMClosedEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnCMClosedCallBack cmClosedCallback);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_cm_closed_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveCMClosedEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnCMClosedCallBack cmClosedCallback);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_device_error_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddDeviceErrorEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnDeviceErrorCallBack deviceErrorCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_device_error_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveDeviceErrorEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnDeviceErrorCallBack deviceErrorCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_device_status_event_handler", CallingConvention = CallingConvention.Cdecl)]
         public static extern void AddDeviceStatusEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnDeviceStatusCallBack deviceStatusCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_device_status_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveDeviceStatusEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnDeviceStatusCallBack deviceStatusCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_authentication_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddAuthenticationEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnAuthenticationCallBack authenticationCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_remove_authentication_event_handler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveAuthenticationEventHandler([MarshalAs(UnmanagedType.FunctionPtr)] OnAuthenticationCallBack authenticationCallBack);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMInitializationResult Initialize([MarshalAs(UnmanagedType.LPStr)] string filename, [MarshalAs(UnmanagedType.LPStr)] string clientid, CTMClientType clientType);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_dispensable_cash_counts", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetCashCountsResult GetDispensableCashCounts();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_non_dispensable_cash_counts", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetCashCountsResult GetNonDispensableCashCounts();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_config", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetConfigResult GetConfig();


        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_customer_transaction", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMBeginTransactionResult BeginCustomerTransaction([MarshalAs(UnmanagedType.LPStr)] string transactionId);


        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_end_transaction", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMEndTransactionResult EndCustomerTransaction([MarshalAs(UnmanagedType.LPStr)] string transactionId);  // Было CTMBeginTransactionError — смени!

  






        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_uninitialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Uninitialize();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_transaction", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMBeginTransactionResult BeginTransaction([MarshalAs(UnmanagedType.LPStr)] string txnType, [MarshalAs(UnmanagedType.LPStr)] string userId, [MarshalAs(UnmanagedType.LPStr)] string cashierId);  // txnType = "CM" для cash management

  

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_accept_cash", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMAcceptCashRequestResult AcceptCash([MarshalAs(UnmanagedType.U4)] int targetAmount);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_stop_accepting_cash", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMStopAcceptingCashResult StopAcceptingCash();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_dispense_cash", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMDispenseCashResult DispenseCash([MarshalAs(UnmanagedType.U4)] int amountToDispense);



        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_cash_management_transaction", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMBeginTransactionResult BeginCashManagementTransaction([MarshalAs(UnmanagedType.LPStr)] string userId, [MarshalAs(UnmanagedType.LPStr)] string cashierId);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_free_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeString(IntPtr ptr);


       
        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_refill", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMAcceptCashRequestResult BeginRefill(int targetAmount = -1); 

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_end_refill", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMEndRefillResult EndRefill();  

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_dispensable_capacities", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetCapacitiesResult GetDispensableCapacities();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_non_dispensable_coin_capacity", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetCapacitiesResult GetNonDispensableCoinCapacity();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_non_dispensable_note_capacity", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetCapacitiesResult GetNonDispensableNoteCapacity();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_loader_cassette_counts", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetLoaderCassetteCountsResult GetLoaderCassetteCounts();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_loader_cassette_capacity", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetCapacitiesResult GetLoaderCassetteCapacity();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_startup_cm_app", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMStartUpCMAppResult StartUpCMApp();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_cm_receipt_data", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetCMReceiptDataResult GetCMReceiptData();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_cash_management_transaction", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMBeginTransactionResult BeginCashManagementTransaction([MarshalAs(UnmanagedType.LPStr)] string userId, [MarshalAs(UnmanagedType.LPStr)] string cashierId, [MarshalAs(UnmanagedType.LPStr)] string transactionId);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_add_cash_unit_to_cash_unit_set", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AddCashUnitToCashUnitSet([MarshalAs(UnmanagedType.Struct)] ref CTMCashUnitSet cashUnitSet, CTMCashType cashType, [MarshalAs(UnmanagedType.U4)] int denomination, [MarshalAs(UnmanagedType.U4)] int count, [MarshalAs(UnmanagedType.LPStr)] string curencyCode);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_free_cash_unit_set_contents", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeCashUnitSetContents([MarshalAs(UnmanagedType.Struct)] ref CTMCashUnitSet cashUnitSet);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_transfer_from_bin_to_cashbox", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMTransferFromBinToCashboxResult TransferFromBinToCashbox([MarshalAs(UnmanagedType.Struct)] CTMCashUnitSet cashUnitSet);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_transfer_all_from_loader_to_cashbox", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMTransferAllFromLoaderToCashboxResult TransferAllFromLoaderToCashbox();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_transfer_all_notes_to_cashbox", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMTransferAllNotesToCashboxResult TransferAllNotesToCashbox();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_refill", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMAcceptCashRequestResult BeginRefill();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_refill_with_location", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMAcceptCashRequestResult BeginRefillWithLocation(CTMRefillLocation refillLocation);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_dispense_cash_by_denomination", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMDispenseCashResult DispenseCashByDenomination([MarshalAs(UnmanagedType.Struct)] CTMCashUnitSet cashUnitSet);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_reset_counts_dispensable_coins", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMResetCountsResult ResetCountsDispensableCoins();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_reset_counts_non_dispensable_coins", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMResetCountsResult ResetCountsNonDispensableCoins();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_reset_counts_non_dispensable_notes", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMResetCountsResult ResetCountsNonDispensableNotes();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_reset_counts_coin_hoppers", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMResetCountsResult ResetCountsCoinHoppers();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_set_dispensable_counts", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMSetCountsResult SetDispensableCounts([MarshalAs(UnmanagedType.Struct)] CTMCashUnitSet cashUnitSet);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_set_loader_cassette_counts", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMSetCountsResult SetLoaderCassetteCounts([MarshalAs(UnmanagedType.Struct)] CTMCashUnitSet cashUnitSet);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_clear_purged_status", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMClearPurgedStatusResult ClearPurgedStatus();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_purged_status", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetPurgedStatusResult GetPurgedStatus();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_read_failed_note_counts", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMReadFailedNoteCountsResult ReadFailedNoteCounts();
		
		[DllImport("libctmclient-0.dll", EntryPoint = "ctm_read_failed_coin_counts", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMReadFailedCoinCountsResult ReadFailedCoinCounts();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_error_details", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMErrorDetailsResult GetErrorDetails([MarshalAs(UnmanagedType.Struct)] CTMDeviceError deviceError, [MarshalAs(UnmanagedType.LPStr)] string localeId, [MarshalAs(UnmanagedType.Struct)] ref CTMDeviceErrorDetails deviceErrorDetails);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_free_error_details", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeErrorDetails([MarshalAs(UnmanagedType.Struct)] ref CTMDeviceErrorDetails deviceErrorDetails);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_test_device", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMDeviceTestResult TestDevice([MarshalAs(UnmanagedType.Struct)] CTMDeviceInfo deviceInfo);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_test_all_devices", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMDeviceTestResult TestAllDevices();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_get_diag_files", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMGetDiagFilesResult GetDiagFiles();

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_authenticate_cm", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AuthenticateCashManagement(CTMAuthenticateResult authenticationResult);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_upload_cm_data", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMUploadCMDataResult UploadCMData([MarshalAs(UnmanagedType.LPStr)] string cmTransactionId, CTMCMOperationType cmType, [MarshalAs(UnmanagedType.LPStr)] string cmData);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_download_cm_data", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMDownloadCMDataResult DownloadCMData([MarshalAs(UnmanagedType.LPStr)] string cmRequestParam, CTMCMOperationType cmType);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_purge_coins", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMPurgeCoinsResult PurgeCoins(CTMPurgeCoinsLocation purgeCoinsLocation);

        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);





        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_begin_cash_management_transaction", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMBeginTransactionError BeginCashManagementTransaction([MarshalAs(UnmanagedType.LPStr)] string userId, [MarshalAs(UnmanagedType.LPStr)] string cashierId, out IntPtr transactionIdPtr);

        [DllImport("libctmclient-0.dll", EntryPoint = "ctm_end_transaction", CallingConvention = CallingConvention.Cdecl)]
        public static extern CTMEndTransactionResult EndCashManagementTransaction([MarshalAs(UnmanagedType.LPStr)] string transactionId);
     
      
    }
}
