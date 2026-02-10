using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace X_Plane2Influx
{
    public static class GetIp
    {
        public static IPAddress[] GetLocalIPAddress()
        {
            string hostName = Dns.GetHostName();
            // Console.WriteLine("Local Machine's Host Name: " + hostName);
            IPHostEntry ipEntry = Dns.GetHostEntry(hostName);

            return ipEntry.AddressList;


        }
    }
}
