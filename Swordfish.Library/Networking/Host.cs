using System.Net;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Networking;

// ReSharper disable once UnusedType.Global
public class Host
{
    private readonly object _lock = new();
    private string _hostname;
    private IPAddress _address;
    private int _port;
    private IPEndPoint _ipEndPoint;

    public string Hostname
    {
        get
        {
            lock (_lock)
            {
                return _hostname;
            }
        }
        set
        {
            lock (_lock)
            {
                _hostname = value;
                _address = Net.GetHostAddress(value);
                UpdateEndPoint();
            }
        }
    }

    public IPAddress Address
    {
        get
        {
            lock (_lock)
            {
                return _address;
            }
        }
        set
        {
            lock (_lock)
            {
                _address = value;
                _hostname = string.Empty;
                UpdateEndPoint();
            }
        }
    }

    public int Port
    {
        get
        {
            lock (_lock)
            {
                return _port;
            }
        }
        set
        {
            lock (_lock)
            {
                _port = value;
                UpdateEndPoint();
            }
        }
    }

    public IPEndPoint IPEndPoint
    {
        get
        {
            lock (_lock)
            {
                return _ipEndPoint;
            }
        }
        set
        {
            lock (_lock)
            {
                _ipEndPoint = value;
                _address = value.Address;
                _hostname = string.Empty;
                _port = value.Port;
            }
        }
    }

    private void UpdateEndPoint()
    {
        if (_ipEndPoint == null)
        {
            _ipEndPoint = new IPEndPoint(_address, _port);
        }
        else
        {
            _ipEndPoint.Address = _address;
            _ipEndPoint.Port = _port;
        }
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return $"{(string.IsNullOrEmpty(Hostname) ? Address.ToString() : Hostname)}:{Port}";
        }
    }
}