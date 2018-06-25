using MQTTnet;
using MQTTnet.Server;
using MqttTest.Common;
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

namespace MqttTestServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : ApplicationWindow, INotifyPropertyChanged
    {
        int PlainPort = 1883;
        IMqttServer Server;
        Thread ServerThread;
        Dispatcher ClientDispatcher;
        bool IsStopping;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Closing += MainWindow_Closing;
#if HAVE_SYNC
            CodeStyle = IsSync ? "Synchronous" : "Asynchronous";
#else
            CodeStyle = "Asynchronous";
#endif

            ServerThread = new Thread(StartMqttServer);
            ServerThread.Start();
            QualityOfService = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce;
            StatusPane.QosChanged += StatusPane_QosChanged;
            StatusPane.CodeStyleChanged += StatusPane_CodeStyleChanged;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            IsStopping = true;
            Disconnect();
        }

        private void StatusPane_QosChanged(object sender, EventArgs e)
        {
            ResetStats();
            ClientDispatcher.BeginInvoke((Action)(() => Disconnect()), DispatcherPriority.Send);
        }

        private void StatusPane_CodeStyleChanged(object sender, EventArgs e)
        {
            ResetStats();
            ClientDispatcher.BeginInvoke((Action)(() => Disconnect()), DispatcherPriority.Send);
        }

        private void Disconnect()
        {
            ClientDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        }

        private void OnServerThreadStopped()
        {
            if (!IsStopping)
            {
                ServerThread = new Thread(StartMqttServer);
                ServerThread.Start();
            }
        }

        private async void StartMqttServer()
        {
            ClientDispatcher = Dispatcher.CurrentDispatcher;
            SetStatus("Configuring");
            MqttFactory factory = new MqttFactory();
            MqttServerOptionsBuilder optionsBuilder = new MqttServerOptionsBuilder()
                .WithDefaultCommunicationTimeout(new TimeSpan(0, 0, 0, 60, 0))
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(PlainPort)
                .WithMaxPendingMessagesPerClient(2000)
                ;

            Server = factory.CreateMqttServer();

            Server.ApplicationMessageReceived += Server_ApplicationMessageReceived;
            Server.ClientConnected += Server_ClientConnected;
            Server.ClientDisconnected += Server_ClientDisconnected;
            Server.ClientSubscribedTopic += Server_ClientSubscribedTopic;
            Server.ClientUnsubscribedTopic += Server_ClientUnsubscribedTopic;
            Server.Started += Server_Started;

#if HAVE_SYNC
            if (IsSync)
                Server.Start(optionsBuilder.Build());
            else
#endif
                await Server.StartAsync(optionsBuilder.Build());

            SetStatus("Started");
            // This will run the event queue forever, until we stop it
            Dispatcher.Run();

#if HAVE_SYNC
            if (IsSync)
                Server.Stop();
            else
#endif
                Server.StopAsync().Wait();

            await Dispatcher.BeginInvoke((Action)(() => OnServerThreadStopped()));
        }

        private void Server_Started(object sender, EventArgs e)
        {
            SetStatus("Server started");
        }

        private void Server_ClientUnsubscribedTopic(object sender, MqttClientUnsubscribedTopicEventArgs e)
        {
            SetStatus("Client unsubscribed " + e.TopicFilter);
        }

        private void Server_ClientSubscribedTopic(object sender, MqttClientSubscribedTopicEventArgs e)
        {
            SetStatus ("Client subscribed " + e.TopicFilter);
        }

        private void Server_ClientDisconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            SetStatus ("Client disconnected");
        }

        private void Server_ClientConnected(object sender, MqttClientConnectedEventArgs e)
        {
            SetStatus ("Client connected");
        }

        private void Server_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            string strVal = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            UpdateOutOfOrderCount(strVal);
            UpdateCounts(0, 1);
        }
    }
}
