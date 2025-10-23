using System;
using System.Runtime.InteropServices;

namespace CTMOnCSharp
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMGetConfigResult
    {
        public CTMConfiguration config;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMConfiguration
    {
        public int count;
        public IntPtr intPtr;
        public CTMConfigurationKeyValue keyValue
        {
            get
            {
                return (CTMConfigurationKeyValue)Marshal.PtrToStructure(intPtr, typeof(CTMConfigurationKeyValue));
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMConfigurationKeyValue
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string key;

        [MarshalAs(UnmanagedType.LPStr)]
        public string value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMStartUpCMAppResult
    {
        public CTMStartUpCMAppError error;
    };


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct BeginCustomerTransactionResult
    {
        public CTMBeginTransactionError error;
        [MarshalAs(UnmanagedType.LPStr)]
        public IntPtr transactionId;
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMDispenseCashResult
    {
        [MarshalAs(UnmanagedType.I4)]
        public UInt32 amountDispensed;

        public CTMCashUnitSet cashUnitSet;

        public CTMDispenseCashError error;

        public IntPtr intPtr;
        public CTMDeviceError deviceError
        {
            get
            {
                return (CTMDeviceError)Marshal.PtrToStructure(intPtr, typeof(CTMDeviceError));
            }
        }
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMBeginCashManagementTransactionRequest
    {
        [MarshalAs(UnmanagedType.LPStr)] public string userId;
        [MarshalAs(UnmanagedType.LPStr)] public string cashierId;
        public CTMBeginTransactionResult result;  
    };


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    [ComVisible(true)]
    public struct CTMEndRefillResult
    {
        public int totalAmount;
        public CTMAcceptCashRequestResult error;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct CTMEventInfo
    {
        [MarshalAs(UnmanagedType.I4)]
        public UInt32 timestamp;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct CTMAcceptEvent
    {
        [MarshalAs(UnmanagedType.U4)]
        public UInt32 amount;

        [MarshalAs(UnmanagedType.U4)]
        public UInt32 amountDue;

        public CTMCashUnit cashUnit;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct CTMCashUnit
    {
        public CTMCashType type;

        [MarshalAs(UnmanagedType.I4)]
        public Int32 denomination;

        [MarshalAs(UnmanagedType.I4)]
        public Int32 count;

        [MarshalAs(UnmanagedType.LPStr)]
        public string currencyCode;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMCashUnitSet
    {
        public int count;

        public IntPtr intPtr;

        public CTMCashUnit cashUnit
        {
            get
            {
                return (CTMCashUnit)Marshal.PtrToStructure(intPtr, typeof(CTMCashUnit));
            }
        }
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMGetCashCountsResult
    {
        public CTMCashUnitSet cashUnitSet;

        public CTMGetCashCountsError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMGetCapacitiesResult
    {
        public CTMCashUnitSet dispensableCapacities;

        [MarshalAs(UnmanagedType.U4)]
        public UInt32 nonDispensableNoteCapacity;

        [MarshalAs(UnmanagedType.U4)]
        public UInt32 nonDispensableCoinCapacity;

        [MarshalAs(UnmanagedType.U4)]
        public UInt32 loaderCassetteCapacity;

        public CTMGetCapacitiesError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMGetLoaderCassetteCountsResult
    {
        public CTMCashUnitSet loaderCassetteCounts;

        public CTMGetLoaderCassetteCountsError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMPurgeCoinsResult
    {
        public CTMCashUnitSet purgeCoinCounts;

        public CTMPurgeCoinsError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMReadFailedNoteCountsResult
    {
        public int failedNoteCounts;
    };
	
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMReadFailedCoinCountsResult
    {
        public int failedCoinCounts;

        public CTMGetCashCountsError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMTransferFromBinToCashboxResult
    {
        public CTMTransferredCash transferredCash;
        public CTMTransferCashError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMTransferAllFromLoaderToCashboxResult 
    {
        public CTMTransferredCash transferredCash;
        public CTMTransferCashError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMTransferAllNotesToCashboxResult
    {
        public CTMTransferredCash transferredCash;
        public CTMTransferCashError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMTransferredCash
    {
        [MarshalAs(UnmanagedType.I4)]
        public UInt32 transferredAmount;

        public CTMCashUnitSet cashUnitSet;
        public CTMCashTransferLocation source;
        public CTMCashTransferLocation destination;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMDeviceErrorDetails
    {
        public IntPtr intPtr;
        public string title
        {
            get { return Marshal.PtrToStringAnsi(intPtr); }
        }

        public IntPtr intPtr2;
        public string subtitle
        {
            get { return Marshal.PtrToStringAnsi(intPtr2); }
        }

        public IntPtr intPtr3;
        public string instructionalText
        {
            get { return Marshal.PtrToStringAnsi(intPtr3); }
        }

        public IntPtr intPtr4;
        public string imageFileName
        {
            get { return Marshal.PtrToStringAnsi(intPtr4); }
        }

        public IntPtr intPtr5;
        public string videoFileName
        {
            get { return Marshal.PtrToStringAnsi(intPtr5); }
        }

        public IntPtr intPtr6;
        public string errorCode
        {
            get { return Marshal.PtrToStringAnsi(intPtr6); }
        }
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CTMDeviceError
    {
        public CTMDeviceInfo deviceInfo;

        public int resultCode;

        public int extendedResultCode;

        public IntPtr denomination;

        public IntPtr changeDue;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CTMDeviceStatus
 	{
 	    public CTMDeviceInfo deviceInfo;
 	
 	    public int status;
 	};

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CTMAuthenticationRequest
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string cmId;

        [MarshalAs(UnmanagedType.LPStr)]
        public string cmPassword;
    }
 	
 	[StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CTMDeviceInfo
    {
        public CTMDeviceType deviceType;

        [MarshalAs(UnmanagedType.LPStr)]
        public string deviceModel;

        [MarshalAs(UnmanagedType.LPStr)]
        public string deviceSubModel;

        public IntPtr deviceId;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CTMContextEvent
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string context;

        [MarshalAs(UnmanagedType.LPStr)]
        public string clientOwner;

        public IntPtr amountDue;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMDeviceTestResult 
    {
        public CTMDeviceErrorSet deviceErrorSet;
        public CTMDeviceTestError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMDeviceErrorSet 
    {
        public int count;

        public IntPtr intPtr;
        public CTMDeviceError deviceError
        {
            get
            {
                return (CTMDeviceError)Marshal.PtrToStructure(intPtr, typeof(CTMDeviceError));
            }
        }
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMCMOperationResult
    {
        public IntPtr intPtr;
        public string description
        {
            get { return Marshal.PtrToStringAnsi(intPtr); }
        }

        public CTMCMErrorCode error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMUploadCMDataResult
    {
        public CTMCMOperationResult result;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMDownloadCMDataResult
    {
        public IntPtr intPtr;
        public string data
        {
            get { return Marshal.PtrToStringAnsi(intPtr); }
        }

        public CTMCMOperationResult result;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMGetDiagFilesResult
    {
        public CTMGetDiagFilesError error;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMReceiptFile
    {
        public IntPtr intPtr;
        public string receiptFileName
        {
            get { return Marshal.PtrToStringAnsi(intPtr); }
        }

        public IntPtr intPtr2;
        public string receiptFileContent
        {
            get { return Marshal.PtrToStringAnsi(intPtr2); }
        }
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMGetCMReceiptDataResult
    {
        public CTMReceiptFile receiptFile;
        public CTMGetCMReceiptDataStatus status;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CTMAuthenticationEvent
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string cmUsername;

        [MarshalAs(UnmanagedType.LPStr)]
        public string cmPassword;

        public CTMBoolean isHCashier;
    };

}