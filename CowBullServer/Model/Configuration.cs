using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CowBullServer.Model
{
    public class Configuration : INotifyPropertyChanged
    {
        #region Fields

        private string _ip;
        private string _port;
        private string _number;
        
        #endregion

        #region Ctor
        //public Configuration(string ip, string port, string number)
        //{
        //    _ip = ip;
        //    _port = port;
        //    _number = number;
        //}
        #endregion

        #region Propeties

        public string IP
        {
            get { return _ip; }
            set
            {
                _ip = value;
                RaisePropertyChanged("IP");
            }
        }

        public string Port
        {
            get { return _port; }
            set
            {
                _port = value;
                RaisePropertyChanged("Number");
            }
        }

        public string Number
        {
            get { return _number; }
            set
            {
                _number = value;
                RaisePropertyChanged("Number");
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
