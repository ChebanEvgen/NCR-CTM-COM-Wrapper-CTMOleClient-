# NCR CTM COM Wrapper (CTMOleClient)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022-blue)](https://visualstudio.microsoft.com/)

This repository contains a C# COM (OLE) wrapper library for integrating NCR SelfServ™ Cash Tender Module (CTM) with 1C:Enterprise 8.3 (ordinary forms). It provides a managed interface to the native `libctmclient-0.dll` for cash management operations in Point-of-Sale (POS) systems, such as accepting/dispensing cash, transaction handling, and device status monitoring.

The wrapper is designed for seamless use in 1C 8.3 environments, enabling cash machine control without direct P/Invoke complexity. It's based on the [NCR SelfServ Cash Tender POS Integration SDK User Guide (Release 1.2)](https://github.com/ChebanEvgen/NCR-CTM-COM-Wrapper-CTMOleClient-/blob/main/docs/CTM_POS_Integration.pdf).

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
- Example Refill Flow:
  ```1c
  Если CTM.BeginCashManagementTransaction("user456", "cashier789", ТранзакцияID) Тогда
      Результат = CTM.BeginRefill(-1);  // -1 = unlimited
      Если Результат = 0 Тогда  // Success
          Пока Истина Цикл
              // Wait for OnCashAccept event or timeout
          КонецЦикла;
          Успех = CTM.EndRefill();
      КонецЕсли;
      CTM.EndCashManagementTransaction(ТранзакцияID);
  КонецЕсли;
  ```
- Handle Events: Use 1C form events like `OnCashAccept(Amount, AmountDue, Count, Currency)`.

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

## Known Issues

- Repeated `GetNonDispensableCashCounts()` after refill may return empty (due to NCR emulator "No Loader" config) — handled gracefully.
- Ensure `SynchronizationContext` for UI events in 1C forms.

## Contributing

Fork the repo, create a branch, submit PRs. Focus on error resilience and 1C compatibility.

## License

MIT License — see [LICENSE](LICENSE) file.

## Author & Contact

- **Evgen Cheban** (ChebanEvgen) — Developer.
- Issues/PRs: GitHub repo.
- Based on NCR SDK docs (B005-0000-2151 Issue E, 2008–2015).

---

*Project started November 2025. For production, consult NCR support for SDK updates.*
