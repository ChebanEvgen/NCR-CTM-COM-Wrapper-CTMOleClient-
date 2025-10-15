using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CTMOnCSharp
{
    // Singleton Class
    public sealed class Utils
    {
        public readonly HardwareType hardwareType;
        public readonly Dictionary<string, string> Properties;

        private Utils()
        {
            hardwareType = getHWTypeFromRegistry();
            // Use the CTM_HOME environment variable for the correct path to Config
            string ctmHome = Environment.GetEnvironmentVariable("CTM_HOME");
            if (string.IsNullOrEmpty(ctmHome))
            {
                throw new InvalidOperationException("The CTM_HOME environment variable is not set. Set it to C:\\Program Files (x86)\\NCR\\CashTenderModule\\");
            }
            string configPath = Path.Combine(ctmHome, "Config", "rpcServer.properties");
            Properties = LoadProperties(configPath);
        }

        public static Utils Instance
        {
            get
            {
                return Nested.instance;
            }
        }

        private class Nested
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static Nested()
            {
            }

            internal static readonly Utils instance = new Utils();
        }

        public IntPtr convertIntToIntPtr(int number)
        {
            // Convert int to string
            string value = number.ToString();

            // Use ToCharArray to convert string to array.
            char[] array = value.ToCharArray();
            Int32[] intArray = new Int32[array.Length];

            for (int i = 0; i < array.Length; i++)
            {
                intArray[i] = Convert.ToInt32(array[i].ToString());
            }

            // Allocate unmanaged memory
            IntPtr pUnmanagedBuffer = (IntPtr)Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Int32)) * intArray.Length);

            // Copy data to unmanaged buffer
            Marshal.Copy(intArray, 0, pUnmanagedBuffer, intArray.Length);

            // Pin object to create fixed address
            GCHandle handle = GCHandle.Alloc(pUnmanagedBuffer, GCHandleType.Pinned);
            IntPtr ppUnmanagedBuffer = (IntPtr)handle.AddrOfPinnedObject();

            if (handle.IsAllocated)
            {
                handle.Free();
            }

            return ppUnmanagedBuffer;
        }

        private HardwareType getHWTypeFromRegistry()
        {
            using (RegistryKey x86Key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\NCR\\SCOT - Platform\\ObservedOptions"))
            {
                if (x86Key != null)
                {
                    Object o = x86Key.GetValue("HWType");
                    if (o != null)
                    {
                        if (o.ToString().Equals("SCOT6"))
                        {
                            return HardwareType.R6;
                        }
                    }
                }

                else
                {
                    using (RegistryKey x64Key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\NCR\\SCOT - Platform\\ObservedOptions"))
                    {
                        if (x64Key != null)
                        {
                            Object o = x64Key.GetValue("HWType");
                            if (o != null)
                            {
                                if (o.ToString().Equals("SCOT6"))
                                {
                                    return HardwareType.R6;
                                }
                            }
                        }
                    }
                }
            }

            return HardwareType.R5;
        }

        private Dictionary<string, string> LoadProperties(string path)
        {
            Dictionary<string, string> propData = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Configuration file not found: {path}");
                }
                string fileData = "";
                using (StreamReader sr = new StreamReader(path))
                {
                    fileData = sr.ReadToEnd().Replace("\r", "");
                }

                string[] kvp;
                string[] records = fileData.Split("\n".ToCharArray());
                foreach (string record in records)
                {
                    if (record.Contains("="))
                    {
                        kvp = record.Split("=".ToCharArray());
                        propData.Add(kvp[0], kvp[1]);
                    }
                }
            }
            catch (Exception e)
            {
                string error = e.ToString();
                // For debugging: you can log or throw an exception
                Console.WriteLine($"Error loading properties: {error}");
            }

            return (propData);
        }

        private string GetProperty(string propName)
        {
            if ((Properties != null) && Properties.ContainsKey(propName))
            {
                return Properties[propName];
            }
            return null;
        }
    }
}