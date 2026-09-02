using CTMOleClient;  // Твой namespace для ICTMWrapper, CTMWrapper
using CTMOnCSharp;   // Для enum: CTMBeginTransactionError, CTMEndTransactionResult
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;  // Для Process ID в ID
using System.Linq;
using static PaymentChecker;


namespace CTMOleClient.Tests
{
    [TestClass]
    public class CTMWrapperTransactionTests
    {
        private ICTMWrapper _wrapper;
        private const string ClientId = "TestClient_UnitTest";
        private const string TestTxnId = "TXN_UNITTEST_123";

        [TestInitialize]  // Вызывается перед каждым тестом
        public void TestInitialize()
        {
            _wrapper = new CTMWrapper();
            _wrapper.SetLogPath($@"C:\Temp\CTM_Test_{Process.GetCurrentProcess().Id}.log");  // Лог per-тест

            // Константы для эмулятора (из твоих логов; смени, если другой IP)
            const string emulatorHost = "100.72.2.50";  // Или "localhost" для локального
            const string emulatorPort = "3636";
            string testClientId = $"TestClient_UnitTest_{Process.GetCurrentProcess().Id}";  // Динамический ID per-тест

            // Act: Инициализация с адресом эмулятора
            bool initOk = _wrapper.Initialize(testClientId, emulatorHost, emulatorPort);
            string initError = _wrapper.GetLastError();

            // Assert: Проверка подключения (fail тест, если эмулятор не отвечает)
            Assert.IsTrue(initOk, $"Init failed on {emulatorHost}:{emulatorPort}. Error: {initError}");
            Assert.AreEqual("OK", initError, "LastError should be OK after success");

            // Дополнительно: Включаем события (callbacks)
            _wrapper.AdviseEvents();

            LogToConsole("TestInitialize: Wrapper initialized and connected to emulator.");
        }

        [TestCleanup]  
        public void TestCleanup()
        {
            if (_wrapper != null)
            {
                _wrapper.UnadviseEvents();  
                _wrapper.Uninitialize();
                _wrapper = null;
            }
        }

       
        private void LogToConsole(string msg)
        {
            Console.WriteLine($"[TEST {DateTime.Now:HH:mm:ss}] {msg}");
        }


    
        [TestMethod]
        public void Test_OpenAndCloseTransaction()
        {
            LogToConsole("Test_OpenAndCloseTransaction: Starting...");

            // Act: Начинаем customer-транзакцию
            bool beginOk = _wrapper.BeginCustomerTransaction(TestTxnId);
            string beginError = _wrapper.GetLastError();
            LogToConsole($"Begin result: {beginOk}, Error: '{beginError}'");

            // Assert: Проверка начала (должен успех)
            Assert.IsTrue(beginOk, $"Begin failed. Error: {beginError}");
            Assert.AreEqual("OK", beginError, "LastError should be OK after begin success");

            // Act: Заканчиваем customer-транзакцию
            bool endOk = _wrapper.EndCustomerTransaction(TestTxnId);
            string endError = _wrapper.GetLastError();
            LogToConsole($"End result: {endOk}, Error: '{endError}'");

            // Assert: Проверка окончания (должен успех)
            Assert.IsTrue(endOk, $"End failed. Error: {endError}");
            Assert.AreEqual("OK", endError, "LastError should be OK after end success");

            LogToConsole("Test_OpenAndCloseTransaction: SUCCESS - Transaction opened and closed.");
        }

