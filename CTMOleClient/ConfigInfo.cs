using CTMOnCSharp;  // Для CTMConfigurationKeyValue
using System;
using System.Runtime.InteropServices;

namespace CTMOleClient
{
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

        internal ConfigInfo(CTMConfiguration config)
        {
            if (config.count == 0) return;

            IntPtr ptr = config.intPtr;
            int size = Marshal.SizeOf(typeof(CTMConfigurationKeyValue));
            for (int i = 0; i < config.count; i++)
            {
                IntPtr itemPtr = IntPtr.Add(ptr, i * size);
                CTMConfigurationKeyValue kv = (CTMConfigurationKeyValue)Marshal.PtrToStructure(itemPtr, typeof(CTMConfigurationKeyValue));

                switch (kv.key.Trim().ToLowerInvariant())
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