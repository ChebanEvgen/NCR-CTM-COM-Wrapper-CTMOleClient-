using System;
using System.Runtime.InteropServices;
using System.Collections;

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
}