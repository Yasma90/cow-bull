using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CowBull.Model
{
    public class Logging
    {
        private string sFilePath = string.Empty;

        public string FilePath
        {
            get { return sFilePath; }
            set { sFilePath = value; }
        }

        public void WriteLog(string logMessage)
        {
            if (FilePath.Length == 0)
                return;
            try
            {
                var writeStream = File.AppendText(FilePath);
                writeStream.Write("\r\nLog Entry : ");
                writeStream.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
                writeStream.WriteLine("  :");
                writeStream.WriteLine("  :{0}", logMessage);
                writeStream.WriteLine("-------------------------------");
                writeStream.Flush();
                writeStream.Close();
            }
            catch { }
        }
        public string FetchLog()
        {
            var logLine = string.Empty;
            var logEntry = string.Empty;
            if (FilePath.Length == 0)
                return logLine;
            try
            {
                var readStream = File.OpenText(FilePath);
                while ((logLine = readStream.ReadLine()) != null)
                {
                    logEntry += logLine;
                }
                readStream.Close();
            }
            catch { }
            return logLine;
        }
    }
}
