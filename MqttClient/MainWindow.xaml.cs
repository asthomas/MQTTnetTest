using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using MqttTest.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MqttTestClient
{
    class NullLogger : MqttNetLogger
    {
        public NullLogger(string name) : base(name)
        {

        }

#if HAVE_SYNC
        public override void Publish(MqttNetLogLevel level, string source, string message, object[] args, Exception ex)
        {

        }
#endif
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : ApplicationWindow, INotifyPropertyChanged
    {
        string Host = "localhost";
        int Port = 1883;
        IMqttClient Client;
        Timer ReconnectTimer;
        Timer DataTimer;
        int DataPointCount = 2000;
        bool IsConnecting;
        Thread CommunicationThread;
        bool Paused = true;
        Dispatcher ClientDispatcher;
        MqttNetLogger Logger;
        IMqttClientOptions ClientOptions;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Closing += MainWindow_Closing;
            Logger = new NullLogger("testLogger");
            Logger.LogMessagePublished += Logger_LogMessagePublished;
            CommunicationThread = new Thread(new ThreadStart(StartMqttClient));
            CommunicationThread.Start();
            QualityOfService = MqttQualityOfServiceLevel.AtMostOnce;
            StatusPane.QosChanged += StatusPane_QosChanged;
            StatusPane.CodeStyleChanged += StatusPane_CodeStyleChanged;
        }

        private void StatusPane_QosChanged (object sender, EventArgs e)
        {
            ResetStats();
            ClientDispatcher.BeginInvoke((Action)(() => Disconnect()), DispatcherPriority.Send);
        }

        private void StatusPane_CodeStyleChanged(object sender, EventArgs e)
        {
            ResetStats();
            ClientDispatcher.BeginInvoke((Action)(() => Disconnect()), DispatcherPriority.Send);
        }

        private void Logger_LogMessagePublished(object sender, MqttNetLogMessagePublishedEventArgs e)
        {
            if (e.TraceMessage.Level == MqttNetLogLevel.Error
                || e.TraceMessage.Level == MqttNetLogLevel.Warning
                )
                SetStatus(e.TraceMessage.Message);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (ClientDispatcher != null)
            {
                ClientDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
        }

        private void Disconnect()
        {
#if HAVE_SYNC
            if (IsSync)
                Client.Disconnect();
            else
#endif
                Client.DisconnectAsync().Wait();
        }

        private void StartMqttClient()
        {
            ClientDispatcher = Dispatcher.CurrentDispatcher;
            SetStatus("Configuring");
            Thread.CurrentThread.Name = "Client MQTT Messaging";

#if HAVE_SYNC
            RunInUiThread(() => CodeStyle = IsSync ? "Synchronous" : "Asynchronous");
#else
            RunInUiThread(() => CodeStyle = "Asynchronous");
#endif
            MqttFactory factory = new MqttFactory();
            Client = factory.CreateMqttClient(Logger);
            Client.Connected += Client_Connected;
            Client.Disconnected += Client_Disconnected;
            Client.ApplicationMessageReceived += Client_ApplicationMessageReceived;
            ReconnectTimer = new Timer(ReconnectTimerTick, null, 1000, 5000);
            DataTimer = new Timer(DataTimerTick, null, 100, 10);
            Dispatcher.Run();
        }

        private void Client_Disconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            Paused = true;
            SetStatus("Disconnected");
            ResetStats();
        }

        private void ReconnectTimerTick(object state)
        {
            if (!Client.IsConnected && !IsConnecting)
            {
                ClientDispatcher.BeginInvoke((Action)(() => AttemptConnection()));
            }
        }

        private string PointName(int seqno)
        {
            return string.Format("test/point{0:D5}", seqno);
        }

        private async void AttemptConnection()
        {
            if (!Client.IsConnected && !IsConnecting)
            {
                try
                {
                    SetStatus("Connecting");
                    IsConnecting = true;
                    MqttClientOptionsBuilder optionsBuilder = new MqttClientOptionsBuilder()
                        .WithTcpServer(Host, Port)
                        .WithCommunicationTimeout(new TimeSpan(0, 0, 1, 30, 0))
                        ;

                    ClientOptions = optionsBuilder.Build();
#if HAVE_SYNC
                    if (IsSync)
                        Client.Connect(ClientOptions);
                    else
#endif
                        await Client.ConnectAsync(ClientOptions);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    IsConnecting = false;
                }
            }
        }

        private async void Client_Connected(object sender, MqttClientConnectedEventArgs e)
        {
            SetStatus(Client.IsConnected ? "Connected" : "Connection failed");
            try
            {
                if (Client.IsConnected)
                {
                    ClientDispatcher.Invoke(() => SendNewData());

                    //List<TopicFilter> filters = new List<TopicFilter>();
                    IList<MqttSubscribeResult> result;
                    for (int i = 0; i < DataPointCount; i++)
                    {
                        List<TopicFilter> filters = new List<TopicFilter>();
                        filters.Add(new TopicFilter(PointName(i), QualityOfService));
#if HAVE_SYNC
                        if (IsSync)
                            result = Client.Subscribe(filters);
                        else
#endif
                            await Client.SubscribeAsync(filters);
                    }
                    Paused = false;
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message);
            }
        }

        private void Client_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            string strVal = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            if (strVal.Contains("{"))
            {
                JObject json = JsonConvert.DeserializeObject(strVal) as JObject;
                JToken jsonRoot = json ?? json.Root;
                if (jsonRoot != null)
                {
                    object value = jsonRoot.SelectToken("Value");
                    if (value != null)
                    {
                        strVal = value.ToString();
                    }
                    else
                    {
                        strVal = "-1";
                    }
                }
            }
            UpdateOutOfOrderCount(strVal);
            UpdateCounts(0, 1);
        }

        private int Value = 0;
        private int IsSending = 0;

        private void DataTimerTick(object state)
        {
            if (Client.IsConnected && !Paused)
            {
                try
                {
                    ClientDispatcher.Invoke(() => SendNewData());
                }
                catch { }
            }
        }

        private async void SendNewData()
        {
            if (Client.IsConnected)
            {
                if (Interlocked.Exchange(ref IsSending, 1) == 0)
                {
                    try
                    {
                        Nvqt nvqt = new Nvqt();
                        for (int i = 0; i < DataPointCount; i++)
                        {
                            string strVal = (Value += 1).ToString();

                            nvqt.Name = PointName(i);
                            nvqt.Value = strVal;

                            //strVal = JsonConvert.SerializeObject(nvqt);

                            MqttApplicationMessage message = 
                                new MqttApplicationMessage()
                                {
                                    Topic = nvqt.Name,
                                    Payload = Encoding.UTF8.GetBytes(strVal),
                                    QualityOfServiceLevel = QualityOfService,
                                    Retain = false
                                }
                            ;
#if HAVE_SYNC
                           if (IsSync)
                                Client.Publish(message);
                            else
#endif
                                await Client.PublishAsync(message);
                        }
                        UpdateCounts(DataPointCount, 0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    Interlocked.Exchange(ref IsSending, 0);
                }
            }
        }

        private void BPause_Click(object sender, RoutedEventArgs e)
        {
            Paused = !Paused;
        }
    }
}
