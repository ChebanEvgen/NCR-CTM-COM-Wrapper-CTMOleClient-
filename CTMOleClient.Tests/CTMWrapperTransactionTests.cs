using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using CTMOleClient;  // Твой namespace для ICTMWrapper, CTMWrapper
using CTMOnCSharp;   // Для enum: CTMBeginTransactionError, CTMEndTransactionResult
using System.Diagnostics;  // Для Process ID в ID

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

            // Act: Создаём и инициализируем двух клиентов (POS_ID_1 и POS_ID_2)
            ICTMWrapper wrapper1 = new CTMWrapper();
            wrapper1.SetLogPath($@"C:\Temp\CTM_Test_POS1_{Process.GetCurrentProcess().Id}.log");
            bool init1 = wrapper1.Initialize("POS_ID_1", "100.72.2.50", "3636");
            Assert.IsTrue(init1, "Wrapper1 init failed");
            wrapper1.AdviseEvents();

            ICTMWrapper wrapper2 = new CTMWrapper();
            wrapper2.SetLogPath($@"C:\Temp\CTM_Test_POS2_{Process.GetCurrentProcess().Id}.log");
            bool init2 = wrapper2.Initialize("POS_ID_2", "100.72.2.50", "3636");
            Assert.IsTrue(init2, "Wrapper2 init failed");
            wrapper2.AdviseEvents();

            // Act: Wrapper1 начинает txn — успех
            bool begin1 = wrapper1.BeginCustomerTransaction(TxnId1);
            string error1 = wrapper1.GetLastError();
            LogToConsole($"Wrapper1 Begin: {begin1}, Error: '{error1}'");
            Assert.IsTrue(begin1, $"Wrapper1 begin failed: {error1}");
            Assert.AreEqual("OK", error1);

            // Act: Wrapper2 пытается начать — ошибка ALREADY_IN_PROGRESS
            bool begin2_fail = wrapper2.BeginCustomerTransaction(TxnId2);
            string error2_fail = wrapper2.GetLastError();
            LogToConsole($"Wrapper2 Begin (fail): {begin2_fail}, Error: '{error2_fail}'");
            Assert.IsFalse(begin2_fail, "Wrapper2 should fail on already in progress");
            Assert.AreEqual("CTM_BEGIN_TRX_ERROR_ALREADY_IN_PROGRESS", error2_fail);

            // Act: Wrapper1 закрывает txn — успех
            bool end1 = wrapper1.EndCustomerTransaction(TxnId1);
            string endError1 = wrapper1.GetLastError();
            LogToConsole($"Wrapper1 End: {end1}, Error: '{endError1}'");
            Assert.IsTrue(end1, $"Wrapper1 end failed: {endError1}");
            Assert.AreEqual("OK", endError1);

            // Act: Wrapper2 теперь начинает — успех
            bool begin2_ok = wrapper2.BeginCustomerTransaction(TxnId2);
            string error2_ok = wrapper2.GetLastError();
            LogToConsole($"Wrapper2 Begin (ok): {begin2_ok}, Error: '{error2_ok}'");
            Assert.IsTrue(begin2_ok, $"Wrapper2 begin after end failed: {error2_ok}");
            Assert.AreEqual("OK", error2_ok);

            // Cleanup: Закрываем
            wrapper1.UnadviseEvents();
            wrapper1.Uninitialize();
            wrapper2.UnadviseEvents();
            wrapper2.Uninitialize();

            LogToConsole("Test_TwoClientsAlreadyInProgress: SUCCESS - Two clients with blocking tested.");
        }


    }
}