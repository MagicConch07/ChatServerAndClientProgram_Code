using System;
using System.Diagnostics;
using System.IO;
using System.Text;

public class Log : Singleton<Log>
{
    const int LogBufferSize = 1024 * 1024 * 4;
    const int MaxFileSize = 1024 * 1024 * 3; // 3MB
    private static char[] LogBuffer = new char[LogBufferSize];
    private static int LogBufferCount = 0;
    private static SpinLock _spinLock = new SpinLock();
    private static int LogSecond = -1;
    private static string CurrentLogFile;
    private static long CurrentFileSize = 0;

    const int TRC_SWITCH_MAX_CNT = 512;
    static bool[] TRACE_LOG_SWITCH_INIT = new bool[TRC_SWITCH_MAX_CNT];
    static int[] TRACE_LOG_SWITCH_FLAG = new int[TRC_SWITCH_MAX_CNT];

    StringBuilder sbMethod = new StringBuilder(256);
    StringBuilder sb = new StringBuilder(1024);

    public enum LogLevel
    {
        TRC_NOLOG,
        TRC_ANY,
        TRC_CRITICAL,
        TRC_MAJOR,
        TRC_MINOR,
        TRC_WARNING,
        TRC_MSG,
        TRC_DATA,
    }

    public enum LogId
    {
        SYSTEM,
        TIMER,
        NET,
        PACKET,
        ITEM,
        PATH,
        SVO,
        ASTAR,
        CHARACTER,
        NPC,
        SKILLBUFF,
        BROADCAST,
    }

    public Log()
    {
        CreateNewLogFile();
    }

    private string GenerateLogFileName()
    {
        DateTime now = DateTime.Now;
        return $"log_{now:yyyyMMdd}_{now:HHmmss}_{now.Millisecond:D3}.txt";
    }

    private void CreateNewLogFile()
    {
        CurrentLogFile = GenerateLogFileName();
        CurrentFileSize = 0;

        // Create directory if it doesn't exist
        string directory = Path.GetDirectoryName(CurrentLogFile);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void InitLog()
    {
        for (int i = 0; i < TRC_SWITCH_MAX_CNT; i++)
        {
            TRACE_LOG_SWITCH_INIT[i] = false;
            TRACE_LOG_SWITCH_FLAG[i] = (int)LogLevel.TRC_NOLOG;
        }
    }

    public void LogSwitch(LogId logId, LogLevel logLvel)
    {
        TRACE_LOG_SWITCH_INIT[(int)logId] = true;
        TRACE_LOG_SWITCH_FLAG[(int)logId] = (int)logLvel;
    }

    private void WriteToFile(string content, bool forceFlush = false)
    {
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        long contentSize = contentBytes.Length;

        // Check if we need to create a new file
        if (CurrentFileSize + contentSize > MaxFileSize)
        {
            CreateNewLogFile();
        }

        using (FileStream logStream = new FileStream(CurrentLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        using (StreamWriter sw = new StreamWriter(logStream))
        {
            sw.Write(content);
        }

        CurrentFileSize += contentSize;
    }

    public void FileLog(LogId logId, LogLevel logLvel, string logMessage, bool writeNow = false)
    {
        _spinLock.Lock();
        try
        {
            if (LogLevel.TRC_NOLOG == logLvel)
            {
                return;
            }

            if (TRACE_LOG_SWITCH_INIT[(int)logId] == false)
            {
                return;
            }

            if (TRACE_LOG_SWITCH_FLAG[(int)logId] < (int)logLvel)
            {
                return;
            }

            sbMethod.Clear();
            StackFrame CallStack = new StackFrame(1, true);
            string SourceFile = Path.GetFileNameWithoutExtension(CallStack.GetFileName());

            StackFrame CallStack2 = new StackFrame(2, true);
            StackFrame CallStack3 = new StackFrame(3, true);

            if (CallStack3.GetMethod() != null)
            {
                sbMethod.Append(Path.GetFileNameWithoutExtension(CallStack3.GetFileName()));
                sbMethod.Append(":");
                sbMethod.Append(CallStack3.GetMethod().Name);
                sbMethod.Append("/" + CallStack3.GetFileLineNumber());
                sbMethod.Append(" > ");
            }

            if (CallStack2.GetMethod() != null)
            {
                sbMethod.Append(Path.GetFileNameWithoutExtension(CallStack2.GetFileName()));
                sbMethod.Append(":");
                sbMethod.Append(CallStack2.GetMethod().Name);
                sbMethod.Append("/" + CallStack2.GetFileLineNumber());
                sbMethod.Append(" > ");
            }
            sbMethod.Append(SourceFile);
            sbMethod.Append(":");
            sbMethod.Append(CallStack.GetMethod().Name);

            int SourceLine = CallStack.GetFileLineNumber();

            if (LogSecond == -1)
            {
                LogSecond = DateTime.Now.Second;
            }

            sb.Clear();
            DateTime now = DateTime.Now;
            sb.AppendFormat("{0:D2}:{1:D2}:{2:D3} {3:D2}/{4:D2}: ",
                now.Hour, now.Minute, now.Millisecond, now.Month, now.Day);
            sb.Append("[" + logId.ToString() + "] ");

            string replacedTabWith4Spaces = logMessage.Replace("\t", "    ");
            sb.Append(replacedTabWith4Spaces);
            if (replacedTabWith4Spaces.Length <= 50)
            {
                sb.Append(' ', 50 - replacedTabWith4Spaces.Length);
            }
            else if (replacedTabWith4Spaces.Length <= 70)
            {
                sb.Append(' ', 70 - replacedTabWith4Spaces.Length);
            }

            sb.Append("[");
            sb.Append(sbMethod);
            sb.Append("/");
            sb.Append(SourceLine);
            sb.Append("]\r\n");

            if (writeNow)
            {
                if (LogBufferCount > 0)
                {
                    WriteToFile(new string(LogBuffer, 0, LogBufferCount));
                    LogBufferCount = 0;
                }
                WriteToFile(sb.ToString());
                LogSecond = now.Second;
            }
            else
            {
                if (LogBufferSize <= sb.Length + LogBufferCount || LogSecond != now.Second)
                {
                    WriteToFile(new string(LogBuffer, 0, LogBufferCount));
                    LogBufferCount = 0;
                    LogSecond = now.Second;
                }
                sb.ToString().CopyTo(0, LogBuffer, LogBufferCount, sb.Length);
                LogBufferCount += sb.Length;
            }
        }
        finally
        {
            _spinLock.Unlock();
        }
    }
}
