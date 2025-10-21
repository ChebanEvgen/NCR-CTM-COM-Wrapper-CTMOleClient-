using System;
using System.Runtime.InteropServices;

namespace CTMOleClient
{
    [ComVisible(true)]
    [Guid("CCED29B4-D6EA-47A5-A47D-A32C1A7AA11F")]
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