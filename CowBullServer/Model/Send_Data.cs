using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime.Serialization;

namespace CowBullServer.Model
{
    public class SendData : INotifyPropertyChanged
    {
        #region Fields

        private string _number;
        private string _message;

        #endregion

        #region Properties

        public NumeroJugado NumberPlayed { get; set; }

        public string Number
        {
            get { return _number; }
            set
            {
                if (_number != value)
                    _number = value;
                RaisePropertyChanged("Number");
            }
        }

        public string Menssage
        {
            get { return _message; }
            set
            {
                if (_message != value)
                    _message = value;
                RaisePropertyChanged("Message");
            }
        }

        #endregion

        public SendData()
        {
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }


    public static class Settings
    {
        private static readonly string SettingFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "setting.config");

        public static void FillWithDefault(SettingsConfiguration config)
        {
            config.NameGamer = "Yasmany";
            config.NumbertoFind = "1479";
            config.Move = 10;
        }

        public static SettingsConfiguration Load()
        {
            FileStream file = null;
            try
            {
                if (File.Exists(SettingFilePath))
                {
                    file = File.Open(SettingFilePath, FileMode.Open);
                    var serializer = new DataContractSerializer(typeof (SettingsConfiguration));
                    var value = (SettingsConfiguration) serializer.ReadObject(file);

                    value.SetFixedValuesDefaults();
                    return value;
                }
                else
                {
                    File.Create(SettingFilePath);
                    Load();
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (file != null)
                    file.Dispose();
            }

            var config = new SettingsConfiguration();
            FillWithDefault(config);
            config.Save();
            return config;
        }

        public static void Save(this SettingsConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            FileStream fileStream = null;
            XmlTextWriter xmlTextWriter = null;
            try
            {
                fileStream = File.Open(SettingFilePath, FileMode.Create);
                xmlTextWriter = new XmlTextWriter(fileStream, Encoding.UTF8)
                {
                    Formatting = Formatting.Indented
                };
                var serializer = new DataContractSerializer(typeof (SettingsConfiguration));
                serializer.WriteObject(xmlTextWriter, config);
            }
            catch (Exception)
            {

            }
            finally
            {
                if (xmlTextWriter != null)
                    xmlTextWriter.Close();

                if (fileStream != null)
                    fileStream.Dispose();
            }

            //--notify config change
            OnConfigChanged(config);
        }


        public static event EventHandler<SettingsConfigurationChangedEventArgs> ConfigChanged;

        private static void OnConfigChanged(SettingsConfiguration config)
        {
            EventHandler<SettingsConfigurationChangedEventArgs> handler = ConfigChanged;
            if (handler != null)
                handler(null, new SettingsConfigurationChangedEventArgs(config));
        }

        private static SettingsConfiguration _instance;

        public static SettingsConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    ConfigChanged += (s, e) =>
                    {
                        _instance = e.Config;
                    };

                    //--load config
                    _instance = Load();
                }
                return _instance;
            }
            //set
            //{
            //    if (_instance == null)
            //    {
            //        ConfigChanged += (s, e) =>
            //        {
            //            _instance = e.Config;
            //        };

            //        //--load config
            //        _instance = Load();
            //    }
            //}
        }
    }



    [DataContract]
    public class SettingsConfiguration : INotifyPropertyChanged
    {
        #region Fields

        private string path;

        private string _nameGamer;
        private string _numbertoFind;
        private int _move;

        #endregion

        #region Properties

        [DataMember]
        public string NameGamer
        {
            get
            {
                return _nameGamer;
            }
            set
            {
                if (_nameGamer != value)
                    _nameGamer = value;
                RaisePropertyChanged("NameGamer");
            }
        }

        [DataMember]
        public string NumbertoFind
        {
            get
            {
                return _numbertoFind;
            }
            set
            {
                if (_numbertoFind != value)
                    _numbertoFind = value;
                RaisePropertyChanged("NumbertoFind");
            }
        }

        [IgnoreDataMember]
        public int Move
        {
            get
            {
                return _move;
            }
            set
            {
                if (_move != value)
                    _move = value;
                RaisePropertyChanged("Move");
            }
        }

        #endregion

        public SettingsConfiguration()
        {}

        public void SetFixedValuesDefaults()
        {
            //DBProvider.CreatedDb = true;
        }
        

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
    
    public sealed class SettingsConfigurationChangedEventArgs : EventArgs
    {
        private readonly SettingsConfiguration _config;

        public SettingsConfigurationChangedEventArgs(SettingsConfiguration config)
        {
            _config = config;
        }

        public SettingsConfiguration Config
        {
            get { return _config; }
        }
    }
}
