using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MqttTest.Common
{
    public class ApplicationWindow : Window, INotifyPropertyChanged
    {
#pragma warning disable CS0067 // Fody uses this property, but the compiler doesn't know it
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        private int _MessageCount;
        public int MessageCount { get; set; }

        private int _SentCount;
        public int SentCount { get; set; }

        private int _ReceivedCount;
        public int ReceivedCount { get; set; }

        public DateTime FirstMessageTime { get; set; }
        public double MessageRate { get; set; }
        public string Status { get; private set; }
        public string CodeStyle { get; set; }
        public bool IsSync { get { return CodeStyle.Equals("Synchronous", StringComparison.InvariantCultureIgnoreCase); } }
        public double CpuUsage { get; set; }

        private int _OutOfOrderCount;
        public int OutOfOrderCount { get; set; }

        private int _MissingValueCount;
        public int MissingValueCount { get; set; }

        private int _DuplicateValueCount;
        public int DuplicateValueCount { get; set; }

        public MqttQualityOfServiceLevel QualityOfService { get; set; }

        protected DateTime LastWindowUpdate = DateTime.Now - new TimeSpan(0,0,30);
        private int _LastMessageNumber = -1;
        public int LastMessageNumber { get; set; }
        protected int LastUpdateCount = 0;

        public static string[] Args;
        public delegate void Lambda();

        private const int HistorySize = 2000;
        private long[] MessageHistory = new long[HistorySize];
        private long[] MessageHistoryPrevious = null;
        private bool HavePreviousHistory = false;
        private int HistoryNext = 0;

        private PerformanceCounter CpuCounter = null;
        private Timer CpuTimer;

        public ApplicationWindow()
        {
            MqttNetGlobalLogger.LogMessagePublished += MqttNetGlobalLogger_LogMessagePublished;
            Loaded += ApplicationWindow_Loaded;
            GetCpuCounter();
#if HAVE_SYNC
            CodeStyle = Args.Contains("-a") ? "Asynchronous" : "Synchronous";
#else
            CodeStyle = "Synchronous";
#endif
            CpuTimer = new Timer(CpuTimerTick, null, 2000, 2000);

            Closing += (s, e) => CpuTimer.Dispose();
        }

        private void GetCpuCounter()
        {
            string procName = GetProcessInstanceName(Process.GetCurrentProcess().Id);
            if (!string.IsNullOrEmpty(procName))
            {
                CpuCounter = new PerformanceCounter("Process", "% Processor Time", procName);
            }
        }

        private string GetProcessInstanceName(int pid)
        {
            PerformanceCounterCategory cat = new PerformanceCounterCategory("Process");
            string procName = Process.GetCurrentProcess().ProcessName;
            string[] instances = cat.GetInstanceNames().Where(x => x.Contains(procName)).ToArray();
            foreach (string instance in instances)
            {

                using (PerformanceCounter cnt = new PerformanceCounter("Process",
                     "ID Process", instance, true))
                {
                    int val = (int)cnt.RawValue;
                    if (val == pid)
                    {
                        return instance;
                    }
                }
            }
            return null;
        }

        private void CpuTimerTick(object state)
        {
            if (CpuCounter != null)
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    CpuUsage = CpuCounter.NextValue();
                }));
            }
        }

        public void RunInUiThread(Lambda code)
        {
            if (Dispatcher.CheckAccess())
                code.DynamicInvoke();
            else
                Dispatcher.BeginInvoke((Action)(() => code.DynamicInvoke()));
        }

        private void ApplicationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            object element = FindName("StatusPane");
            StatusControl statusPane = element as StatusControl;
            if (statusPane != null)
            {
                statusPane.ResetClicked += StatusPane_ResetClicked;
            }
        }

        public void ResetStats()
        {
            _MessageCount = _SentCount = _ReceivedCount = 0;
            MessageCount = SentCount = ReceivedCount = 0;
            _OutOfOrderCount = _MissingValueCount = 0;
            OutOfOrderCount = MissingValueCount = 0;
            _DuplicateValueCount = DuplicateValueCount = 0;
            _LastMessageNumber = -1;
            MessageHistory = new long[HistorySize];
            MessageHistoryPrevious = null;
            HavePreviousHistory = false;
            HistoryNext = 0;
        }

        private void StatusPane_ResetClicked(object sender, EventArgs e)
        {
            ResetStats();
        }

        private void MqttNetGlobalLogger_LogMessagePublished(object sender, MqttNetLogMessagePublishedEventArgs e)
        {
            if (e.TraceMessage.Level == MqttNetLogLevel.Error 
                || e.TraceMessage.Level == MqttNetLogLevel.Warning)
            {
                SetStatus(e.TraceMessage.Message);
            }
        }

        public void UpdateCounts(int incrementSent, int incrementReceived)
        {
            if (_MessageCount == 0)
                RunInUiThread(() => FirstMessageTime = DateTime.Now);

            Interlocked.Add(ref _SentCount, incrementSent);
            Interlocked.Add(ref _ReceivedCount, incrementReceived);
            _MessageCount = _SentCount + _ReceivedCount;

            if (false
                // || (incrementSent + incrementReceived) > 1 
                || _MessageCount - LastUpdateCount >= 10000
                || (DateTime.Now - LastWindowUpdate).TotalMilliseconds > 1000)
            {
                LastWindowUpdate = DateTime.Now;
                LastUpdateCount = _MessageCount;

                RunInUiThread(() =>
                {
                    MessageCount = _MessageCount;
                    SentCount = _SentCount;
                    ReceivedCount = _ReceivedCount;
                    OutOfOrderCount = _OutOfOrderCount;
                    MissingValueCount = _MissingValueCount;
                    LastMessageNumber = _LastMessageNumber;
                    DuplicateValueCount = _DuplicateValueCount;
                    MessageRate = (double)MessageCount / (DateTime.Now - FirstMessageTime).TotalSeconds;
                });
            }
        }

        object UpdateLock = new object() { };

        public void UpdateOutOfOrderCount(string strVal)
        {
            lock (UpdateLock)
            {
                int iVal = Convert.ToInt32(strVal);
                if (iVal < _LastMessageNumber + 1)
                {
                    Interlocked.Increment(ref _OutOfOrderCount);
                }
                _LastMessageNumber = iVal;

                MessageHistory[HistoryNext++] = iVal;

                // We have a full message history.  Compute missing messages.
                if (HistoryNext == MessageHistory.Length)
                {
                    if (HavePreviousHistory)
                    {
                        List<long> tempHistory = new List<long>(MessageHistory);
                        tempHistory.AddRange(MessageHistoryPrevious);
                        tempHistory.Sort();

                        for (long i = 1, last = tempHistory[0]; i < HistorySize; i++)
                        {
                            long curval = tempHistory[(int)i];
                            if (curval > last + 1)
                            {
                                Interlocked.Add(ref _MissingValueCount, (int)(curval - last - 1));
                            }
                            else if (curval == last)
                            {
                                Interlocked.Increment(ref _DuplicateValueCount);
                            }
                            last = curval;
                        }

                        MessageHistoryPrevious = tempHistory.Skip(HistorySize).ToArray();
                    }
                    else
                    {
                        MessageHistoryPrevious = MessageHistory;
                    }

                    MessageHistory = new long[HistorySize];
                    HavePreviousHistory = true;
                    HistoryNext = 0;
                }
            }
        }

        public void SetStatus(string message)
        {
            RunInUiThread(() => Status = string.Format("[{0}] {1}", DateTime.Now, message));
        }
    }
}
