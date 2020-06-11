using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace sni
{
    class SNIDetect
    {
        public static bool? Detect(IPAddress ip, string host, int timeout_connect, int timeout_auth)
        {
            using (var client = _connect(ip, timeout_connect, timeout_auth))
            {
                if (client == null)
                {
                    return null;
                }
                return _auth(client, host);
            }
        }

        public static bool? Detect(IPAddress ip, string host, int timeout_connect, int timeout_auth, int retry)
        {
            for (int i = 0; i < retry + 1; i++)
            {
                var ret = Detect(ip, host, timeout_connect, timeout_auth);
                if (ret.HasValue)
                {
                    return ret.Value;
                }
            }
            return null;
        }

        private static bool? _auth(TcpClient client, string host)
        {
            using (var ssl = new SslStream(client.GetStream()))
            {
                try
                {
                    ssl.AuthenticateAsClient(host, null, System.Security.Authentication.SslProtocols.Tls12, false);
                    return true;
                }
                catch (System.Security.Authentication.AuthenticationException)
                {
                    return false;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private static TcpClient _connect(IPAddress ip, int timeout_connect, int timeout_auth)
        {
            TcpClient client = new TcpClient();
            try
            {
                if (!client.ConnectAsync(ip, 443).Wait(timeout_connect))
                {
                    client.Close();
                    return null;
                }
                client.SendTimeout = timeout_auth;
                client.ReceiveTimeout = timeout_auth;
                return client;
            }
            catch (Exception)
            {
                client.Close();
                return null;
            }
        }
    }
}
