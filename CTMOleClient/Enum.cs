using System;
using System.Runtime.InteropServices;

namespace CTMOnCSharp
{
    [ComVisible(true)]
    public enum Mode
    {
        INITIALIZE,
        BEGIN_CUSTOMER_TRANSACTION,
        END_CUSTOMER_TRANSACTION,
        BEGIN_CASH_MANAGEMENT_TRANSACTION,
        END_CASH_MANAGEMENT_TRANSACTION,
        ACCEPT_CASH,
        BEGIN_REFILL,
        DISPENSE_CHANGE,
        SET_DISPENSABLE_COUNTS,
        SET_LOADER_CASSETTE_COUNTS,
        DISPENSE_CASH_BY_DENOMINATION,
        TRANSFER_FROM_BIN_TO_CASHBOX,
        PURGE_COINS,
        CM_CREDENTIALS,
        INVOKE_CM_APPLICATION,
        DEFAULT
    };

    [ComVisible(true)]
    public enum HardwareType
    {
        R5,
        R6
    }

    [ComVisible(true)]
    public enum CTMInitializationResult
    {
        /** The CTM client successfully connected to the CTM service. */
        CTM_INIT_SUCCESS,
        /** A standard C library call failed and populated errno.h's errno. */
        CTM_INIT_ERROR_RUNTIME,
        /** The CTM client could not parse the service URL parameter. */
        CTM_INIT_ERROR_INVALID_SERVICE_URL,
        /** The CTM client could not connect to the CTM service. */
        CTM_INIT_ERROR_COULD_NOT_CONNECT,
        /** The CTM client is already initialized. */
        CTM_INIT_ERROR_ALREADY_INITIALIZED,
        /** The CTM client connection request was rejected by the CTM service. */
        CTM_INIT_ERROR_REJECTED_BY_SERVICE,
        /** The CTM service reports that at least one device cannot be claimed. */
        CTM_INIT_ERROR_AT_LEAST_ONE_DEVICE_NOT_READY,
        /** You must supply a callback to ctm_add_system_message_event_handler(). */
        CTM_INIT_ERROR_MISSING_SYSTEM_MESSAGE_EVENT_HANDLER,
        /** You must supply a callback to ctm_add_device_error_event_handler(). */
        CTM_INIT_ERROR_MISSING_DEVICE_ERROR_EVENT_HANDLER,
        /** The client id that was sent to service already exists. */
        CTM_INIT_ERROR_CLIENT_ID_ALREADY_EXISTS,
        /** Only one PSX Client can be connected to the CTM Service. */
        CTM_INIT_ERROR_PSX_CLIENT_ALREADY_CONNECTED,
        /** Only one CM Client can be connected to the CTM Service. */
        CTM_INIT_ERROR_CM_CLIENT_ALREADY_CONNECTED,
        /** An unhandled exception occurred. */
        CTM_INIT_ERROR_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMStartUpCMAppError
    {
        /** The CTM Service was successful in starting up the Cash Management Application. */
        CTM_STARTUP_CM_SUCCESS = 0,

        /** The client is not connected to the CTM Service. */
        CTM_STARTUP_CM_NOT_CONNECTED,

        /** An invalid path location to CMApplication.exe was configured in CTMBaseOpts.dat .*/
        CTM_STARTUP_CM_INVALID_CM_LOCATION,

        /** The path location to CMApplication.exe is missing from or wasn't configured in CTMBaseOpts.dat .*/
        CTM_STARTUP_CM_LOCATION_NOT_FOUND,

        /** CMApplication.exe is already running. */
        CTM_STARTUP_CM_IS_RUNNING,

        /** An unhandled exception occurred. */
        CTM_STARTUP_CM_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMBeginTransactionError
    {
        CTM_BEGIN_TRX_SUCCESS,
        CTM_BEGIN_TRX_ERROR_ALREADY_IN_PROGRESS,
        CTM_BEGIN_TRX_ERROR_NOT_CONNECTED,
        CTM_BEGIN_TRX_ERROR_UNHANDLED_EXCEPTION = 99
    }

  
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    [ComVisible(true)]
    public struct CTMBeginTransactionResult
    {
        public IntPtr intPtr;
        public string transactionId
        {
            get { return Marshal.PtrToStringAnsi(intPtr); }
        }

        public CTMBeginTransactionError error;
    };








    [ComVisible(true)]
    public enum CTMEndTransactionResult
    {
        /** The transaction has ended. */
        CTM_END_TRX_SUCCESS,
        /** There is no transaction in progress. */
        CTM_END_TRX_ERROR_NO_TRANSACTION_IN_PROGRESS,
        /** You must supply a non-empty transaction ID string. */
        CTM_END_TRX_ERROR_MUST_SUPPLY_TRANSACTION_ID,
        /** The client is not connected to the CTM service. */
        CTM_END_TRX_ERROR_NOT_CONNECTED,
        /** Default value **/
        CTM_END_TRX_DEFAULT,
        /** An unhandled exception occurred. */
        CTM_END_TRX_ERROR_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMAcceptCashRequestResult
    {
        CTM_ACCEPT_CASH_SUCCESS,
        CTM_ACCEPT_CASH_ERROR_NEEDS_OPEN_TRANSACTION_ID,
        CTM_ACCEPT_CASH_ERROR_ALREADY_IN_PROGRESS,
        CTM_ACCEPT_CASH_ERROR_NOT_CONNECTED,
        CTM_ACCEPT_CASH_ERROR_COIN_REFILL_LOCATION_NOT_SUPPORTED,
        CTM_ACCEPT_CASH_ERROR_TARGET_AMOUNT_INVALID,
        CTM_ACCEPT_CASH_ERROR_UNHANDLED_EXCEPTION = 99
    };
    
    [ComVisible(true)]
    public enum CTMCMAppAuthenticationType
    {
        CTM_CM_ATN_TYPE_START_UP,
        CTM_CM_ATN_TYPE_DENY,
        CTM_CM_ATN_TYPE_APPROVE
    }
    
    [ComVisible(true)]
    public enum CTMStopAcceptingCashResult
    {
        CTM_STOP_ACCEPTING_CASH_SUCCESS,
        CTM_STOP_ACCEPTING_CASH_ERROR_NO_DEPOSIT_IN_PROGRESS,
        CTM_STOP_ACCEPTING_CASH_ERROR_NOT_CONNECTED,
        CTM_STOP_ACCEPTING_CASH_ERROR_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMRefillLocation
    {
        CTM_REFILL_COINS_THROUGH_VALIDATOR = 1,    
        CTM_REFILL_NOTES_THROUGH_VALIDATOR = 2,    
        CTM_REFILL_COINS_THROUGH_COIN_CHUTE = 3   
    };
    
    [ComVisible(true)]
    public enum CTMCashType
    {
        CTM_CASH_TYPE_NOTE,
        CTM_CASH_TYPE_COIN
    };

    [ComVisible(true)]
    public enum CTMPurgeCoinsError
    {
        CTM_PURGE_COINS_SUCCESS,
        CTM_PURGE_COINS_NEEDS_OPEN_CASH_MANAGEMENT_TRX_ID,
        CTM_PURGE_COINS_NOT_CONNECTED,
        CTM_PURGE_COINS_DEVICE_IN_ERROR,
        CTM_PURGE_COINS_LOCATION_NOT_SUPPORTED,
        CTM_PURGE_COINS_ECASH_EXIT_FULL,
        CTM_PURGE_COINS_NO_COINS_TO_PURGE,
        CTM_PURGE_COINS_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMPurgeCoinsLocation
    {
        CTM_PURGE_COINS_LOCATION_ALL,
        CTM_PURGE_COINS_LOCATION_HOPPERS,
        CTM_PURGE_COINS_LOCATION_CASHBOX
    };

    [ComVisible(true)]
    public enum CTMDispenseCashError
    {
        CTM_DISPENSE_CASH_SUCCESS,
        CTM_DISPENSE_CASH_ERROR_NEEDS_OPEN_TRANSACTION_ID,
        CTM_DISPENSE_CASH_ERROR_CANNOT_BE_NEGATIVE,
        CTM_DISPENSE_CASH_ERROR_INVALID_DENOMINATION,
        CTM_DISPENSE_CASH_ERROR_EXCEEDS_CONFIGURABLE_MAXIMUM,
        CTM_DISPENSE_CASH_ERROR_NOT_CONNECTED,
        CTM_DISPENSE_CASH_ERROR_DEVICE_IN_ERROR,
        CTM_DISPENSE_CASH_ERROR_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMGetCashCountsError
    {
        /** The CTM Service was able to get cash counts. */
        CTM_GET_CASH_COUNTS_SUCCESS,
        /** The device is not present. */
        CTM_GET_CASH_COUNTS_DEVICE_IN_ERROR,
        /** The client is not connected to the CTM Service. */
        CTM_GET_CASH_COUNTS_NOT_CONNECTED,
        /** The device is not supported. */
        CTM_GET_CASH_COUNT_DEVICE_NOT_SUPPORTED,
        /** An unhandled exception occurred. */
        CTM_GET_CASH_COUNTS_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMGetLoaderCassetteCountsError
    {
        /** The CTM Service was able to get loader cassette counts. */
        CTM_GETLOADERCOUNTS_SUCCESS = 0,
        /** There is a mismatch in the protocol version. */
        CTM_GETLOADERCOUNTS_NOT_CONNECTED,
        /** There is no begin cash management transaction id. */
        CTM_GETLOADERCOUNTS_NEEDS_OPEN_CASH_MANAGEMENT_TRX_ID,
        /** The device is not present. */
        CTM_GETLOADERCOUNTS_DEVICE_NOT_PRESENT,
        /** "No loader" option is configured. */
        CTM_GETLOADERCOUNTS_NO_LOADER_CONFIGURED,
        /** An unhandled exception occurred. */
        CTM_GETLOADERCOUNTS_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMTransferCashError
    {
        /** The CTM Service was able to transfer cash. */
        CTM_TRANSFER_SUCCESS,
        /** There is no begin cash management transaction id. */
        CTM_TRANSFER_NEEDS_OPEN_CASH_MANAGEMENT_TRX_ID,
        /** The amount specified is a negative number. */
        CTM_TRANSFER_AMOUNT_CANNOT_BE_NEGATIVE,
        /** The denomination specified is an invalid denomination. */
        CTM_TRANSFER_AMOUNT_INVALID_DENOMINATION,
        /** The amount specified exceeds the maximum limit. */
        CTM_TRANSFER_AMOUNT_EXCEEDS_CONFIGURABLE_MAX,
        /** The device is configured with no loader */
        CTM_TRANSFER_NO_LOADER_CONFIGURED,
        /** The client is not connected to the CTM Service. */
        CTM_TRANSFER_NOT_CONNECTED,
        /** The CTM Service reported at least one device error */
        CTM_TRANSFER_DEVICE_IN_ERROR,
        /** An unhandled exception occurred. */
        CTM_TRANSFER_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMCashTransferLocation
    {
        CTM_CASH_TRANSFER_LOCATION_NONE = 0,
        /**
         * The loader cassette. This is where cash is stored prior to being
         * transferred to individual dispenser bins.
         */
        CTM_CASH_TRANSFER_LOCATION_LOADER = 1,
        /**
         * The cash box. This is where bills that cannot be transferred to an
         * individual dispenser bin go. In addition, cashiers can remove bills from
         * the dispenser bins by transferring the bills from the dispenser bins to
         * the cash box and removing the cash box cassette from the machine.
         */
        CTM_CASH_TRANSFER_LOCATION_CASHBOX = 2,
        /**
         * An individual dispenser bin. The cash devices are able to dispense cash
         * stored in the dispenser bins.
         */
        CTM_CASH_TRANSFER_LOCATION_INDIVIDUAL_BIN = 3
    };

    [ComVisible(true)]
    public enum CTMResetCountsResult
    {
        /** The CTM Service was able to set loader cassette and dispensable counts. */
        CTM_RESET_COUNTS_SUCCESS,
        /** There is no begin cash management transaction id. */
        CTM_RESET_COUNTS_NEEDS_OPEN_CASH_MANAGEMENT_TRX_ID,
        /** The device is not present. */
        CTM_RESET_COUNTS_DEVICE_NOT_PRESENT,
        /** The client is not connected to the CTM Service. */
        CTM_RESET_COUNTS_NOT_CONNECTED,
        /** An unhandled exception occurred. */
        CTM_RESET_COUNTS_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMSetCountsResult
    {
        /** The CTM Service was able to set loader cassette and dispensable counts. */
        CTM_SET_COUNTS_SUCCESS,
        /** There is no begin cash management transaction id. */
        CTM_SET_COUNTS_NEEDS_OPEN_CASH_MANAGEMENT_TRX_ID,
        /** The amount specified is a negative number. */
        CTM_SET_COUNTS_AMOUNT_CANNOT_BE_NEGATIVE,
        /** The denomination specified is an invalid denomination. */
        CTM_SET_COUNTS_AMOUNT_INVALID_DENOMINATION,
        /** The amount specified exceeds the maximum limit. */
        CTM_SET_COUNTS_AMOUNT_EXCEEDS_CONFIGURABLE_MAX,
        /** The device is not present. */
        CTM_SET_COUNTS_DEVICE_NOT_PRESENT,
        /** "No loader" option is configured. */
        CTM_SET_COUNTS_NO_LOADER_CONFIGURED,
        /** The client is not connected to the CTM Service. */
        CTM_SET_COUNTS_NOT_CONNECTED,
        /** An unhandled exception occurred. */
        CTM_SET_COUNTS_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMClearPurgedStatusResult
    {
        /** The CTM Service was able to clear purge bills. */
        CTM_CLEAR_PURGED_STATUS_SUCCESS,
        /** The CTM Service was not able to clear purge bills. */
        CTM_CLEAR_PURGED_STATUS_ERROR_RUNTIME,
        /** The client is not connected to the CTM Service. */
        CTM_CLEAR_PURGED_STATUS_NOT_CONNECTED
    };

    [ComVisible(true)]
    public enum CTMGetPurgedStatusResult
    {
        /** The CTM Service was able to detect purge bills. */
        CTM_GET_PURGED_STATUS_PURGE_BIN_CONTAINS_NOTES,
        /** The CTM Service was not able to detect purge bills. */
        CTM_GET_PURGED_STATUS_PURGE_BIN_DOESNT_CONTAINS_NOTES,
        /** The client is not connected to the CTM Service. */
        CTM_GET_PURGED_STATUS_NOT_CONNECTED
    };

    [ComVisible(true)]
    public enum CTMErrorDetailsResult
    {
        CTM_ERROR_DETAILS_SUCCESS = 0,
        CTM_ERROR_DETAILS_UNKNOWN_DEVICE_TYPE = 1,
        CTM_ERROR_DETAILS_NOT_INITIALIZED = -1,
        CTM_ERROR_DETAILS_ERROR_DETAILS_STRUCT_IS_NULL = -2,
        CTM_ERROR_DETAILS_ERROR_DETAILS_STRUCT_CONTAINS_DATA = -3,
        CTM_ERROR_DETAILS_ERROR_STRUCT_IS_INVALID = -4,
        CTM_ERROR_DETAILS_NO_DATA_RECEIVED = -5,
        /** A standard C library call failed and populated errno.h's errno. */
        CTM_ERROR_DETAILS_RUNTIME_ERROR = -6
    };

    [ComVisible(true)]
    public enum CTMDeviceType
    {
        /* CTM_DEVICETYPE_CASHCHANGER = 5,
         CTM_DEVICETYPE_CASHACCEPTOR = 15,
         CTM_DEVICETYPE_COINACCEPTOR = 16,
         CTM_DEVICETYPE_OTHER = 17*/
        CTM_DEVICETYPE_RECEIPT = 0,
        CTM_DEVICETYPE_JOURNAL = 1,
        CTM_DEVICETYPE_SLIP = 2,
        CTM_DEVICETYPE_PRINTER = 3,
        CTM_DEVICETYPE_CASHDRAWER = 4,
        CTM_DEVICETYPE_CASHCHANGER = 5,
        CTM_DEVICETYPE_KEYLOCK = 6,
        CTM_DEVICETYPE_LINEDISPLAY = 7,
        CTM_DEVICETYPE_MICR = 8,
        CTM_DEVICETYPE_MSR = 9,
        CTM_DEVICETYPE_SCALE = 10,
        CTM_DEVICETYPE_SCANNER = 11,
        CTM_DEVICETYPE_SIGCAP = 12,
        CTM_DEVICETYPE_MISC = 13,
        CTM_DEVICETYPE_ENCRYPTOR = 14,
        CTM_DEVICETYPE_CASHACCEPTOR = 15,
        CTM_DEVICETYPE_COINACCEPTOR = 16,
        CTM_DEVICETYPE_OTHER = 17,
        CTM_DEVICETYPE_MOTIONSENSOR = 18,
        CTM_DEVICETYPE_EAS = 19,
        CTM_DEVICETYPE_RECYCLER = 20,
        CTM_DEVICETYPE_CARDDISPENSER = 21,
        CTM_DEVICETYPE_MAX_CLASSES = 22
    };

    [ComVisible(true)]
    public enum CTMDeviceTestError
    {
        /** The CTM service was able to test the devices. */
        CTM_DEVICE_TEST_SUCCESS = 0,
        /** An invalid identifier was supplied for a device test request. */
        CTM_DEVICE_TEST_ERROR_UNKNOWN_DEVICE_TYPE,
        /** The client is not connected to the CTM service. */
        CTM_DEVICE_TEST_ERROR_NOT_CONNECTED
    };

    [ComVisible(true)]
    public enum CTMGetDiagFilesError
    {
        /** The CTM Service was successful in starting up the GetDiagFiles app */
        CTM_GET_DIAG_FILES_SUCCESS = 0,

        /** An invalid path location to GetDiagFiles app was configured in CTMBaseOpts.dat .*/
        CTM_GET_DIAG_FILES_INVALID_LOCATION,

        /** The path location to GetDiagFiles app is missing from or wasn't configured in CTMBaseOpts.dat .*/
        CTM_GET_DIAG_FILES_LOCATION_NOT_FOUND,

        /** GetDiagFiles app is already running. */
        CTM_GET_DIAG_FILES_IS_RUNNING,

        /** The client is not connected to the CTM Service. */
        CTM_GET_DIAG_FILES_NOT_CONNECTED,

        /** An unhandled exception occurred. */
        CTM_GET_DIAG_FILES_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMGetCMReceiptDataStatus
    {
        /** The CTM Service was successful in getting CM Receipt data. */
        CTM_GET_CM_RECEIPT_DATA_SUCCESS = 0,

        /** The path location to CM receipts is missing from or wasn't configured in CTMBaseOpts.dat. */
        CTM_GET_CM_RECEIPT_DATA_LOCATION_NOT_FOUND,

        /** The receipt file does not exist. */
        CTM_GET_CM_RECEIPT_DATA_FILE_NOT_FOUND,

        /** A User or Cash Management Transaction is in progress. */
        CTM_GET_CM_RECEIPT_DATA_TRANSACTION_IN_PROGRESS,

        /** Another CM Receipt Data request is already in progress. */
        CTM_GET_CM_RECEIPT_DATA_REQUEST_ALREADY_IN_PROGRESS,

        /** The client is not connected to the CTM Service. */
        CTM_GET_CM_RECEIPT_DATA_NOT_CONNECTED,

        /** An unhandled exception occurred. */
        CTM_GET_CM_RECEIPT_DATA_UNHANDLED_EXCEPTION = 99
    };

    [ComVisible(true)]
    public enum CTMCMOperationType
    {

        CTM_CM_NONE = 0,
        CTM_CM_LOAN = 1, 
        CTM_CM_PICKUP = 2, 
        CTM_CM_BALANCE = 3, 
        CTM_CM_REFILL = 4,
        CTM_CM_OPERATOR,
        CTM_CM_HCASHIER,
        CTM_CM_PRINTER_DATA,
        CTM_CM_SEND_TBCMD,
        CTM_CM_REPORT_DATA
    };

    [ComVisible(true)]
    public enum CTMCMErrorCode
    {
        CTM_CM_SUCCESS = 0,
        CTM_CM_FAILURE
    };

    [ComVisible(true)]
    public enum CTMGetCapacitiesError
    {
        CTM_GET_CAPACITIES_NO_LOADER_CONFIGURED
    };

    [ComVisible(true)]
    public enum CTMClientType
    {
        CTM_POS = 0,
        CTM_PSX,
        CTM_CM,
        CTM_NULL_TYPE
    };

    [ComVisible(true)]
    public enum CTMBoolean
    {
        CTM_FALSE = 0,
        CTM_TRUE
    }

    [ComVisible(true)]
    public enum CTMAuthenticateResult
    {
        CTM_AUTHENTICATION_USERNAME_IS_INCORRECT = 0,
        CTM_AUTHENTICATION_PASSWORD_IS_INCORRECT,
        CTM_AUTHENTICATE_SUCCESS = 99
    }

}