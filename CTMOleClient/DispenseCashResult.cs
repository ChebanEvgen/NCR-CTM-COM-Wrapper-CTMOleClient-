// Новый класс DispenseCashResult.cs (добавьте в проект, как CashUnitInfo.cs)
using System;
using System.Runtime.InteropServices;
using System.Collections;

namespace CTMOleClient
{
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
    }
}