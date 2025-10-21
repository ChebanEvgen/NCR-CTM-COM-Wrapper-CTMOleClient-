using System;
using System.Runtime.InteropServices;

namespace CTMOleClient
{
    [ComVisible(true)]
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]  
    [ProgId("CTMOleClient.CashUnitInfo")]  
    [ClassInterface(ClassInterfaceType.None)]  
    public class CashUnitInfo
    {
        public int Denomination { get; set; } 
        public int Count { get; set; }     
        public string CurrencyCode { get; set; }
        public int Type { get; set; }        
        public CashUnitInfo() { }
    }
}