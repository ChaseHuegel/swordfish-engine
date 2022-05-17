using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Swordfish.Library
{
    public class PropertyChangeNotifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) { }

        protected void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                OnPropertyChanged(propertyName);
            }
        }

        protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string caller = "")
        {
            property = value;
            NotifyPropertyChanged(caller);
        }
    }
}