using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace PixelProxy
{
    public class worldData
    {
        public struct Positions
        {
            public int x;
            public int y;
            public string username;
            public string uid;
        }

            static string getIp(string input)
        {
            IPAddress ip;

            if (IPAddress.TryParse(input, out ip))
            {
                // If the input is already a valid IP address, return it as-is.
                return input;
            }
            else
            {
                // Try pinging the domain and getting its IP address.
                try
                {
                    Ping ping = new Ping();
                    PingReply reply = ping.Send(input);

                    if (reply.Status == IPStatus.Success)
                    {
                        return reply.Address.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ping error: " + ex.Message);
                }

                return null;
            }
        }

        public string worldName = string.Empty;
        public string serverIp = string.Empty;
        public List<string> players = new List<string>();
        public List<string> userIds = new List<string>();
        public List<int> lvl = new List<int>();
        public List<string> staffMembers = new List<string>();
        public List<Positions> positions = new List<Positions>();
    }
}
