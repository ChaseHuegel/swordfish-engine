using System.Net;
using System;

namespace Swordfish.Library.Networking
{
    public class Host
    {
        private string m_Hostname;
        private IPAddress m_Address;
        private int m_Port;
        private IPEndPoint m_EndPoint;

        public string Hostname {
            get => m_Hostname;
            set {
                m_Hostname = value;
                m_Address = NetUtils.GetHostAddress(value);
                UpdateEndPoint();
            }
        }

        public IPAddress Address {
            get => m_Address;
            set {
                m_Address = value;
                m_Hostname = string.Empty;
                UpdateEndPoint();
            }
        }

        public int Port {
            get => m_Port;
            set {
                m_Port = value;
                UpdateEndPoint();
            }
        }

        public IPEndPoint EndPoint {
            get => m_EndPoint;
            set {
                m_EndPoint = value;
                m_Address = value.Address;
                m_Hostname = string.Empty;
                m_Port = value.Port;
            }
        }

        private void UpdateEndPoint()
        {
            if (m_EndPoint == null)
                m_EndPoint = new IPEndPoint(m_Address, m_Port);
            else 
            {
                m_EndPoint.Address = m_Address;
                m_EndPoint.Port = m_Port;
            }
        }

        public override string ToString()
        {
            return $"{(string.IsNullOrEmpty(Hostname) ? Address.ToString() : Hostname)}:{Port}";
        }
    }
}
