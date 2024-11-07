using System.Net;

namespace Swordfish.Library.Networking;

public class Host
{
    private string _mHostname;
    private IPAddress _mAddress;
    private int _mPort;
    private IPEndPoint _mEndPoint;

    public string Hostname
    {
        get => _mHostname;
        set
        {
            _mHostname = value;
            _mAddress = NetUtils.GetHostAddress(value);
            UpdateEndPoint();
        }
    }

    public IPAddress Address
    {
        get => _mAddress;
        set
        {
            _mAddress = value;
            _mHostname = string.Empty;
            UpdateEndPoint();
        }
    }

    public int Port
    {
        get => _mPort;
        set
        {
            _mPort = value;
            UpdateEndPoint();
        }
    }

    public IPEndPoint EndPoint
    {
        get => _mEndPoint;
        set
        {
            _mEndPoint = value;
            _mAddress = value.Address;
            _mHostname = string.Empty;
            _mPort = value.Port;
        }
    }

    private void UpdateEndPoint()
    {
        if (_mEndPoint == null)
        {
            _mEndPoint = new IPEndPoint(_mAddress, _mPort);
        }
        else
        {
            _mEndPoint.Address = _mAddress;
            _mEndPoint.Port = _mPort;
        }
    }

    public override string ToString()
    {
        return $"{(string.IsNullOrEmpty(Hostname) ? Address.ToString() : Hostname)}:{Port}";
    }
}