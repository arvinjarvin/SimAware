using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace SimAware.Client
{
    public sealed class SingletonApplicationEnforcer
    {
        readonly Action<IEnumerable<string>> processArgsFunc;
        readonly string applicationId;
        Thread thread;
        string argDelimiter = "_;;_";

        public string ArgDelimiter
        {
            get
            {
                return argDelimiter;
            }
            set
            {
                argDelimiter = value;
            }
        }

        public SingletonApplicationEnforcer(Action<IEnumerable<string>> processArgsFunc,
            string applicationId)
        {
            if(processArgsFunc == null)
            {
                throw new ArgumentNullException("processArgsFunc");
            }
            this.processArgsFunc = processArgsFunc;
            this.applicationId = applicationId;
        }

        public bool ShouldApplicationExit()
        {
            bool createdNew;
            string argsWaitHandleName = "ArgsWaitHandle_" + applicationId;
            string memoryFileName = "ArgFile_" + applicationId;

            EventWaitHandle argsWaitHandle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                argsWaitHandleName,
                out createdNew);

            GC.KeepAlive(argsWaitHandle);

            if(createdNew)
            {
                thread = new Thread(() =>
                {
                    try
                    {
                        using (MemoryMappedFile file = MemoryMappedFile.CreateOrOpen(memoryFileName, 10000))
                        {
                            while(true)
                            {
                                argsWaitHandle.WaitOne();
                                using (MemoryMappedViewStream stream = file.CreateViewStream())
                                {
                                    var reader = new BinaryReader(stream);
                                    string args;
                                    try
                                    {
                                        args = reader.ReadString();
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine("Unable to retrieve string. " + ex);
                                        continue;
                                    }
                                    string[] argsSplit = args.Split(new string[] { argDelimiter }, StringSplitOptions.RemoveEmptyEntries);

                                    processArgsFunc(argsSplit);
                                }
                            }
                        }
                    }
                    catch ( Exception ex)
                    {
                        Debug.WriteLine("Unable to monitor memory file. " + ex);
                    }
                });
                thread.IsBackground = true;
                thread.Start();
            }
            else
            {
                try
                {
                    using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(memoryFileName))
                    {
                        using (MemoryMappedViewStream stream = mmf.CreateViewStream())
                        {
                            var writer = new BinaryWriter(stream);
                            string[] args = Environment.GetCommandLineArgs();
                            string joined = string.Join(argDelimiter, args);
                            writer.Write(joined);
                        }
                    }
                    argsWaitHandle.Set();
                }
                catch { }
            }

            return !createdNew;
        }
    }
}