        [TestMethod]
        public void Test_TwoClientsAlreadyInProgress()
        {
            const string TxnId1 = "TXN_POS1_123";
            const string TxnId2 = "TXN_POS2_456";

            LogToConsole("Test_TwoClientsAlreadyInProgress: Starting...");

            // 1. Первый клиент (уже инициализирован в _wrapper) начинает транзакцию
            bool begin1 = _wrapper.BeginCustomerTransaction(TxnId1);
            string error1 = _wrapper.GetLastError();
            LogToConsole($"Wrapper Begin Txn1: {begin1}, Error: '{error1}'");
            Assert.IsTrue(begin1, $"First transaction start failed: {error1}");
            Assert.AreEqual("OK", error1);

            // 2. Пытаемся начать вторую транзакцию (или от имени другого ID, если сервер это поддерживает, 
            // либо проверяем занятость текущей сессии)
            // Если нативная библиотека держит блокировку по сессии, то повторный Begin выдаст ошибку:
            bool begin2_fail = _wrapper.BeginCustomerTransaction(TxnId2);
            string error2_fail = _wrapper.GetLastError();
            LogToConsole($"Wrapper Begin Txn2 (should fail): {begin2_fail}, Error: '{error2_fail}'");

            // В зависимости от логики эмулятора здесь ожидается ошибка занятости
            Assert.IsFalse(begin2_fail, "Should not allow starting a new transaction while one is in progress");

            // 3. Завершаем первую транзакцию
            bool end1 = _wrapper.EndCustomerTransaction(TxnId1);
            string endError1 = _wrapper.GetLastError();
            LogToConsole($"Wrapper End Txn1: {end1}, Error: '{endError1}'");
            Assert.IsTrue(end1, $"First transaction end failed: {endError1}");
            Assert.AreEqual("OK", endError1);

            // 4. Теперь можно успешно начать новую транзакцию
            bool begin2_ok = _wrapper.BeginCustomerTransaction(TxnId2);
            string error2_ok = _wrapper.GetLastError();
            LogToConsole($"Wrapper Begin Txn2 after close (ok): {begin2_ok}, Error: '{error2_ok}'");
            Assert.IsTrue(begin2_ok, $"Transaction start after close failed: {error2_ok}");
            Assert.AreEqual("OK", error2_ok);

            // Cleanup: закрываем вторую транзакцию, чтобы оставить сессию чистой
            _wrapper.EndCustomerTransaction(TxnId2);

            LogToConsole("Test_TwoClientsAlreadyInProgress: SUCCESS - Sequential transaction locking tested.");
        }

        [TestMethod]
        public void Test_TestAllDevices()
        {
            LogToConsole("Test_TestAllDevices: Starting...");

            // Act: Вызываем обновленный метод тестирования всех устройств с out-параметром
            string errorDescription;
            int errorCode = _wrapper.TestAllDevices(out errorDescription);
            string lastError = _wrapper.GetLastError();

            LogToConsole($"TestAllDevices result: errorCode={errorCode}, description='{errorDescription}', LastError='{lastError}'");

            // Assert: Проверяем, что метод вернул валидный код и заполнил ошибки
            Assert.AreEqual(0, errorCode, $"TestAllDevices failed with error code: {errorCode}");
            Assert.AreEqual("OK", lastError, "LastError should be OK after successful device test");
            Assert.IsFalse(string.IsNullOrEmpty(errorDescription), "Error description should be populated");

            LogToConsole("Test_TestAllDevices: SUCCESS - Device test executed.");
        }


    }

    [TestClass]
    public class PaymentCheckerTests
    {
        private void Log(string message)
        {
            Console.WriteLine($"[PaymentChecker {DateTime.Now:HH:mm:ss}] {message}");
        }

        private static List<CashLevel> CreateLevels(params (decimal value, int stored)[] items)
        {
            return items.Select(x => new CashLevel
            {
                Value = x.value,
                Stored = x.stored
            }).ToList();
        }

        [TestMethod]
        public void CheckAllAmounts_AllAchievable_ReturnsSuccess()
        {
            Log("AllAchievable: Starting...");

            var levels = CreateLevels(
                (0.5m, 200),
                (1.0m, 100),
                (2.0m, 50),
                (5.0m, 30),
                (10.0m, 20)
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 150m);

            Assert.IsTrue(result.Success, "Все суммы должны быть достижимы");
            Assert.AreEqual(0, result.Missing.Count);

            Log("SUCCESS");
        }

        [TestMethod]
        public void CheckAllAmounts_OnlyLargeBills_HasMissing()
        {
            Log("OnlyLargeBills: Starting...");

            var levels = CreateLevels(
                (10.0m, 15),
                (20.0m, 10),
                (50.0m, 5)
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 100m);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Missing.Count > 0);

            Assert.IsTrue(result.Missing.Contains(0.5m));
            Assert.IsTrue(result.Missing.Contains(1.0m));
            Assert.IsTrue(result.Missing.Contains(5.0m));
            Assert.IsFalse(result.Missing.Contains(10.0m));
            Assert.IsFalse(result.Missing.Contains(20.0m));

            Log($"Missing count: {result.Missing.Count}");
            Log("SUCCESS");
        }

        [TestMethod]
        public void CheckAllAmounts_EmptyLevels_AllMissing()
        {
            Log("EmptyLevels: Starting...");

            var levels = new List<CashLevel>();

            var result = PaymentChecker.CheckAllAmounts(levels, 5.0m);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(10, result.Missing.Count); // 0.5 ... 5.0
            Assert.AreEqual(0.5m, result.Missing.First());
            Assert.AreEqual(5.0m, result.Missing.Last());

            Log("SUCCESS");
        }

