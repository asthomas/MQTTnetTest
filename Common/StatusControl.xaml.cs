using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

namespace MqttTest.Common
{
    /// <summary>
    /// Interaction logic for StatusControl.xaml
    /// </summary>
    public partial class StatusControl : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<MqttQualityOfServiceLevel> QosLevels { get; set; }
        public ObservableCollection<string> CodeStyles { get; set; }

        public event EventHandler ResetClicked;
        public event EventHandler QosChanged;
        public event EventHandler CodeStyleChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public StatusControl()
        {
            InitializeComponent();
            QosLevels = new ObservableCollection<MqttQualityOfServiceLevel>()
            {
                MqttQualityOfServiceLevel.AtMostOnce,
                MqttQualityOfServiceLevel.AtLeastOnce,
                MqttQualityOfServiceLevel.ExactlyOnce
            };
            CodeStyles = new ObservableCollection<string>() { "Synchronous", "Asynchronous" };
        }

        private void BReset_Click(object sender, RoutedEventArgs e)
        {
            ResetClicked?.Invoke(this, e);
        }

        private void CBCodeStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CodeStyleChanged?.Invoke(sender, e);
        }

        private void CBQos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            QosChanged?.Invoke(sender, e);
        }
    }
}
