using System.ComponentModel;

public class PlayedNumber : INotifyPropertyChanged
{
    private string _number;
    private int _bulls;
    private int _cows;

    public PlayedNumber() { }

    #region Properties


    public string Number
    {
        get { return _number; }
        set
        {
            _number = value;
            RaisePropertyChanged("Number");
        }
    }

    public int Bulls
    {
        get { return _bulls; }
        set
        {
            _bulls = value;
            RaisePropertyChanged("Bulls");
        }
    }

    public int Cows
    {
        get { return _cows; }
        set
        {
            _cows = value;
            RaisePropertyChanged("Cows");
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