        [TestMethod]
        public void CheckAllAmounts_ZeroCounts_Ignored()
        {
            Log("ZeroCounts: Starting...");

            var levels = CreateLevels(
                (0.5m, 0),
                (1.0m, 0),
                (2.0m, 0),
                (5.0m, 8)
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 20m);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Missing.Contains(0.5m));
            Assert.IsTrue(result.Missing.Contains(1.0m));
            Assert.IsFalse(result.Missing.Contains(5.0m));
            Assert.IsFalse(result.Missing.Contains(10.0m));

            Log("SUCCESS");
        }


        [TestMethod]
        public void CheckAllAmounts_LimitedByTotalMoney()
        {
            Log("LimitedByTotalMoney: Starting...");

            // Всего денег только 7.5 грн
            var levels = CreateLevels(
                (0.5m, 5),   // 2.5
                (1.0m, 5)    // 5.0  → итого 7.5
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 100m);

            // Теперь метод всегда проверяет полный диапазон,
            // поэтому Success = false, а всё что выше 7.5 — в missing
            Assert.IsFalse(result.Success, "Выше реальной суммы денег должны быть недостающие");

            // До 7.5 всё должно быть достижимо
            Assert.IsFalse(result.Missing.Contains(0.5m));
            Assert.IsFalse(result.Missing.Contains(1.0m));
            Assert.IsFalse(result.Missing.Contains(2.5m));
            Assert.IsFalse(result.Missing.Contains(7.0m));
            Assert.IsFalse(result.Missing.Contains(7.5m));

            // Выше 7.5 — недостающие
            Assert.IsTrue(result.Missing.Contains(8.0m));
            Assert.IsTrue(result.Missing.Contains(10.0m));
            Assert.IsTrue(result.Missing.Contains(100.0m));

            Log($"Missing count: {result.Missing.Count}");
            Log("SUCCESS");
        }

  
        [TestMethod]
        public void CheckAllAmounts_BinarySplit_LargeCount()
        {
            Log("BinarySplit LargeCount: Starting...");

            var levels = CreateLevels(
                (0.5m, 30000)
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 1000m);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Missing.Count);

            Log("SUCCESS");
        }

        [TestMethod]
        public void CheckAllAmounts_ClassicGaps_2and5()
        {
            Log("ClassicGaps 2+5: Starting...");

            var levels = CreateLevels(
                (2.0m, 15),
                (5.0m, 10)
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 30m);

            Assert.IsFalse(result.Success);

            Assert.IsTrue(result.Missing.Contains(0.5m));
            Assert.IsTrue(result.Missing.Contains(1.0m));
            Assert.IsTrue(result.Missing.Contains(3.0m));
            Assert.IsFalse(result.Missing.Contains(2.0m));
            Assert.IsFalse(result.Missing.Contains(4.0m));
            Assert.IsFalse(result.Missing.Contains(5.0m));
            Assert.IsFalse(result.Missing.Contains(6.0m));
            Assert.IsFalse(result.Missing.Contains(7.0m));

            Log($"Missing count: {result.Missing.Count}");
            Log("SUCCESS");
        }

        [TestMethod]
        public void CheckAllAmounts_SingleHalfHryvnia()
        {
            Log("Single 0.5: Starting...");

            var levels = CreateLevels(
                (0.5m, 1)
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 2.0m);

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Missing.Contains(0.5m)); // 0.5 должна быть
            Assert.IsTrue(result.Missing.Contains(1.0m));
            Assert.IsTrue(result.Missing.Contains(1.5m));
            Assert.IsTrue(result.Missing.Contains(2.0m));

            Log("SUCCESS");
        }

        [TestMethod]
        public void CheckAllAmounts_MaxAmount999()
        {
            Log("MaxAmount 999: Starting...");

            var levels = CreateLevels(
                (0.5m, 80),
                (1.0m, 60),
                (2.0m, 40),
                (5.0m, 25),
                (10.0m, 15),
                (20.0m, 10),
                (50.0m, 8),
                (100.0m, 5),
                (200.0m, 3),
                (500.0m, 2)
            );

            var result = PaymentChecker.CheckAllAmounts(levels, 999m);

            Log($"Success: {result.Success}, Missing: {result.Missing.Count}");

            if (result.Missing.Count > 0)
            {
                Log("First 10 missing: " + string.Join(", ", result.Missing.Take(10)));
            }

            // Информационный тест
            Assert.IsNotNull(result.Missing);
        }
    }


}