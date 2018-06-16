using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttTest.Common
{
    public class Nvqt : INotifyPropertyChanged
    {
#pragma warning disable CS0067 // Fody uses this property, but the compiler doesn't know it
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        public string Name { get; set; }
        public object Value { get; set; }
        public double Timestamp { get; set; }
        public int Quality { get; set; }

        public const double DAYS_TO_UNIX_TIME = 25569.0;
        public const double SECS_PER_DAY = (60 * 60 * 24);

        public Nvqt()
        {
            Quality = 0xc0;
            Timestamp = WindowsTimeToUnixTime(DateTime.Now.ToUniversalTime().ToOADate());
        }

        public static double WindowsTimeToUnixTime(double wdate)
        {
            if (wdate < DAYS_TO_UNIX_TIME)
                return (-1);
            wdate -= DAYS_TO_UNIX_TIME;
            wdate *= SECS_PER_DAY;
            return (wdate);
        }
    }
}
