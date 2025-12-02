# NCR CTM COM Wrapper (CTMOleClient)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022-blue)](https://visualstudio.microsoft.com/)

This repository contains a C# COM (OLE) wrapper library for integrating NCR SelfServ™ Cash Tender Module (CTM) with 1C:Enterprise 8.3 (ordinary forms). It provides a managed interface to the native `libctmclient-0.dll` for cash management operations in Point-of-Sale (POS) systems, such as accepting/dispensing cash, transaction handling, and device status monitoring.

The wrapper is designed for seamless use in 1C 8.3 environments, enabling cash machine control without direct P/Invoke complexity. It's based on the [NCR SelfServ™ Cash Tender POS Integration SDK User Guide (Release 1.2, B005-0000-2151 Issue E)](https://github.com/ChebanEvgen/NCR-CTM-COM-Wrapper-CTMOleClient-/blob/main/docs/CTM_POS_Integration.pdf).

## Features

- **COM-Compatible Interface**: Dual interface (`ICTMWrapper`) for easy registration and use in 1C via `ПодключитьВнешнююКомпоненту`.
- **Cash Management Transactions**: Start/end CM transactions, refill cash units, dispense change.
- **Event Handling**: Asynchronous callbacks for cash accept, errors, status changes (marshaled to 1C events).
- **Error Handling**: Graceful failures with `_lastError` property; prevents crashes in 1C on repeated calls.
- **Logging**: Built-in file logging for debugging (configurable path).
- **Supported Operations**:
  - Initialize/Uninitialize connection to CTM service.
  - Get/Set cash counts (dispensable/non-dispensable).
  - Begin/End customer and CM transactions.
  - Accept/Dispense cash, purge coins.
  - Device testing and configuration retrieval.

## Prerequisites

- **Development Environment**:
  - Microsoft Visual Studio Community 2022 (64-bit, v17.14+).
  - .NET Framework 4.7.2 or later (for COM visibility).
- **Runtime Dependencies**:
  - NCR CTM SDK (includes `libctmclient-0.dll` and configs like `CTMBaseOpts.dat`).
  - Environment variable: `CTM_HOME` set to `C:\Program Files (x86)\NCR\CashTenderModule\`.
  - CTM Service running (Java-based, port 3636 default).
- **Testing Environment**:
  - 1C:Enterprise 8.3 (ordinary forms mode).
  - Admin rights for COM registration (`regasm.exe`).

## Installation

1. **Clone the Repository**:
   ```
   git clone https://github.com/ChebanEvgen/NCR-CTM-COM-Wrapper-CTMOleClient-.git
   cd NCR-CTM-COM-Wrapper-CTMOleClient-
   ```

2. **Set Up Environment**:
   - Install NCR CTM SDK from vendor.
   - Set `CTM_HOME` in System Environment Variables.
   - Copy `libctmclient-0.dll` to your build output directory (e.g., `bin/Debug`).

3. **Build the Project**:
   - Open `CTMOleClient.sln` in Visual Studio.
   - Set configuration to `Debug` / `Any CPU`.
   - Build: **Build > Rebuild Solution** (Ctrl+Shift+B).

4. **Register COM Object**:
   - Run as Administrator: `register_ctmoleclient.bat` (in root folder).
   - Or manually: `regasm /codebase /tlb CTMOleClient.dll`.

## Usage

### In Visual Studio (C# Projects)
- Reference the built `CTMOleClient.dll`.
- Use `ICTMWrapper` interface:
  ```csharp
  var ctm = new CTMWrapper();
  ctm.Initialize("POS_ID_1", "localhost", "3636");
  ctm.AdviseEvents();  // Enable events

  var counts = ctm.GetDispensableCashCounts();
  // Process ArrayList of CashUnitInfo

  ctm.UnadviseEvents();
  ```

### In 1C 8.3 (Ordinary Forms)
- Connect: `CTM = Новый("AddIn.CTMOleClient.CTMWrapper");`.
- Initialize: `CTM.Initialize("Client_XXX", "100.72.2.50", "3636");`.
- Example Refill Flow (working code from tests):

```1c
Перем CTM;     
Перем ClientName;   
Перем ТранзакцияCM;

Функция Копейки(суммаВКопейках)
    Возврат суммаВКопейках/100;
КонецФункции

Процедура ПриОткрытии()
    Host = "100.72.2.50";
    Port = "3636";
    ClientName = "Client_" + Лев(Новый УникальныйИдентификатор,8);  
    
    CTM = Новый COMОбъект("CTMOleClient.CTMWrapper");  
    CTM.SetConnection(ЭтаФорма); 
    CTM.SetLogPath("C:\Temp\CTM_"+Новый УникальныйИдентификатор()+".log");
    
    Если CTM.Initialize(ClientName,Host,Port) Тогда
        CTM.AdviseEvents();  
        Сообщить("Init CTM: " + CTM.GetLogPath());
    Иначе
        Сообщить("Init CTM failed: " + CTM.GetLastError());
    КонецЕсли;    
КонецПроцедуры   

Процедура OnCashAccept(Принято, Остаток, Номинал, Валюта) Экспорт
    Сообщить(">>> Колбек CTM: Получено: " + Копейки(Принято) + " (" + Копейки(Номинал) + " " + Валюта + "), Остаток: " + Копейки(Остаток));
КонецПроцедуры

