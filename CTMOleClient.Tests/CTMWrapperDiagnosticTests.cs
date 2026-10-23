// Diagnostic tests for CTMOleClient (.NET Framework 4.8, x86, MSTest).
// Add this file to the EXISTING MSTest project; do not replace current tests.
// Default tests never dispense, accept, purge or move cash.
using CTMOleClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;


namespace CTMOleClient.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class CTMWrapperDiagnosticTests
    {
        private CTMWrapper _wrapper;
        private bool _initialized;
        private static readonly string Host = Environment.GetEnvironmentVariable("CTM_TEST_HOST") ?? "100.72.2.50";
        private static readonly string Port = Environment.GetEnvironmentVariable("CTM_TEST_PORT") ?? "3636";
        private static readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "CTMWrapperDiagnostics");

       // private const string Host = "100.70.1.18";
       // private const string Port = "3636";

        private static void Log(string message)
        {
            Directory.CreateDirectory(DirectoryPath);
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " pid=" + Process.GetCurrentProcess().Id + " tid=" + Thread.CurrentThread.ManagedThreadId + " " + message;
            Console.WriteLine(line);
            File.AppendAllText(Path.Combine(DirectoryPath, "diagnostics.log"), line + Environment.NewLine);
        }

        private static object Field(object obj, string name)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "Field is missing: " + name);
            return f.GetValue(obj);
        }

        private static bool HasCallback(object obj, string name) { return Field(obj, name) != null; }
        private static bool EventsEnabled(object obj) { return (bool)Field(obj, "_eventsEnabled"); }

        [TestInitialize]
        public void Setup()
        {
            _wrapper = new CTMWrapper();
            _wrapper.SetLogPath(Path.Combine(DirectoryPath, "wrapper_" + Process.GetCurrentProcess().Id + ".log"));
            _initialized = false;
        }

        private void Connect()
        {
            string id = "Diag_" + Process.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Log("Initialize START host=" + Host + " port=" + Port + " client=" + id);
            bool ok = _wrapper.Initialize(id, Host, Port);
            _initialized = ok;
            Log("Initialize END ok=" + ok + " error=" + _wrapper.GetLastError());
            Assert.IsTrue(ok, "Cannot connect to TEST emulator. Check CTM_TEST_HOST and CTM_TEST_PORT.");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (_wrapper == null) return;
            if (_initialized)
            {
                Log("Uninitialize START");
                try { _wrapper.Uninitialize(); Log("Uninitialize END"); }
                catch (Exception ex) { Log("Uninitialize exception " + ex); throw; }
                finally { _initialized = false; }
            }
            _wrapper = null;
        }


        private sealed class D11Receiver
        {
            public readonly ManualResetEventSlim Entered =
                new ManualResetEventSlim(false);

            public readonly ManualResetEventSlim Release =
                new ManualResetEventSlim(false);

            public int CallCount;
            public int ThreadId;

            public bool BlockOnCall;

            public void OnSocketClosed(string message)
            {
                ThreadId = Thread.CurrentThread.ManagedThreadId;

                Interlocked.Increment(ref CallCount);

                Entered.Set();

                if (BlockOnCall)
                {
                    if (!Release.Wait(5000))
                        throw new TimeoutException(
                            "D11: Receiver release timeout.");
                }
            }
        }


        private class FakeOneCReceiver
        {
            public int CallCount { get; private set; }
            public int LastThreadId { get; private set; }
            public string LastMessage { get; private set; }

            public void OnSocketClosed(string message)
            {
                CallCount++;
                LastThreadId = Thread.CurrentThread.ManagedThreadId;
                LastMessage = message;
            }
        }

        private class InlineTestContext : SynchronizationContext
        {
            public override void Post(
                SendOrPostCallback callback,
                object state)
            {
                // Только тестовый контекст.
                // Исполняет callback синхронно.
                callback(state);
            }
        }

        private void SimulateSocketClosed()
        {
            MethodInfo method = typeof(CTMWrapper).GetMethod(
                "HandleSocketClosed",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "HandleSocketClosed not found.");

            Type eventType = method.GetParameters()[0].ParameterType;

            object eventInfo = Activator.CreateInstance(eventType);

            // Вызываем управляемый обработчик напрямую,
            // без регистрации и вызовов NCR DLL.
            method.Invoke(_wrapper, new object[] { eventInfo });
        }


        private sealed class StaPumpContext : SynchronizationContext, IDisposable
        {
            private readonly System.Collections.Concurrent.BlockingCollection<Action>
                _queue = new System.Collections.Concurrent.BlockingCollection<Action>();

            private readonly Thread _thread;
            private readonly ManualResetEventSlim _ready =
                new ManualResetEventSlim(false);

            public int ThreadId { get; private set; }

            public StaPumpContext()
            {
                _thread = new Thread(Run);
                _thread.IsBackground = true;
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();

                if (!_ready.Wait(5000))
                    throw new TimeoutException("STA thread did not start.");
            }

            private void Run()
            {
                ThreadId = Thread.CurrentThread.ManagedThreadId;
                SynchronizationContext.SetSynchronizationContext(this);

                _ready.Set();

                foreach (Action action in _queue.GetConsumingEnumerable())
                    action();
            }

            public override void Post(
                SendOrPostCallback callback,
                object state)
            {
                _queue.Add(() => callback(state));
            }

            public void Invoke(Action action)
            {
                Exception error = null;

                using (var completed = new ManualResetEventSlim(false))
                {
                    Post(_ =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                        }
                        finally
                        {
                            completed.Set();
                        }
                    }, null);

                    if (!completed.Wait(5000))
                        throw new TimeoutException("STA action timed out.");
                }

                if (error != null)
                    throw new InvalidOperationException(
                        "STA action failed.", error);
            }

            public void Dispose()
            {
                _queue.CompleteAdding();

                if (!_thread.Join(5000))
                    throw new TimeoutException("STA thread did not stop.");

                _ready.Dispose();
                _queue.Dispose();
            }
        }


        [TestMethod]
        public void D01_ManagedDefaults_NoNativeCalls()
        {
            Assert.IsFalse(EventsEnabled(_wrapper));
            Assert.IsFalse(HasCallback(_wrapper, "_cashAcceptCallback"));
            Assert.IsFalse(HasCallback(_wrapper, "_deviceErrorCallback"));
            Log("Default managed state OK");
        }

        [TestMethod]
        public void D02_SynchronizationContextFallback_IsNotUiDispatcher()
        {
            var previous = SynchronizationContext.Current;

            try
            {
                // Имитируем отсутствие UI SynchronizationContext
                SynchronizationContext.SetSynchronizationContext(null);

                _wrapper.SetConnection(new object());

                var ctx = (SynchronizationContext)Field(
                    _wrapper,
                    "_uiContext"
                );

                // При отсутствии контекста нельзя создавать
                // обычный SynchronizationContext как замену UI.
                Assert.IsNull(
                    ctx,
                    "ERROR: SetConnection created an unsafe fallback SynchronizationContext."
                );

                Log("PASS: No SynchronizationContext available.");
                Log("PASS: Unsafe ThreadPool fallback is disabled.");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        [TestMethod]
        public void D03_Initialize_RegistersCallbackReferences()
        {
            Connect();

            // Проверяем, что регистрация callbacks выполнена
            bool registered = (bool)Field(
                _wrapper,
                "_callbacksRegistered"
            );

            Assert.IsTrue(
                registered,
                "Callbacks must be registered after Initialize."
            );

            string[] callbacks =
            {
        "_deviceErrorCallback",
        "_cashAcceptCallback",
        "_cashAcceptCompleteCallback",
        "_deviceStatusCallback",
        "_socketClosedCallback",
        "_changeContextCallback",
        "_authenticationCallback",
        "_cmClosedCallback"
    };

            foreach (string callbackName in callbacks)
            {
                Assert.IsTrue(
                    HasCallback(_wrapper, callbackName),
                    "Callback reference missing: " + callbackName
                );
            }

            Log("PASS D03: All 8 callback references are present.");
            Log("PASS D03: _callbacksRegistered=True.");

            // Включаем доставку событий в 1С
            _wrapper.AdviseEvents();

            Assert.IsTrue(
                EventsEnabled(_wrapper),
                "AdviseEvents must enable event delivery."
            );

            Log("PASS D03: AdviseEvents enabled.");
        }



        [TestMethod]
        public void D04_AdviseUnadvise_RevealsLostRegistrations()
        {
            Connect();

            // После Initialize callbacks должны быть зарегистрированы
            Assert.IsTrue(
                (bool)Field(_wrapper, "_callbacksRegistered"),
                "Callbacks должны быть зарегистрированы после Initialize."
            );

            Assert.IsTrue(
                HasCallback(_wrapper, "_cashAcceptCallback"),
                "CashAccept callback отсутствует после Initialize."
            );

            // Включаем события 1С
            _wrapper.AdviseEvents();

            Assert.IsTrue(
                EventsEnabled(_wrapper),
                "AdviseEvents должен включать события."
            );

            // Отключаем события 1С
            _wrapper.UnadviseEvents();

            Assert.IsFalse(
                EventsEnabled(_wrapper),
                "UnadviseEvents должен отключать события."
            );

            // Нативные callbacks при этом должны оставаться зарегистрированными
            Assert.IsTrue(
                (bool)Field(_wrapper, "_callbacksRegistered"),
                "UnadviseEvents не должен удалять нативные callbacks."
            );

            Assert.IsTrue(
                HasCallback(_wrapper, "_cashAcceptCallback"),
                "UnadviseEvents удалил ссылку на CashAccept callback."
            );

            // Повторно включаем события
            _wrapper.AdviseEvents();

            Assert.IsTrue(
                EventsEnabled(_wrapper),
                "Повторный AdviseEvents не включил события."
            );

            Assert.IsTrue(
                (bool)Field(_wrapper, "_callbacksRegistered"),
                "После повторного AdviseEvents callbacks не зарегистрированы."
            );

            Assert.IsTrue(
                HasCallback(_wrapper, "_cashAcceptCallback"),
                "После повторного AdviseEvents потеряна ссылка на callback."
            );

            Log("PASS: AdviseEvents / UnadviseEvents lifecycle OK.");
            Log("PASS: Native callback references preserved.");
        }

        [TestMethod]
        public void D05_CashCounts_ReadOnlyRepeated()
        {
            Connect();
            int iterations;
            if (!int.TryParse(Environment.GetEnvironmentVariable("CTM_TEST_ITERATIONS"), out iterations)) iterations = 200;
            Assert.IsTrue(iterations >= 1 && iterations <= 20000, "CTM_TEST_ITERATIONS must be 1..20000");
            var p = Process.GetCurrentProcess();
            long baseline = p.PrivateMemorySize64;
            int errorCount = 0;
            int totalUnits = 0;
            for (int i = 1; i <= iterations; i++)
            {
                ArrayList units = _wrapper.GetDispensableCashCounts();
                if (units == null || units.Count == 0) errorCount++;
                if (units != null) totalUnits += units.Count;
                if (i % 100 == 0 || i == iterations)
                {
                    p.Refresh();
                    Log("Read counts i=" + i + " units=" + totalUnits + " empty=" + errorCount +
                        " privateMB=" + (p.PrivateMemorySize64 / 1048576.0).ToString("F2") +
                        " deltaKB=" + ((p.PrivateMemorySize64 - baseline) / 1024));
                }
            }
            Assert.AreEqual(0, errorCount, "Some count reads returned empty/error; inspect wrapper log.");
        }

        [TestMethod]
        public void D06_Reinitialize_Once_NoMoneyOperations()
        {
            Connect();
            Log("Reinitialize START");
            string id = "DiagReinit_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            bool ok = _wrapper.Reinitialize(id, Host, Port);
            Log("Reinitialize END ok=" + ok + " error=" + _wrapper.GetLastError());
            Assert.IsTrue(ok, "Reinitialize failed; inspect wrapper log for AddCallbacks/Uninitialize ordering.");
            Assert.IsTrue(HasCallback(_wrapper, "_cashAcceptCallback"));
        }

        [TestMethod]
        public void D07_GetNonDispensable_ReadOnly()
        {
            Connect();
            ArrayList units = _wrapper.GetNonDispensableCashCounts();
            Log("NonDispensable: count=" + (units == null ? -1 : units.Count) + " lastError=" + _wrapper.GetLastError());
            Assert.IsNotNull(units);
        }


        [TestMethod]
        public void D08_CallbackLifecycle_Stress_100Cycles()
        {
            const string emulatorHost = "100.72.2.50";
            const string emulatorPort = "3636";
            const int iterations = 100;

            string[] callbackFields =
            {
        "_deviceErrorCallback",
        "_cashAcceptCallback",
        "_cashAcceptCompleteCallback",
        "_deviceStatusCallback",
        "_socketClosedCallback",
        "_changeContextCallback",
        "_authenticationCallback",
        "_cmClosedCallback"
    };

            Process process = Process.GetCurrentProcess();

            string clientId = "D08_" + process.Id + "_START";

            Log("D08 START. Host=" + emulatorHost +
                ", Port=" + emulatorPort +
                ", Iterations=" + iterations);

            bool initialized = _wrapper.Initialize(
                clientId, emulatorHost, emulatorPort);

            _initialized = initialized;

            Assert.IsTrue(initialized,
                "Initial Initialize failed: " + _wrapper.GetLastError());

            process.Refresh();
            long initialMemory = process.PrivateMemorySize64;
            long maxMemory = initialMemory;

            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int i = 1; i <= iterations; i++)
            {
                string newClientId = "D08_" + process.Id + "_" + i;

                // Reinitialize выполняет:
                // Uninitialize -> Initialize -> AddCallbacks
                bool ok = _wrapper.Reinitialize(
                    newClientId, emulatorHost, emulatorPort);

                if (!ok)
                {
                    Log("D08 FAILED at cycle=" + i +
                        ", LastError=" + _wrapper.GetLastError());
                }

                Assert.IsTrue(ok,
                    "Reinitialize failed at cycle " + i +
                    ": " + _wrapper.GetLastError());

                // Проверяем флаг регистрации
                bool registered = (bool)Field(
                    _wrapper, "_callbacksRegistered");

                Assert.IsTrue(registered,
                    "Callbacks not registered at cycle " + i);

                // Проверяем все восемь делегатов
                foreach (string fieldName in callbackFields)
                {
                    Assert.IsTrue(
                        HasCallback(_wrapper, fieldName),
                        "Callback missing at cycle " + i +
                        ": " + fieldName);
                }

                // Проверяем состояние доставки событий
                _wrapper.AdviseEvents();

                Assert.IsTrue(
                    EventsEnabled(_wrapper),
                    "Events not enabled at cycle " + i);

                _wrapper.UnadviseEvents();

                Assert.IsFalse(
                    EventsEnabled(_wrapper),
                    "Events not disabled at cycle " + i);

                // UnadviseEvents не должен удалять callbacks
                Assert.IsTrue(
                    (bool)Field(_wrapper, "_callbacksRegistered"),
                    "Callbacks unexpectedly removed at cycle " + i);

                // Контроль памяти каждые 10 циклов
                if (i % 10 == 0)
                {
                    process.Refresh();

                    long currentMemory = process.PrivateMemorySize64;

                    if (currentMemory > maxMemory)
                        maxMemory = currentMemory;

                    Log(
                        "D08 cycle=" + i +
                        "/" + iterations +
                        " privateMB=" +
                        (currentMemory / 1048576.0).ToString("F2") +
                        " deltaKB=" +
                        ((currentMemory - initialMemory) / 1024) +
                        " callbacks=8" +
                        " registered=True" +
                        " elapsedSec=" +
                        stopwatch.Elapsed.TotalSeconds.ToString("F1")
                    );
                }
            }

            stopwatch.Stop();

            process.Refresh();

            long finalMemory = process.PrivateMemorySize64;

            Log("D08 COMPLETED");
            Log("Cycles=" + iterations);
            Log("TotalSeconds=" +
                stopwatch.Elapsed.TotalSeconds.ToString("F1"));
            Log("InitialPrivateMB=" +
                (initialMemory / 1048576.0).ToString("F2"));
            Log("FinalPrivateMB=" +
                (finalMemory / 1048576.0).ToString("F2"));
            Log("MaxPrivateMB=" +
                (maxMemory / 1048576.0).ToString("F2"));
            Log("MemoryDeltaKB=" +
                ((finalMemory - initialMemory) / 1024));
            Log("PASS: 100 reinitializations completed.");
        }


        [TestMethod]
        public void D09A_Callback_NoContext_MustNotCallOneC()
        {
            SynchronizationContext previous =
                SynchronizationContext.Current;

            try
            {
                SynchronizationContext.SetSynchronizationContext(null);

                FakeOneCReceiver receiver = new FakeOneCReceiver();

                _wrapper.SetConnection(receiver);
                _wrapper.AdviseEvents();

                Assert.IsNull(
                    Field(_wrapper, "_uiContext"),
                    "Unsafe fallback SynchronizationContext detected."
                );

                Log("D09A: Simulating SocketClosed callback.");

                SimulateSocketClosed();

                Assert.AreEqual(
                    0,
                    receiver.CallCount,
                    "OneC receiver was invoked without UI context."
                );

                Log("PASS D09A: No COM dispatch without context.");
            }
            finally
            {
                _wrapper.UnadviseEvents();

                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }


        [TestMethod]
        public void D09B_Callback_WithContext_DeliveredOnExpectedThread()
        {
            SynchronizationContext previous =
                SynchronizationContext.Current;

            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new InlineTestContext()
                );

                FakeOneCReceiver receiver = new FakeOneCReceiver();

                int expectedThread =
                    Thread.CurrentThread.ManagedThreadId;

                _wrapper.SetConnection(receiver);
                _wrapper.AdviseEvents();

                Log("D09B: Simulating SocketClosed callback.");

                SimulateSocketClosed();

                Assert.AreEqual(
                    1,
                    receiver.CallCount,
                    "OnSocketClosed was not invoked exactly once."
                );

                Assert.AreEqual(
                    expectedThread,
                    receiver.LastThreadId,
                    "Callback executed on unexpected thread."
                );

                Assert.IsFalse(
                    string.IsNullOrEmpty(receiver.LastMessage),
                    "Callback message was empty."
                );

                Log("PASS D09B: Callback delivered on expected thread.");
            }
            finally
            {
                _wrapper.UnadviseEvents();

                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }


        [TestMethod]
        public void D09C_CallbackRace_UnadviseWhileEventQueued()
        {
            Log("D09C START");

            using (var context = new StaPumpContext())
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                var receiver = new FakeOneCReceiver();

                // Имитируем SetConnection из основного STA-потока.
                context.Invoke(() =>
                {
                    _wrapper.SetConnection(receiver);
                });

                _wrapper.AdviseEvents();

                Log("D09C: STA thread=" + context.ThreadId);

                try
                {
                    // Намеренно блокируем обработку очереди.
                    // Это только тестовый STA-поток, не настоящий UI 1C.
                    context.Post(_ =>
                    {
                        entered.Set();
                        release.Wait();
                    }, null);

                    Assert.IsTrue(
                        entered.Wait(5000),
                        "STA worker did not enter blocking action.");

                    // Имитируем callback NCR из фонового потока.
                    var callbackTask = System.Threading.Tasks.Task.Run(() =>
                    {
                        Log("D09C: Callback thread=" +
                            Thread.CurrentThread.ManagedThreadId);

                        SimulateSocketClosed();
                    });

                    Assert.IsTrue(
                        callbackTask.Wait(5000),
                        "Simulated callback did not complete.");

                    Log("D09C: Callback queued.");

                    // Событие уже в очереди, но ещё не обработано.
                    _wrapper.UnadviseEvents();

                    Assert.IsFalse(
                        EventsEnabled(_wrapper),
                        "Events should be disabled.");

                    Log("D09C: UnadviseEvents completed.");

                    // Разрешаем STA-потоку обработать очередь.
                    release.Set();

                    // Барьер: все события, стоящие перед ним,
                    // должны завершиться.
                    context.Invoke(() =>
                    {
                        Log("D09C: Queue drained.");
                    });

                    int calls = receiver.CallCount;

                    Log("D09C: Receiver calls after Unadvise=" + calls);

                    Assert.AreEqual(
                        0,
                        calls,
                        "BUG: Event was delivered after UnadviseEvents."
                    );

                    Log("PASS D09C: No late event delivery.");
                }
                finally
                {
                    release.Set();
                    _wrapper.UnadviseEvents();
                }
            }
        }


        // Add these members INSIDE your existing CTMWrapperDiagnosticTests class.
        // Requires existing _wrapper, Log(string), StaPumpContext from D09C.
        // Add using System.Reflection; using System.Threading; using System.Threading.Tasks;

        private sealed class D10Receiver
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int>
                _counts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

            private void Record(string name)
            {
                _counts.AddOrUpdate(name, 1, (key, oldValue) => oldValue + 1);
            }

            public int Count(string name)
            {
                int count;
                return _counts.TryGetValue(name, out count) ? count : 0;
            }

            public void OnDeviceError(string info) { Record("OnDeviceError"); }
            public void OnCashAccept(int amount, int amountDue, int denom, string currency, int type)
            { Record("OnCashAccept"); }
            public void OnCashAcceptComplete() { Record("OnCashAcceptComplete"); }
            public void OnDeviceStatus(object info) { Record("OnDeviceStatus"); }
            public void OnSocketClosed(string info) { Record("OnSocketClosed"); }
            public void OnChangeContext(string info) { Record("OnChangeContext"); }
            public void OnAuthentication(string user, bool isHCashier)
            { Record("OnAuthentication"); }
            public void OnCMClosed(string info) { Record("OnCMClosed"); }
        }

        private void D10_SimulateCallback(string methodName)
        {
            MethodInfo method = typeof(CTMWrapper).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing callback method: " + methodName);

            ParameterInfo[] args = method.GetParameters();
            object[] values = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                // Use zero/default structures. No NCR device APIs are invoked.
                Type type = args[i].ParameterType;
                values[i] = Activator.CreateInstance(type);
            }

            try
            {
                method.Invoke(_wrapper, values);
            }
            catch (TargetInvocationException ex)
            {
                throw new AssertFailedException(
                    methodName + " failed: " +
                    (ex.InnerException == null ? ex.ToString() : ex.InnerException.ToString()));
            }
        }

        [TestMethod]
        public void D10_AllCallbacks_LateDelivery()
        {
            string[] handlers =
            {
        "HandleDeviceError",
        "HandleCashAccept",
        "HandleCashAcceptComplete",
        "HandleDeviceStatus",
        "HandleSocketClosed",
        "HandleChangeContext",
        "HandleAuthentication",
        "HandleCMClosed"
    };

            string[] events =
            {
        "OnDeviceError",
        "OnCashAccept",
        "OnCashAcceptComplete",
        "OnDeviceStatus",
        "OnSocketClosed",
        "OnChangeContext",
        "OnAuthentication",
        "OnCMClosed"
    };

            Log("D10 START: testing all 8 callbacks, no NCR connection.");

            using (var context = new StaPumpContext())
            {
                var receiver = new D10Receiver();
                context.Invoke(() => _wrapper.SetConnection(receiver));

                try
                {
                    for (int i = 0; i < handlers.Length; i++)
                    {
                        string handler = handlers[i];
                        string eventName = events[i];
                        _wrapper.AdviseEvents();

                        // Positive control: verify the handler really delivers an event.
                        int before = receiver.Count(eventName);
                        Task positive = Task.Run(() => D10_SimulateCallback(handler));
                        Assert.IsTrue(positive.Wait(5000), "Positive callback timeout: " + handler);
                        context.Invoke(() => { }); // Queue barrier.
                        int after = receiver.Count(eventName);
                        Assert.AreEqual(before + 1, after,
                            "Positive control failed for " + handler + ". " +
                            "Check whether handler calls PostOneCEvent.");

                        // Race control: hold STA while callback queues its event.
                        using (var entered = new ManualResetEventSlim(false))
                        using (var release = new ManualResetEventSlim(false))
                        {
                            try
                            {
                                context.Post(_ =>
                                {
                                    entered.Set();
                                    release.Wait();
                                }, null);
                                Assert.IsTrue(entered.Wait(5000), "STA blocker timeout: " + handler);

                                Task background = Task.Run(() => D10_SimulateCallback(handler));
                                Assert.IsTrue(background.Wait(5000), "Background timeout: " + handler);

                                _wrapper.UnadviseEvents();
                                release.Set();
                                context.Invoke(() => { }); // Drain queued event.

                                int lateCount = receiver.Count(eventName);
                                Assert.AreEqual(after, lateCount,
                                    "Late event delivered after UnadviseEvents: " + eventName);

                                Log("PASS D10 " + handler + ": positive=1, late=0");
                            }
                            finally
                            {
                                release.Set();
                                _wrapper.UnadviseEvents();
                            }
                        }
                    }

                    Log("PASS D10: All 8 callbacks deliver when enabled and drop queued events when disabled.");
                }
                finally
                {
                    _wrapper.UnadviseEvents();
                }
            }
        }

        [TestMethod]
        public void D11A_Unadvise_DuringActiveCallback()
        {
            Log("D11A START");

            using (var context = new StaPumpContext())
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = true
                };

                try
                {
                    context.Invoke(() =>
                    {
                        _wrapper.SetConnection(receiver);
                    });

                    _wrapper.AdviseEvents();

                    Log("D11A: STA thread=" + context.ThreadId);

                    // Имитируем событие NCR из фонового потока.
                    var task = Task.Run(() =>
                    {
                        SimulateSocketClosed();
                    });

                    Assert.IsTrue(
                        task.Wait(5000),
                        "Callback producer did not finish.");

                    // Ждём, пока вызов получателя реально начнётся.
                    Assert.IsTrue(
                        receiver.Entered.Wait(5000),
                        "Receiver did not enter callback.");

                    Log("D11A: Receiver entered. Thread=" +
                        receiver.ThreadId);

                    Assert.AreEqual(
                        1,
                        Volatile.Read(ref receiver.CallCount));

                    // Событие уже исполняется!
                    _wrapper.UnadviseEvents();

                    Log("D11A: UnadviseEvents completed.");

                    Assert.IsFalse(
                        EventsEnabled(_wrapper));

                    // Уже запущенный callback не отменяется.
                    Assert.AreEqual(
                        1,
                        Volatile.Read(ref receiver.CallCount));

                    // Освобождаем тестовый обработчик.
                    receiver.Release.Set();

                    // Барьер: ждём окончания STA-обработчика.
                    context.Invoke(() =>
                    {
                        Log("D11A: STA callback completed.");
                    });

                    Assert.AreEqual(
                        1,
                        Volatile.Read(ref receiver.CallCount));

                    Assert.AreEqual(
                        context.ThreadId,
                        receiver.ThreadId,
                        "Callback executed outside STA thread.");

                    Log("PASS D11A: Active callback completed safely.");
                }
                finally
                {
                    receiver.Release.Set();
                    _wrapper.UnadviseEvents();
                }
            }
        }

        [TestMethod]
        public void D11B_DetachReceiver_WhileEventQueued()
        {
            Log("D11B START");

            using (var context = new StaPumpContext())
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = false
                };

                try
                {
                    context.Invoke(() =>
                    {
                        _wrapper.SetConnection(receiver);
                    });

                    _wrapper.AdviseEvents();

                    // Блокируем обработку очереди STA.
                    context.Post(_ =>
                    {
                        entered.Set();
                        release.Wait();
                    }, null);

                    Assert.IsTrue(
                        entered.Wait(5000),
                        "STA queue blocker did not start.");

                    // Ставим событие в очередь с фонового потока.
                    var task = Task.Run(() =>
                    {
                        SimulateSocketClosed();
                    });

                    Assert.IsTrue(
                        task.Wait(5000),
                        "Callback producer did not finish.");

                    Log("D11B: Callback queued.");

                    // Отключаем доставку событий.
                    _wrapper.UnadviseEvents();

                    // Имитируем отсоединение объекта 1С.
                    FieldInfo field = typeof(CTMWrapper).GetField(
                        "_oneCObject",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                    Assert.IsNotNull(
                        field,
                        "_oneCObject field not found.");

                    field.SetValue(_wrapper, null);

                    Log("D11B: Receiver detached.");

                    // Разрешаем обработку ожидающего события.
                    release.Set();

                    context.Invoke(() =>
                    {
                        Log("D11B: STA queue drained.");
                    });

                    Assert.AreEqual(
                        0,
                        Volatile.Read(ref receiver.CallCount),
                        "BUG: Detached receiver was invoked.");

                    Log("PASS D11B: Detached receiver not invoked.");
                }
                finally
                {
                    release.Set();
                    _wrapper.UnadviseEvents();
                }
            }
        }




        [TestMethod]
        public void D12_Unadvise_DuringActiveCallback_Stress()
        {
            const int iterations = 100;

            Log("D12 START. Iterations=" + iterations);

            int returnedWhileActive = 0;
            int waitedForCallback = 0;

            using (var context = new StaPumpContext())
            {
                for (int i = 1; i <= iterations; i++)
                {
                    var receiver = new D11Receiver
                    {
                        BlockOnCall = true
                    };

                    using (var unadviseStarted = new ManualResetEventSlim(false))
                    using (var unadviseFinished = new ManualResetEventSlim(false))
                    {
                        Task unadviseTask = null;

                        try
                        {
                            context.Invoke(() =>
                            {
                                _wrapper.SetConnection(receiver);
                            });

                            _wrapper.AdviseEvents();

                            // Имитируем callback NCR из фонового потока.
                            Task callbackTask = Task.Run(() =>
                            {
                                SimulateSocketClosed();
                            });

                            Assert.IsTrue(
                                callbackTask.Wait(5000),
                                "Callback producer timeout at iteration " + i);

                            // Ждём входа получателя в OnSocketClosed.
                            Assert.IsTrue(
                                receiver.Entered.Wait(5000),
                                "Receiver did not enter at iteration " + i);

                            // Запускаем UnadviseEvents из другого потока.
                            unadviseTask = Task.Run(() =>
                            {
                                unadviseStarted.Set();

                                _wrapper.UnadviseEvents();

                                unadviseFinished.Set();
                            });

                            Assert.IsTrue(
                                unadviseStarted.Wait(5000),
                                "Unadvise thread did not start.");

                            // Получатель всё ещё заблокирован.
                            // Проверяем, успел ли UnadviseEvents завершиться.
                            bool returnedEarly =
                                unadviseFinished.Wait(200);

                            if (returnedEarly)
                                returnedWhileActive++;
                            else
                                waitedForCallback++;

                            if (i <= 5 || i % 10 == 0)
                            {
                                Log(
                                    "D12 cycle=" + i +
                                    " unadviseReturnedWhileActive=" +
                                    returnedEarly +
                                    " calls=" +
                                    Volatile.Read(ref receiver.CallCount));
                            }

                            // Освобождаем уже выполняющийся callback.
                            receiver.Release.Set();

                            // Дожидаемся завершения STA callback.
                            context.Invoke(() => { });

                            Assert.IsTrue(
                                unadviseTask.Wait(5000),
                                "Unadvise timeout at iteration " + i);

                            Assert.AreEqual(
                                1,
                                Volatile.Read(ref receiver.CallCount),
                                "Unexpected callback count at iteration " + i);
                        }
                        finally
                        {
                            // Обязательно освобождаем блокировку,
                            // даже если Assertion завершился ошибкой.
                            receiver.Release.Set();

                            if (unadviseTask != null)
                            {
                                try
                                {
                                    unadviseTask.Wait(5000);
                                }
                                catch (AggregateException)
                                {
                                    // Исключение будет видно в основном тесте,
                                    // если оно возникло при штатной проверке.
                                }
                            }

                            _wrapper.UnadviseEvents();
                        }
                    }
                }
            }

            Log("D12 COMPLETED");
            Log("Iterations=" + iterations);
            Log("UnadviseReturnedWhileCallbackActive=" +
                returnedWhileActive);
            Log("UnadviseWaitedBeyond200ms=" +
                waitedForCallback);

            Assert.AreEqual(
                iterations,
                returnedWhileActive + waitedForCallback);

            Log("PASS D12: Callback/Unadvise ordering measured.");
        }



        private sealed class D13CallbackGate
        {
            private readonly object _sync = new object();

            private bool _accepting = true;
            private int _active = 0;

            private readonly TaskCompletionSource<bool> _stopped =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public int ActiveCount
            {
                get
                {
                    lock (_sync)
                        return _active;
                }
            }

            public IDisposable TryEnter()
            {
                lock (_sync)
                {
                    if (!_accepting)
                        return null;

                    _active++;
                    return new CallbackLease(this);
                }
            }

            public Task ShutdownAsync()
            {
                lock (_sync)
                {
                    _accepting = false;

                    if (_active == 0)
                        _stopped.TrySetResult(true);

                    return _stopped.Task;
                }
            }

            private void Leave()
            {
                lock (_sync)
                {
                    _active--;

                    if (_active < 0)
                        throw new InvalidOperationException(
                            "D13: Negative active callback count.");

                    if (!_accepting && _active == 0)
                        _stopped.TrySetResult(true);
                }
            }

            private sealed class CallbackLease : IDisposable
            {
                private D13CallbackGate _owner;

                public CallbackLease(D13CallbackGate owner)
                {
                    _owner = owner;
                }

                public void Dispose()
                {
                    D13CallbackGate owner =
                        Interlocked.Exchange(ref _owner, null);

                    if (owner != null)
                        owner.Leave();
                }
            }
        }



        [TestMethod]
        public void D13A_ShutdownWithoutActiveCallbacks()
        {
            Log("D13A START");

            var gate = new D13CallbackGate();

            Task shutdown = gate.ShutdownAsync();

            Assert.AreEqual(
                TaskStatus.RanToCompletion,
                shutdown.Status,
                "Shutdown should complete immediately.");

            Assert.AreEqual(0, gate.ActiveCount);

            Assert.IsNull(
                gate.TryEnter(),
                "New callback accepted after shutdown.");

            Log("PASS D13A: Immediate shutdown, new callbacks rejected.");
        }


        [TestMethod]
        public void D13B_ShutdownWaitsForActiveCallback()
        {
            Log("D13B START");

            var gate = new D13CallbackGate();

            using (var context = new StaPumpContext())
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                try
                {
                    context.Post(_ =>
                    {
                        IDisposable lease = gate.TryEnter();

                        if (lease == null)
                            return;

                        using (lease)
                        {
                            entered.Set();

                            if (!release.Wait(5000))
                                throw new TimeoutException(
                                    "D13B: Callback release timeout.");
                        }

                    }, null);

                    Assert.IsTrue(
                        entered.Wait(5000),
                        "Callback did not start.");

                    Assert.AreEqual(1, gate.ActiveCount);

                    Log("D13B: Callback active on STA.");

                    Task shutdown = gate.ShutdownAsync();

                    Assert.IsFalse(
                        shutdown.IsCompleted,
                        "Shutdown completed while callback was active.");

                    Assert.IsNull(
                        gate.TryEnter(),
                        "New callback admitted during shutdown.");

                    Log("D13B: Shutdown pending, new callbacks rejected.");

                    release.Set();

                    // Барьер очереди STA.
                    context.Invoke(() => { });

                    Assert.IsTrue(
                        shutdown.Wait(5000),
                        "Shutdown did not finish.");

                    Assert.AreEqual(0, gate.ActiveCount);

                    Log("PASS D13B: Shutdown completed after callback.");
                }
                finally
                {
                    release.Set();
                }
            }
        }


        [TestMethod]
        public void D13C_ShutdownFromCallback_NoDeadlock()
        {
            Log("D13C START");

            var gate = new D13CallbackGate();
            Task shutdown = null;

            using (var context = new StaPumpContext())
            {
                context.Invoke(() =>
                {
                    using (IDisposable lease = gate.TryEnter())
                    {
                        Assert.IsNotNull(
                            lease,
                            "Callback entry rejected unexpectedly.");

                        Assert.AreEqual(1, gate.ActiveCount);

                        // Остановка инициируется из самого callback.
                        shutdown = gate.ShutdownAsync();

                        // ShutdownAsync не блокирует STA.
                        Assert.IsFalse(
                            shutdown.IsCompleted,
                            "Shutdown completed before callback exit.");

                        Assert.IsNull(gate.TryEnter());

                        Log("D13C: Shutdown requested from STA callback.");
                    }

                    // Здесь lease.Dispose() уже выполнен.
                });

                Assert.IsNotNull(shutdown);

                Assert.IsTrue(
                    shutdown.Wait(5000),
                    "Shutdown did not complete.");

                Assert.AreEqual(0, gate.ActiveCount);

                Log("PASS D13C: No STA deadlock.");
            }
        }


        [TestMethod]
        public void D14A_RealWrapper_ShutdownWaitsForActiveEvent()
        {
            Log("D14A START");

            using (var context = new StaPumpContext())
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = true
                };

                Task shutdown = null;

                try
                {
                    context.Invoke(() =>
                    {
                        _wrapper.SetConnection(receiver);
                    });

                    _wrapper.AdviseEvents();

                    // Запускаем callback на другом потоке.
                    Task producer = Task.Run(() =>
                    {
                        SimulateSocketClosed();
                    });

                    Assert.IsTrue(
                        producer.Wait(5000),
                        "Callback producer timeout.");

                    // Ждём начала реального InvokeOneCEvent.
                    Assert.IsTrue(
                        receiver.Entered.Wait(5000),
                        "OnSocketClosed did not start.");

                    Log("D14A: Receiver entered.");

                    Assert.AreEqual(
                        1,
                        Volatile.Read(ref receiver.CallCount));

                    // Запрашиваем завершение компоненты.
                    shutdown = _wrapper.ShutdownOneCEventsAsync();

                    Assert.IsFalse(
                        shutdown.IsCompleted,
                        "Shutdown finished while callback was active.");

                    Log("D14A: Shutdown pending.");

                    // Новый callback после начала shutdown.
                    SimulateSocketClosed();

                    // Освобождаем уже выполняющийся callback.
                    receiver.Release.Set();

                    // Барьер STA-очереди.
                    context.Invoke(() => { });

                    Assert.IsTrue(
                        shutdown.Wait(5000),
                        "Shutdown timeout.");

                    Assert.AreEqual(
                        TaskStatus.RanToCompletion,
                        shutdown.Status);

                    Assert.AreEqual(
                        1,
                        Volatile.Read(ref receiver.CallCount),
                        "New event delivered after shutdown.");

                    Log("PASS D14A: Active event drained, new event rejected.");
                }
                finally
                {
                    receiver.Release.Set();

                    if (shutdown != null)
                    {
                        // После освобождения receiver задача должна
                        // завершиться без блокировки STA.
                        shutdown.Wait(5000);
                    }

                    _wrapper.UnadviseEvents();
                }
            }
        }



        [TestMethod]
        public void D14B_RealWrapper_QueuedEventRejectedAfterShutdown()
        {
            Log("D14B START");

            using (var context = new StaPumpContext())
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = false
                };

                try
                {
                    context.Invoke(() =>
                    {
                        _wrapper.SetConnection(receiver);
                    });

                    _wrapper.AdviseEvents();

                    // Занимаем STA-поток.
                    context.Post(_ =>
                    {
                        entered.Set();
                        release.Wait();
                    }, null);

                    Assert.IsTrue(
                        entered.Wait(5000),
                        "STA blocker did not start.");

                    // Помещаем событие в очередь.
                    Task producer = Task.Run(() =>
                    {
                        SimulateSocketClosed();
                    });

                    Assert.IsTrue(
                        producer.Wait(5000),
                        "Producer timeout.");

                    Log("D14B: Event queued.");

                    // Активных InvokeOneCEvent сейчас нет.
                    Task shutdown = _wrapper.ShutdownOneCEventsAsync();

                    Assert.AreEqual(
                        TaskStatus.RanToCompletion,
                        shutdown.Status,
                        "Shutdown should finish with no active calls.");

                    Log("D14B: Shutdown completed.");

                    release.Set();

                    context.Invoke(() => { });

                    Assert.AreEqual(
                        0,
                        Volatile.Read(ref receiver.CallCount),
                        "Queued event delivered after shutdown.");

                    Log("PASS D14B: Queued event rejected.");
                }
                finally
                {
                    release.Set();
                    _wrapper.UnadviseEvents();
                }
            }
        }



        [TestMethod]
        public void D15A_GateRemainsClosedAfterShutdown()
        {
            Log("D15A START");

            using (var context = new StaPumpContext())
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = false
                };

                context.Invoke(() =>
                {
                    _wrapper.SetConnection(receiver);
                });

                _wrapper.AdviseEvents();

                // Первое событие должно доставиться.
                SimulateSocketClosed();

                context.Invoke(() => { });

                Assert.AreEqual(
                    1,
                    Volatile.Read(ref receiver.CallCount));

                Log("D15A: First callback delivered.");

                // Закрываем gate.
                Task shutdown = _wrapper.ShutdownOneCEventsAsync();

                Assert.AreEqual(
                    TaskStatus.RanToCompletion,
                    shutdown.Status);

                // Пытаемся включить события старым способом.
                _wrapper.AdviseEvents();

                SimulateSocketClosed();

                context.Invoke(() => { });

                // Текущий OneCEventGate закрыт навсегда.
                Assert.AreEqual(
                    1,
                    Volatile.Read(ref receiver.CallCount),
                    "Unexpected callback after closed gate.");

                Log("PASS D15A: AdviseEvents does not reopen closed gate.");
            }
        }


        [TestMethod]
        public void D15B_OldEventsRejected_NewEventsDelivered()
        {
            Log("D15B START");

            using (var context = new StaPumpContext())
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = false
                };

                try
                {
                    context.Invoke(() =>
                    {
                        _wrapper.SetConnection(receiver);
                    });

                    _wrapper.AdviseEvents();

                    // Блокируем STA-поток.
                    context.Post(_ =>
                    {
                        entered.Set();

                        if (!release.Wait(5000))
                            throw new TimeoutException(
                                "D15B: STA blocker timeout.");
                    }, null);

                    Assert.IsTrue(
                        entered.Wait(5000),
                        "STA blocker did not start.");

                    // Событие старого жизненного цикла.
                    SimulateSocketClosed();

                    Log("D15B: Old callback queued.");

                    // Закрываем старый gate.
                    Task shutdown = _wrapper.ShutdownOneCEventsAsync();

                    Assert.AreEqual(
                        TaskStatus.RanToCompletion,
                        shutdown.Status);

                    Log("D15B: Old gate shutdown completed.");

                    // Создаём новый gate.
                    _wrapper.ResetOneCEvents();

                    // Событие нового жизненного цикла.
                    SimulateSocketClosed();

                    Log("D15B: New callback queued.");

                    // Освобождаем STA.
                    release.Set();

                    // Дожидаемся обработки обоих событий.
                    context.Invoke(() => { });

                    int calls = Volatile.Read(ref receiver.CallCount);

                    Log("D15B: Receiver calls=" + calls);

                    Assert.AreEqual(
                        1,
                        calls,
                        "Expected only new lifecycle callback.");

                    Log("PASS D15B: Old event rejected, new event delivered.");
                }
                finally
                {
                    release.Set();
                    _wrapper.UnadviseEvents();
                }
            }
        }



        [TestMethod]
        public void D16_FullLifecycle_Reconnect_Emulator()
        {
            const string host = "100.72.2.50";
            const string port = "3636";

            int pid = Process.GetCurrentProcess().Id;

            string client1 = "D16_" + pid + "_A";
            string client2 = "D16_" + pid + "_B";

            Log("D16 START");
            Log("Emulator=" + host + ":" + port);

            bool connected = false;

            using (var context = new StaPumpContext())
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = false
                };

                try
                {
                    // ============================================
                    // ЭТАП 1: Первое подключение
                    // ============================================

                    context.Invoke(() =>
                    {
                        _wrapper.SetConnection(receiver);
                    });

                    bool ok = _wrapper.Initialize(
                        client1, host, port);

                    Assert.IsTrue(ok,
                        "First Initialize failed: " +
                        _wrapper.GetLastError());

                    connected = true;
                    _initialized = true;

                    _wrapper.AdviseEvents();

                    Log("D16: First Initialize OK.");

                    // Искусственно вызываем управляемый callback.
                    SimulateSocketClosed();

                    context.Invoke(() => { });

                    Assert.AreEqual(
                        1,
                        Volatile.Read(ref receiver.CallCount),
                        "First callback not delivered.");

                    Log("D16: First callback delivered.");

                    // ============================================
                    // ЭТАП 2: Shutdown управляемых событий
                    // ============================================

                    Task shutdown =
                        _wrapper.ShutdownOneCEventsAsync();

                    Assert.IsTrue(
                        shutdown.Wait(5000),
                        "Managed shutdown timeout.");

                    Assert.AreEqual(
                        TaskStatus.RanToCompletion,
                        shutdown.Status);

                    Log("D16: Managed shutdown completed.");

                    // Новый callback теперь должен блокироваться.
                    SimulateSocketClosed();

                    context.Invoke(() => { });

                    Assert.AreEqual(
                        1,
                        Volatile.Read(ref receiver.CallCount),
                        "Callback delivered after shutdown.");

                    Log("D16: Callback rejected after shutdown.");

                    // ============================================
                    // ЭТАП 3: Нативное отключение
                    // ============================================

                    _wrapper.Uninitialize();

                    connected = false;
                    _initialized = false;

                    Log("D16: First Uninitialize completed.");

                    // ============================================
                    // ЭТАП 4: Новый жизненный цикл событий
                    // ============================================

                    _wrapper.ResetOneCEvents();

                    // Uninitialize мог сбросить _uiContext.
                    // Поэтому привязываем тестовый контекст снова.
                    context.Invoke(() =>
                    {
                        _wrapper.SetConnection(receiver);
                    });

                    Log("D16: Event gate reset.");

                    // ============================================
                    // ЭТАП 5: Повторное подключение к эмулятору
                    // ============================================

                    ok = _wrapper.Initialize(
                        client2, host, port);

                    Assert.IsTrue(ok,
                        "Second Initialize failed: " +
                        _wrapper.GetLastError());

                    connected = true;
                    _initialized = true;

                    _wrapper.AdviseEvents();

                    Log("D16: Second Initialize OK.");

                    // ============================================
                    // ЭТАП 6: Доставка события после reconnect
                    // ============================================

                    SimulateSocketClosed();

                    context.Invoke(() => { });

                    Assert.AreEqual(
                        2,
                        Volatile.Read(ref receiver.CallCount),
                        "Callback not restored after reconnect.");

                    Assert.AreEqual(
                        context.ThreadId,
                        receiver.ThreadId,
                        "Callback executed on unexpected thread.");

                    Log("D16: Callback after reconnect delivered.");

                    Log("PASS D16: Full lifecycle completed.");
                }
                finally
                {
                    // Не оставляем нативное соединение открытым
                    // после теста или Assert failure.
                    receiver.Release.Set();

                    _wrapper.UnadviseEvents();

                    if (connected)
                    {
                        try
                        {
                            Task finalShutdown =
                                _wrapper.ShutdownOneCEventsAsync();

                            if (finalShutdown.Wait(5000))
                            {
                                _wrapper.Uninitialize();
                                _initialized = false;
                            }
                            else
                            {
                                Log("D16 WARNING: Managed callbacks " +
                                    "still active; native Uninitialize skipped.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("D16 CLEANUP ERROR: " + ex);
                            throw;
                        }
                    }
                }
            }
        }



        [TestMethod]
        public void D17_FullLifecycle_Stress_100Cycles()
        {
            const string host = "100.72.2.50";
            const string port = "3636";
            const int iterations = 100;

            string[] callbackFields =
            {
        "_deviceErrorCallback",
        "_cashAcceptCallback",
        "_cashAcceptCompleteCallback",
        "_deviceStatusCallback",
        "_socketClosedCallback",
        "_changeContextCallback",
        "_authenticationCallback",
        "_cmClosedCallback"
    };

            Process process = Process.GetCurrentProcess();
            int pid = process.Id;

            Log("D17 START");
            Log("Host=" + host + ":" + port);
            Log("Iterations=" + iterations);

            process.Refresh();

            long initialMemory = process.PrivateMemorySize64;
            long maxMemory = initialMemory;

            Stopwatch stopwatch = Stopwatch.StartNew();

            int successfulConnections = 0;
            int successfulShutdowns = 0;
            int deliveredEvents = 0;

            bool connected = false;

            using (var context = new StaPumpContext())
            {
                var receiver = new D11Receiver
                {
                    BlockOnCall = false
                };

                try
                {
                    for (int i = 1; i <= iterations; i++)
                    {
                        string clientId =
                            "D17_" + pid + "_" + i;

                        // ==================================
                        // 1. Восстановление gate
                        // ==================================

                        if (i > 1)
                        {
                            _wrapper.ResetOneCEvents();
                        }

                        // После Uninitialize контекст
                        // может быть сброшен.
                        context.Invoke(() =>
                        {
                            _wrapper.SetConnection(receiver);
                        });

                        // ==================================
                        // 2. Подключение NCR
                        // ==================================

                        bool ok = _wrapper.Initialize(
                            clientId, host, port);

                        Assert.IsTrue(
                            ok,
                            "Initialize failed at cycle " + i +
                            ": " + _wrapper.GetLastError());

                        connected = true;
                        _initialized = true;
                        successfulConnections++;

                        // ==================================
                        // 3. Проверка 8 callbacks
                        // ==================================

                        Assert.IsTrue(
                            (bool)Field(
                                _wrapper, "_callbacksRegistered"),
                            "Callbacks not registered at cycle " + i);

                        foreach (string callbackField in callbackFields)
                        {
                            Assert.IsTrue(
                                HasCallback(_wrapper, callbackField),
                                "Missing callback at cycle " + i +
                                ": " + callbackField);
                        }

                        // ==================================
                        // 4. Проверка доставки события
                        // ==================================

                        _wrapper.AdviseEvents();

                        int before = Volatile.Read(
                            ref receiver.CallCount);

                        SimulateSocketClosed();

                        // Барьер очереди STA.
                        context.Invoke(() => { });

                        int after = Volatile.Read(
                            ref receiver.CallCount);

                        Assert.AreEqual(
                            before + 1,
                            after,
                            "Event not delivered at cycle " + i);

                        deliveredEvents++;

                        Assert.AreEqual(
                            context.ThreadId,
                            receiver.ThreadId,
                            "Unexpected event thread at cycle " + i);

                        // ==================================
                        // 5. Managed shutdown
                        // ==================================

                        Task shutdown =
                            _wrapper.ShutdownOneCEventsAsync();

                        Assert.IsTrue(
                            shutdown.Wait(5000),
                            "Managed shutdown timeout at cycle " + i);

                        Assert.AreEqual(
                            TaskStatus.RanToCompletion,
                            shutdown.Status,
                            "Shutdown failed at cycle " + i);

                        successfulShutdowns++;

                        // ==================================
                        // 6. После shutdown события запрещены
                        // ==================================

                        SimulateSocketClosed();

                        context.Invoke(() => { });

                        Assert.AreEqual(
                            after,
                            Volatile.Read(ref receiver.CallCount),
                            "Event delivered after shutdown at cycle " + i);

                        // ==================================
                        // 7. Отключение NCR
                        // ==================================

                        _wrapper.Uninitialize();

                        connected = false;
                        _initialized = false;

                        Assert.IsFalse(
                            (bool)Field(
                                _wrapper, "_callbacksRegistered"),
                            "Callbacks still registered at cycle " + i);

                        // ==================================
                        // 8. Контроль памяти
                        // ==================================

                        if (i % 10 == 0)
                        {
                            process.Refresh();

                            long current =
                                process.PrivateMemorySize64;

                            if (current > maxMemory)
                                maxMemory = current;

                            Log(
                                "D17 cycle=" + i +
                                "/" + iterations +
                                " privateMB=" +
                                (current / 1048576.0).ToString("F2") +
                                " deltaKB=" +
                                ((current - initialMemory) / 1024) +
                                " connections=" +
                                successfulConnections +
                                " shutdowns=" +
                                successfulShutdowns +
                                " events=" +
                                deliveredEvents +
                                " elapsedSec=" +
                                stopwatch.Elapsed.TotalSeconds.ToString("F1")
                            );
                        }
                    }

                    stopwatch.Stop();

                    process.Refresh();

                    long finalMemory =
                        process.PrivateMemorySize64;

                    Log("D17 COMPLETED");
                    Log("Iterations=" + iterations);
                    Log("Connections=" + successfulConnections);
                    Log("Shutdowns=" + successfulShutdowns);
                    Log("DeliveredEvents=" + deliveredEvents);
                    Log("InitialPrivateMB=" +
                        (initialMemory / 1048576.0).ToString("F2"));
                    Log("FinalPrivateMB=" +
                        (finalMemory / 1048576.0).ToString("F2"));
                    Log("MaxPrivateMB=" +
                        (maxMemory / 1048576.0).ToString("F2"));
                    Log("MemoryDeltaKB=" +
                        ((finalMemory - initialMemory) / 1024));
                    Log("TotalSeconds=" +
                        stopwatch.Elapsed.TotalSeconds.ToString("F1"));

                    Assert.AreEqual(
                        iterations, successfulConnections);

                    Assert.AreEqual(
                        iterations, successfulShutdowns);

                    Assert.AreEqual(
                        iterations, deliveredEvents);

                    Log("PASS D17: 100 full lifecycle cycles completed.");
                }
                finally
                {
                    receiver.Release.Set();
                    _wrapper.UnadviseEvents();

                    if (connected)
                    {
                        try
                        {
                            Task shutdown =
                                _wrapper.ShutdownOneCEventsAsync();

                            if (shutdown.Wait(5000) &&
                                shutdown.Status == TaskStatus.RanToCompletion)
                            {
                                _wrapper.Uninitialize();
                                _initialized = false;
                            }
                            else
                            {
                                Log("D17 WARNING: Managed shutdown " +
                                    "incomplete; Uninitialize skipped.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("D17 CLEANUP ERROR: " + ex);
                        }
                    }
                }
            }
        }

















    }





















}
