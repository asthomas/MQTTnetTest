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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Closing += MainWindow_Closing;
            CodeStyle = IsSync ? "Synchronous" : "Asynchronous";

            ServerThread = new Thread(StartMqttServer);
            ServerThread.Start();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
        }

        private async void StartMqttServer()
        {
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

            if (IsSync)
                Server.Start(optionsBuilder.Build());
            else
                await Server.StartAsync(optionsBuilder.Build());

            SetStatus("Started");
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