Процедура OnDeviceError(ИнфоОшибки) Экспорт
    Сообщить(">>> Колбек CTM Ошибка: " + ИнфоОшибки);
КонецПроцедуры

Процедура OnCashAcceptComplete() Экспорт
    Сообщить(">>> Колбек CTM: Приём завершён");
КонецПроцедуры

Процедура OnDeviceStatus(ИнфоСтатуса) Экспорт
    Сообщить(">>> Колбек CTM Статус: " + ИнфоСтатуса);
КонецПроцедуры

Процедура OnSocketClosed(ИнфоСоединения) Экспорт  
    Сообщить(">>> Колбек CTM: Соединение закрыто - " + ИнфоСоединения);  
КонецПроцедуры

Процедура OnChangeContext(ИнфоКонтекста) Экспорт  
    Сообщить(">>> Колбек CTM: Смена контекста - " + ИнфоКонтекста);  
КонецПроцедуры

Процедура OnAuthentication(ИмяПользователя, ЭтоHCashier) Экспорт  
    Сообщить(">>> Колбек CTM: Аутентификация - Пользователь: " + ИмяПользователя + ", HCashier: " + ЭтоHCashier);  
КонецПроцедуры

Процедура OnCMClosed(ИнфоЗакрытия) Экспорт  
    Сообщить(">>> Колбек CTM: CM-приложение закрыто - " + ИнфоЗакрытия);  
КонецПроцедуры

Процедура OnTransactionEnd(TxnId, Status) Экспорт
    Сообщить(">>> Колбек CTM: Транзакция " + TxnId + " завершена со статусом: " + Status);
КонецПроцедуры

Процедура НачатьCMТранзакциюНажатие(Команда)
    UserId = "456";
    CashierId = "789";
    ТранзакцияCM_Нов = "";
    
    Если CTM.BeginCashManagementTransaction(UserId, CashierId, ТранзакцияCM_Нов) Тогда
        Сообщить("✓ Транзакция CM начата. ID = " + ТранзакцияCM_Нов );   
        ТранзакцияCM = ТранзакцияCM_Нов;
    Иначе
        Ошибка = CTM.GetLastError();
        Сообщить("✗ (Начало транзакции) Ошибка: " + Ошибка);
    КонецЕсли;
КонецПроцедуры

Процедура НачатьПополнениеНажатие(Элемент)  
    Если ПустаяСтрока(ТранзакцияCM) Тогда
        Сообщить("Сначала начните CM-транзакцию!");
        Возврат;
    КонецЕсли;
    
    Результат = CTM.BeginRefill(-1); 
    Если Результат = 0 Тогда 
        Сообщить("Пополнение начато: акцепторы включены.");
        СуммаПополнения = 0;  
    Иначе
        Сообщить("Ошибка начала пополнения: " + Результат);
    КонецЕсли;     
КонецПроцедуры

Процедура ЗавершитьПополнениеНажатие(Элемент)     
    Если CTM.EndRefill() Тогда 
        Сообщить("Пополнение завершено. Итого: " );
        //СуммаПополнения = Результат.totalAmount;
    Иначе
        Сообщить("Ошибка завершения: " + CTM.GetLastError());
    КонецЕсли; 
КонецПроцедуры

Процедура ЗавершитьCMТранзакциюНажатие(Элемент)    
    Если ПустаяСтрока(ТранзакцияCM) Тогда
        Сообщить("✗ Нет активной CM-транзакции!");
        Возврат;
    КонецЕсли;
    
    Если CTM.EndCashManagementTransaction(ТранзакцияCM) Тогда
        Сообщить("✓ CM-транзакция завершена. ACO-отчёт сгенерирован.");
        ТранзакцияCM = "";
    Иначе
        Сообщить("✗ Ошибка завершения CM: " + CTM.GetLastError());
    КонецЕсли;
КонецПроцедуры

Процедура ОтключитьИЗакрытьНажатие(Элемент)  
    Если CTM <> Неопределено Тогда
        CTM.UnadviseEvents();
        CTM.Uninitialize();
        CTM = Неопределено;    
        ЗавершитьРаботуСистемы(Ложь);
    КонецЕсли;  
КонецПроцедуры
```

- Event Handling: Use 1C form events like `OnCashAccept(Amount, AmountDue, Count, Currency)`.

See `examples/1C_Example.epf` for a sample 1C external processing file.

## Building and Testing

- **Build Targets**:
  - Debug: For development (with logging).
  - Release: For production (optimized, no debug symbols).
- **Testing**:
  - Run CTM Service: `java -jar ctm-service.jar` (from NCR install dir).
  - Unit Tests: Run `NUnit` tests in VS (if added).
  - 1C Integration: Load the example form, simulate cash inserts via emulator.

For troubleshooting:
- Check `CTM.GetLastError()` after calls.
- Logs: Set via `CTM.SetLogPath("C:\Temp\CTM.log")`.



## Contributing

Fork the repo, create a branch, submit PRs. Focus on error resilience and 1C compatibility.

## License

MIT License — see [LICENSE](LICENSE) file.

## Author & Contact

- **Evgen Cheban (ChebanEvgen)** — Developer.
- Issues/PRs: GitHub repo.
- Based on NCR SDK docs (B005-0000-2151 Issue E, 2008–2015).

---

*Project started November 2025. For production, consult NCR support for SDK updates.*
