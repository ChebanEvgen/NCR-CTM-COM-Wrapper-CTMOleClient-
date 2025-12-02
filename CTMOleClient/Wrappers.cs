using CTMOnCSharp;
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace CTMOleClient
{
    [ComVisible(true)]
    [Guid("CCED29B4-D6EA-47A5-A47D-A32C1A7AA11F")]
    [ProgId("CTMOleClient.CashUnitInfo")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CashUnitInfo
    {
        public int Denomination { get; set; } = 0;
        public int Count { get; set; } = 0;
        public string CurrencyCode { get; set; } = string.Empty;
        public int Type { get; set; } = 0;
        public CashUnitInfo() { }

        // Добавим метод для копирования из unmanaged (если нужно в CTMWrapper)
        public void FromUnmanaged(CTMOnCSharp.CTMCashUnit unit)
        {
            Denomination = unit.denomination;
            Count = unit.count;
            CurrencyCode = unit.currencyCode ?? string.Empty;
            Type = (int)unit.type;
        }
    }

    [ComVisible(true)]
    [Guid("23228CED-717C-430D-A0FC-0140E2C3C71F")]
    [ProgId("CTMOleClient.DispenseCashResult")]
    [ClassInterface(ClassInterfaceType.None)]
    public class DispenseCashResult
    {
        public int AmountDispensed { get; set; } = 0;
        public string Error { get; set; } = "OK";
        public ArrayList DispensedUnits { get; set; } = new ArrayList();
        public bool Success { get; set; } = false;
        public string DeviceError { get; set; } = "";

        public DispenseCashResult() { }

        // Добавим метод для заполнения из unmanaged
        public void FromUnmanaged(CTMOnCSharp.CTMDispenseCashResult result)
        {
            AmountDispensed = (int)result.amountDispensed;
            Success = ((int)result.error == 0);  
            Error = result.error.ToString();
            if (result.intPtr != IntPtr.Zero)
            {
                // Пример: парсим deviceError, если нужно
                DeviceError = result.deviceError.ToString();
            }
            // Заполни DispensedUnits из cashUnitSet (реализуй позже, если используешь)
        }
    }

    [ComVisible(true)]
    [Guid("F535A143-FA89-4677-99B7-4F52A884AA8E")]
    [ProgId("CTMOleClient.ConfigInfo")]
    [ClassInterface(ClassInterfaceType.None)]
    public class ConfigInfo
    {
        public string AcceptedNoteDenominations { get; private set; } = string.Empty;
        public string AcceptedCoinDenominations { get; private set; } = string.Empty;
        public string DispensedDenominations { get; private set; } = string.Empty;
        public string CurrencyCode { get; private set; } = string.Empty;
        public string LanguageCode { get; private set; } = string.Empty;

        internal ConfigInfo(CTMOnCSharp.CTMConfiguration config)
        {
            if (config.count == 0) return;

            IntPtr ptr = config.intPtr;
            int size = Marshal.SizeOf(typeof(CTMOnCSharp.CTMConfigurationKeyValue));
            for (int i = 0; i < config.count; i++)
            {
                IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                CTMOnCSharp.CTMConfigurationKeyValue kv = (CTMOnCSharp.CTMConfigurationKeyValue)Marshal.PtrToStructure(itemPtr, typeof(CTMOnCSharp.CTMConfigurationKeyValue));

                switch (kv.key?.Trim().ToLowerInvariant())
                {
                    case "accepted note denominations":
                        AcceptedNoteDenominations = kv.value ?? string.Empty;
                        break;
                    case "accepted coin denominations":
                        AcceptedCoinDenominations = kv.value ?? string.Empty;
                        break;
                    case "dispensed denominations":
                        DispensedDenominations = kv.value ?? string.Empty;
                        break;
                    case "currency code":
                        CurrencyCode = kv.value ?? string.Empty;
                        break;
                    case "language code":
                        LanguageCode = kv.value ?? string.Empty;
                        break;
                }
            }
        }

        public string ToStringDebug()
        {
            return $"Notes: {AcceptedNoteDenominations}; Coins: {AcceptedCoinDenominations}; Dispensed: {DispensedDenominations}; Currency: {CurrencyCode}; Lang: {LanguageCode}";
        }
    }
    
    [ComVisible(true)]
    [Guid("0E4CF82D-FB86-42A5-8C5E-FB7F26CF6BAC")]
    [ProgId("CTMOleClient.TransferBinResult")]
    [ClassInterface(ClassInterfaceType.None)]
    public class TransferBinResult
    {
        public bool Success { get; set; } = false;
        public int TransferredAmount { get; set; } = 0;
        public string Error { get; set; } = "";

        public TransferBinResult() { }
    }

    [ComVisible(true)]
    [Guid("C747C4F5-20D0-4D34-8DE8-34F94FABE06E")]
    [ProgId("CTMOleClient.DeviceStatusInfo")]
    [ClassInterface(ClassInterfaceType.None)]
    public class DeviceStatusInfo
    {
        public int Timestamp { get; set; } = 0;
        public int DeviceType { get; set; } = 0;  // CTMDeviceType
        public int DeviceId { get; set; } = 0;
        public string DeviceModel { get; set; } = string.Empty;
        public string DeviceSubModel { get; set; } = string.Empty;
        public int Status { get; set; } = 0;

        public DeviceStatusInfo() { }
    }


    [ComVisible(true)]
    [Guid("8DF07B7E-C84A-4F62-BAFE-AD6F3B306B0E")]
    [ProgId("CTMOleClient.PurgeCoinsResult")]
    [ClassInterface(ClassInterfaceType.None)]
    public class PurgeCoinsResult
    {
        public CTMPurgeCoinsError Error { get; set; } = CTMPurgeCoinsError.CTM_PURGE_COINS_UNHANDLED_EXCEPTION;
        public ArrayList PurgedUnits { get; set; } = new ArrayList();  // Список CashUnitInfo для 1C
        public bool Success { get; set; } = false;

        public PurgeCoinsResult() { }
    }

    [ComVisible(true)]
    [Guid("F806264F-19B4-444A-BE65-5819496B1945")]
    [ProgId("CTMOleClient.TransferAllNotesResult")]
    [ClassInterface(ClassInterfaceType.None)]
    public class TransferAllNotesResult
    {
        public bool Success { get; set; } = false;
        public int TransferredAmount { get; set; } = 0;
        public string Error { get; set; } = "";
        public ArrayList TransferredUnits { get; set; } = new ArrayList();  // Список CashUnitInfo для 1C

        public TransferAllNotesResult() { }
    }


}