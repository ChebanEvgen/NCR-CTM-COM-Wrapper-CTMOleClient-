using System;
using System.Runtime.InteropServices;

namespace CTMOleClient
{
    // Минимальный интерфейс IInitDone (для инициализации AddIn)
    [ComImport]
    [Guid("B1A1D7A4-7B2C-4E5A-8B3D-1E2F3A4B5C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitDone
    {
        void Init(object pConnection);
        void Done();
        void GetInfo(ref object[] pInfo);
    }

    // Базовый класс InitAddIn
    [ComVisible(true)]
    public abstract class InitAddIn : IInitDone
    {
        protected object _oneCObject;  // Объект 1С для вызовов событий (глобальный контекст или модуль)
        private int m_Version = 1000;  // Версия для 1C 8.3+

        public virtual void Init(object pConnection)
        {
            _oneCObject = pConnection;  // Сохраняем для вызовов из колбеков
            // Инициализация CTM (Utils.Instance) — перенести в наследника, если нужно
        }

        public virtual void Done()
        {
            _oneCObject = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void GetInfo(ref object[] pInfo)
        {
            pInfo = new object[1] { m_Version };
        }
    }

    // StandardAddIn — базовый для вашего CTMWrapper (наследует InitAddIn)
    [ComVisible(true)]
    public abstract class StandardAddIn : InitAddIn
    {
        protected StandardAddIn() : base() { }  // Пустой конструктор
    }
}