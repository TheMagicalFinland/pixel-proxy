using Kernys.Bson;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace PixelProxy
{

    class TcpProxy
    {
        private String PIXEL_IP;
        private int PIXEL_PORT;

        private TcpListener Listener;
        private TcpClient Server;
        string partOfId = "None";
        long previousMsg = 0;
        public static bool allowStart = false;
        public string ownUserId = "None";
        public BSONObject impPacket = new BSONObject();
        int state = 0;
        public static List<worldData> worlds = new List<worldData>();
        string lastWorld;
        bool allowSummon = false;
        bool updateLoc = true;
        bool actionCommand = false;
        string commandReply = "";

        static worldData.Positions FindUserPosition(List<worldData> worlds, string username)
        {
            foreach (var world in worlds)
            {
                var userPos = world.positions.Find(position => position.username == username || position.uid == username);

                if (!string.IsNullOrEmpty(userPos.username))
                {
                    return userPos;
                }
            }

            return new worldData.Positions();
        }

        public static BSONObject CreateChatMessage(string nickname, string userID, string channel, int channelIndex, string message)
        {
            BSONObject bObj = new BSONObject();
            bObj["nick"] = nickname;
            bObj["userID"] = userID;
            bObj["channel"] = channel;
            bObj["channelIndex"] = channelIndex;
            bObj["message"] = message;
            bObj["time"] = DateTime.UtcNow;
            return bObj;
        }

        static async Task<string> TranslateText(string fromLanguage, string toLanguages, string inputText)
        {
            string apiKey = "ZPYQY1K-P70MXXA-QWAZJZ8-K8J91DR";
            using (HttpClient client = new HttpClient())
            {
                string url = "https://api.lecto.ai/v1/translate/text";

                client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                client.DefaultRequestHeaders.Add("Content-Type", "application/json");
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                string requestBody = $"{{ \"texts\": \"{inputText}\", \"to\": \"{toLanguages}\", \"from\": \"{fromLanguage}\" }}";

                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseContent = await response.Content.ReadAsStringAsync();

                return responseContent;
            }
            return null;
        }

        public async Task<BSONObject> handleCommand(string command, BSONObject packet)
        {
            if (packet == null) return null;

            BSONObject data = new BSONObject();
            data = packet;
            char[] delimiter = new char[] { ' ' };

            string[] args = command.Split(delimiter);

            Console.WriteLine(string.Join(" ", args));

            switch (args[0])
            {
                case "/test":
                    {
                        data["ID"] = "BGM";
                        data["CmB"] = CreateChatMessage("test", lastWorld, lastWorld, 1,
                   "Test");
                        break;
                    }
                case "/testtp":
                    {
                        data["ID"] = "mP";
                        double playerPosX = data["x"].doubleValue;
                        double playerPosY = data["y"].doubleValue;

                        Console.WriteLine("Player stands at:\nX: " + playerPosX.ToString() + "\nY: " + playerPosY.ToString());
                        break;
                    }

                case "say":
                    {
                        if (!allowStart) break;
                        if (previousMsg > DateTimeOffset.Now.ToUnixTimeMilliseconds()) break;
                        if (previousMsg < DateTimeOffset.Now.ToUnixTimeMilliseconds()) previousMsg = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 10000;
                        data["ID"] = "WCM";
                        data["msg"] = "Seqsss!!!";
                        break;
                    }

                case "/start":
                    {
                        if (allowStart) {
                            allowStart = false;
                            Console.WriteLine("Server -> Start disallowed.");
                            break; 
                         }

                        allowStart = true;
                        break;
                    }

                case "/banself":
                    {
                        if (!string.IsNullOrEmpty(ownUserId))
                        {
                            Console.WriteLine("Server -> Banned your own account permanently.");
                            data["ID"] = "BanPlayerFromGame";
                            data["U"] = ownUserId;
                            data["BanFromGameReasonValue"] = "Self-ban";
                            data["BanFromGameDurationValue"] = 3600;
                        }
                        break;
                    }

                case "/listmods":
                    {
                        Console.WriteLine("Server -> Wait...");
                        worldData wData = worlds.Find(x => x.worldName == lastWorld);
                        if (wData != null)
                        {
                            string msg = "Client found no staffs.";
                            if (wData.staffMembers.Count > 0)
                            {
                                msg = "Client found staffs: " + String.Join(", ", wData.staffMembers);
                            }
                            data["ID"] = "WCM";
                            data["msg"] = msg;
                            Console.WriteLine("Server -> Mods: " + msg);
                        }
                        break;
                    }

                case "/listplayers":
                    {
                        Console.WriteLine("Server -> Wait...");
                        worldData wData = worlds.Find(x => x.worldName == lastWorld);
                        if (wData != null)
                        {
                            string msg = "No players registered to your cache.";
                            if (wData.players.Count > 0 || wData.userIds.Count > 0)
                            {
                                msg = "Players registered in cache: name: " + string.Join(", ", wData.players) + ", uid: " + string.Join(", ", wData.userIds) + ", lvl: " + string.Join(",", wData.lvl.Select(x => x.ToString()).ToArray());
                            }
                            data["ID"] = "WCM";
                            data["msg"] = msg;
                            Console.WriteLine("Server -> " + msg);
                        }
                        break;
                    }

                case "/getserver":
                    {
                        Console.WriteLine("Server -> Wait...");
                        worldData wData = worlds.Find(x => x.worldName == lastWorld);
                        if (wData != null)
                        {
                            string msg = "World " + wData.worldName + ", subserver " + wData.serverIp + ". Cached U: " + wData.players.Count.ToString() + ", uid: " + wData.userIds.Count.ToString();
                            data["ID"] = "WCM";
                            data["msg"] = msg;
                            Console.WriteLine("Server -> " + msg);
                        }
                        break;
                    }

                case "/allowsummon":
                    {
                        if (allowSummon)
                        {
                            allowSummon = false;
                            Console.WriteLine("Server -> Summon disallowed.");
                            break;
                        }

                        allowSummon = true;
                        Console.WriteLine("Server -> Start allowed.");
                        break;
                    }

                case "/goto":
                    {
                        if (args.Length >= 2)
                        {
                            Console.WriteLine("Server -> Wait...");
                            worldData.Positions userPos = FindUserPosition(worlds, args[1]);
                                updateLoc = false;
                                data["ID"] = "mP";
                                data["t"] = 638283999725740420;
                                data["x"] = userPos.x;
                                data["y"] = userPos.y;
                                data["a"] = 1;
                                data["d"] = 3;
                                data["tp"] = true;
                            Console.WriteLine("Server -> Found user: " + userPos.x + ", " + userPos.y);
                        }
                        break;
                    }

                case "/getpos":
                    {
                        if (args.Length >= 2)
                        {
                            Console.WriteLine("Server -> Wait...");
                            worldData.Positions userPos = FindUserPosition(worlds, args[1]);
                            string msg = "UID " + userPos.uid + " stands at X: " + userPos.x + ", Y: " + userPos.y;
                            data["ID"] = "WCM";
                            data["msg"] = msg;
                            Console.WriteLine("Server (also broadcasted to chat) -> " + msg);
                        }
                        break;
                    }

                case "/translate":
                    {
                        if (args.Length < 4)
                        {
                            Console.WriteLine("Gotta have text and language.");
                            break;
                        }

                        string language = args[1];
                        string target = args[2];
                        string message = string.Join(" ", args.Skip(4));

                        //Task<string> msg = await TranslateText(language, target, message);
                        //Console.WriteLine(msg.ToString());
                    }
                    break;
            }
            return data;
        }

        public TcpProxy()
        {
            Console.WriteLine("Enter IP address (empty for default) > ");
            PIXEL_IP = Console.ReadLine();
            if (string.IsNullOrEmpty(PIXEL_IP))
            {
                PIXEL_IP = "44.194.163.69";
            }

            Console.WriteLine("Enter PORT > ");
            string portData = Console.ReadLine();
            if (string.IsNullOrEmpty(portData))
            {
                PIXEL_PORT = 10001;
            } else
            {
                PIXEL_PORT = int.Parse(portData);
            }

            /*if (getIp(PIXEL_IP) == null)
            {
                Console.WriteLine("Invalid ip.");
                Console.ReadLine();
                Environment.Exit(0);
                return;
            }*/
            Listener = new TcpListener(IPAddress.Parse("127.0.0.1"), PIXEL_PORT);
        }

        public async void Start()
        {
            Console.WriteLine("Starting...");
            Listener.Start();
            await AcceptConnections();
        }

        private async Task AcceptConnections()
        {
            Console.WriteLine("Listening for " + PIXEL_IP + ", PORT " + PIXEL_PORT);
            while(true)
            {
                HandleConnection(await Listener.AcceptTcpClientAsync());
            }
        }

        private void HandleConnection(TcpClient client)
        {
            Server = new TcpClient(PIXEL_IP, PIXEL_PORT);
            Console.WriteLine("Connection : " + PIXEL_IP);

            NetworkStream serverStream = Server.GetStream();
            NetworkStream clientStream = client.GetStream();

            new Task(() => OnClientPacket(clientStream, serverStream)).Start();
            new Task(() => OnServerPacket(serverStream, clientStream)).Start();
        }

        public static void handledNestedObj(BSONObject obj)
        {
            try
            {
                Console.WriteLine("---------- NESTED ----------");
                foreach (var nestedKey in obj.Keys)
                {
                    var nestedValue = obj[nestedKey];

                    if (nestedValue is BSONObject nestedNestedObj)
                    {
                        Console.WriteLine($"Nested key: {nestedKey}, Value (nested object):");
                        handledNestedObj(nestedNestedObj);
                    }
                    else
                    {
                        Console.WriteLine($"Nested key: {nestedKey}, Value: {nestedValue}");

                        if (nestedValue.ToString().ToLower() == "kernys.bson.bsonarray")
                        {
                            BSONArray array = (BSONArray)nestedValue;
                            Console.WriteLine("Array length: " + array.Count);

                            foreach (BSONValue data in array)
                            {
                                Console.WriteLine("HANDLED NESTED DATA ARRAY: " + data.stringValue);
                            }
                        }

                        if (nestedKey.ToString().ToLower() == "creationtime" || nestedKey.ToString().ToLower() == "lastactivatedtime")
                        {
                            Console.WriteLine("DATA: " + nestedValue.dateTimeValue);
                            continue;
                        }

                        if (nestedValue.ToString().ToLower() == "kernys.bson.bsonvalue")
                        {
                            Console.WriteLine("HANDLED BSONVALUE: " + nestedValue.stringValue);
                        }
                    }
                }
                Console.WriteLine("---------- END OF NESTED ----------");
            } catch(Exception ex)
            {
                Console.WriteLine(ex.ToString() + " occured in nested obj parser.");
            }
        }

        public static void getObjValues(BSONObject obj, string fromWho)
        {
            try
            {
                Console.WriteLine("PACKET id: " + obj["ID"]);
                if (obj["ID"].stringValue.ToLower() == "mp") return;
                Console.WriteLine("---------- GETOBJVALUES " + fromWho + " ----------");
                foreach (var key in obj.Keys)
                {
                    var value = obj[key];

                    if (value is BSONObject nestedObj)
                    {
                        Console.WriteLine($"Handled key: {key}, Value (nested object):");
                        handledNestedObj(nestedObj);
                    }
                    else
                    {
                        if (value.ToString() == "Kernys.Bson.BSONValue")
                        {
                            Console.WriteLine("typeof: " + obj[key].GetType());
                            Console.WriteLine("Data: " + key + " -> " + obj[key].stringValue);
                        }

                        if (key == "WN")
                        {

                        }
                        Console.WriteLine($"Handled key: {key}, Value: {value}");

                        if (value.ToString().ToLower() == "kernys.bson.bsonarray")
                        {
                            BSONArray array = (BSONArray)value;
                            Console.WriteLine("array length: " + array.Count);
                            foreach (BSONValue data in array)
                            {
                                Console.WriteLine("HANDLED DATA: " + data.stringValue);
                            }
                        }

                        if (value.ToString().ToLower() == "kernys.bson.bsonvalue")
                        {
                            Console.WriteLine("data value string: " + value.stringValue);
                        }
                    }
                }
                Console.WriteLine("Done");
                //Console.ReadLine();
                Console.WriteLine("---------- END OF GETOBJVALUES " + fromWho + " ----------");
            } catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private byte[] OnPacket(byte[] revBuffer, String from)
        {
            // Remove padding and load the bson.
            byte[] data = new byte[revBuffer.Length - 4];
            Buffer.BlockCopy(revBuffer, 4, data, 0, data.Length);

            BSONObject packets = null;
            try
            {
                packets = SimpleBSON.Load(data);
            }catch { }

            if (packets == null || !packets.ContainsKey("mc"))
                return revBuffer;

            // Modify the packet?
            //Console.WriteLine(from + " ========================================================================================");
            for (int i = 0; i < packets["mc"]; i++)
            {
                BSONObject packet = packets["m" + i] as BSONObject;

                string packetId = packet["ID"].stringValue;

                switch (from.ToLower())
                {
                    case "server":
                        {
                            switch(packetId)
                            {
                                case "GPd":
                                    {
                                        if (packet.ContainsKey("U"))
                                        {
                                            ownUserId = packet["U"].stringValue;
                                            impPacket = packet;
                                        }
                                        break;
                                    }

                                case "TTjW":
                                    {
                                        string name = packet["WN"].stringValue;
                                        string path = "F:/Projektit/pixel-proxy/Serverpackets.txt";
                                        string serverPackets = File.ReadAllText(path);
                                        File.WriteAllText(path, serverPackets + "\n\n------------- JOINING WORLD (" + name + ") -------------");

                                        int joinState = packet["JR"].int32Value;

                                        if (joinState == 0)
                                        {
                                            lastWorld = name;
                                            worldData newWorld = new worldData();
                                            newWorld.worldName = name;
                                            newWorld.serverIp = PIXEL_IP;
                                            worlds.Add(newWorld);
                                            break;
                                        }
                                        break;
                                    }

                                /*case "mP":
                                    {
                                        double posX = packet["x"].doubleValue;
                                        double posY = packet["y"].doubleValue;
                                        string user = packet["U"].stringValue;
                                        worldData.Positions playerPos = FindUserPosition(worlds, user);

                                        playerPos.x = (int)posX;
                                        playerPos.y = (int)posY;
                                        playerPos.uid = user;
                                        break;
                                    }*/

                                case "AnP":
                                    {
                                        Console.WriteLine("user join event");
                                        try
                                        {
                                            worldData wData = worlds.Find(x => x.worldName == lastWorld);
                                            if (wData != null)
                                            {
                                                worldData.Positions playerPos = FindUserPosition(worlds, packet["UN"]);

                                                if (string.IsNullOrEmpty(playerPos.username))
                                                {
                                                    playerPos.uid = packet["U"].stringValue;
                                                    playerPos.username = packet["UN"].stringValue;
                                                }

                                                wData.players.Add(packet["UN"].stringValue);
                                                wData.userIds.Add(packet["U"].stringValue);
                                                wData.lvl.Add(packet["LvL"].int32Value);

                                                //Console.WriteLine(packet["UN"].stringValue + " joined, lvl " + packet["LvL"].int32Value + ", id " + packet["U"].stringValue);
                                                //commandReply = packet["UN"].stringValue + " joined, lvl " + packet["LvL"].int32Value + ", id " + packet["U"].stringValue;
                                                //state = 3;

                                                if (packet.ContainsKey("playerAdminStatusKey"))
                                                {
                                                    Console.WriteLine("Admin level detected.");
                                                    int level = packet["playerAdminStatusKey"].int32Value;

                                                    if (level > 0 && level < 3)
                                                    {
                                                        wData.staffMembers.Add(packet["UN"]);
                                                        MessageBox.Show("Moderator joined your world. Name: " + playerPos.username + "\nUID: " + playerPos.uid);
                                                    }
                                                  
                                                    if (level == 3)
                                                    {
                                                        MessageBox.Show("World owner joined your world. Name: " + playerPos.username + "\nUID: " + playerPos.uid);
                                                    }
                                                }
                                            }
                                        } catch(Exception ex)
                                        {
                                            Console.WriteLine(ex.ToString());
                                        }
                                        break;
                                    }

                                case "WP":
                                    {
                                        if (!allowSummon)
                                        {
                                            state = 2;
                                        }
                                        break;
                                    }

                                case "KPl":
                                    {
                                        state = 1;
                                        break;
                                    }

                                case "p":
                                    {
                                        if (actionCommand && !string.IsNullOrEmpty(commandReply))
                                        {
                                            BSONObject nestedData = new BSONObject();
                                            nestedData["nick"] = "Proxy";
                                            nestedData["message"] = commandReply;
                                            nestedData["channel"] = lastWorld;
                                            nestedData["channelIndex"] = 1;
                                            nestedData["time"] = "0";
                                            packet["ID"] = "BGM";
                                            packet["CmB"] = nestedData;
                                            actionCommand = false;
                                            commandReply = "";
                                        }
                                        break;
                                    }

                                case "BGM":
                                    {
                                        try
                                        {
                                            var msgData = packet["CmB"];
                                            string globalMsgs = File.ReadAllText("F:/Projektit/pixel-proxy/globalmessages.txt");
                                            File.WriteAllText("F:/Projektit/pixel-proxy/globalmessages.txt", globalMsgs+"\n\nUser: " + msgData["nick"] + "\nBroadcast: " + msgData["message"] + "\nChannel: " + msgData["channel"] + "\nUID: " + msgData["userID"] + "\nIndex: " + msgData["channelIndex"]); ;
                                        } catch(Exception ex) {
                                            Console.WriteLine("FAIL: " + ex.ToString());
                                            Console.ReadLine();
                                        }
                                        break;
                                    }

                                case "WCM":
                                    {
                                        //getObjValues(packet);
                                        //Console.WriteLine("trigger");
                                        try
                                        {
                                            var wcData = packet["CmB"];
                                            worldData wData = worlds.Find(x => x.worldName == lastWorld);
                                            if (wData != null)
                                            {
                                                string userName = wcData["nick"];
                                                string userId = wcData["userID"];
                                                string messageC = wcData["message"];
                                                string input = messageC;
                                                List<string> replies = new List<string>() { "sup", "hi", "hey", "hello", "yellow", "yo", "wassup" };

                                                string pattern = @"^[0-9+\-*/()\s]+$";

                                                /*if (Regex.IsMatch(input, pattern))
                                                {
                                                    try
                                                    {
                                                        double result = Convert.ToDouble(new System.Data.DataTable().Compute(input, null));
                                                        state = 3;
                                                        commandReply = result.ToString();
                                                    }
                                                    catch (Exception) {
                                                        state = 3;
                                                        commandReply = "brains lagged";
                                                    };
                                                } else
                                                {
                                                    if (messageC.ToLower().Contains("bro"))
                                                    {
                                                        state = 3;
                                                        commandReply = "bro " + userName;
                                                    }
                                                    if (messageC.ToLower().StartsWith("are you "))
                                                    {
                                                        state = 3;
                                                        commandReply = messageC;
                                                    }
                                                    if (messageC.ToLower().Contains("neek") || messageC.ToLower().Contains("nigg") || messageC.ToLower().Contains("nekr"))
                                                    {
                                                        state = 3;
                                                        commandReply = "oleppa hiliaa " + userName;
                                                    }
                                                    if (messageC.ToLower().StartsWith("i am ") || messageC.ToLower().StartsWith("i'm ") || messageC.ToLower().StartsWith("im "))
                                                    {
                                                        state = 3;
                                                        commandReply = "Sup, " + messageC.ToLower().Replace("i am", "").Replace("i'm ", "").Replace("im ", "") + ". I'm dad.";
                                                    }
                                                    if (messageC.ToLower() == "no")
                                                    {
                                                        state = 3;
                                                        commandReply = "why not";
                                                    }
                                                    if (messageC.ToLower().Contains("because") || messageC.ToLower().Contains("cuz") || messageC.ToLower().Contains("becuz"))
                                                    {
                                                        state = 3;
                                                        commandReply = "ok fine";
                                                    }
                                                    if (messageC.ToLower().Contains("like"))
                                                    {
                                                        state = 3;
                                                        commandReply = "yes";
                                                    }
                                                    if (messageC.ToLower().Contains("yes"))
                                                    {
                                                        state = 3;
                                                        commandReply = "no.";
                                                    }
                                                    if (messageC.ToLower().Contains("bruh"))
                                                    {
                                                        state = 3;
                                                        commandReply = "dafuq";
                                                    }
                                                    if (messageC.ToLower().StartsWith("add"))
                                                    {
                                                        state = 3;
                                                        commandReply = "sry no";
                                                    }
                                                    if (messageC.ToLower().Contains("scam"))
                                                    {
                                                        state = 3;
                                                        commandReply = userName + " scammer report guys";
                                                    }
                                                    if (messageC.ToLower().Contains("copy"))
                                                    {
                                                        state = 3;
                                                        commandReply = "no copy.";
                                                    }
                                                    if (replies.Any(reply => messageC.ToLower().StartsWith(reply.ToLower())))
                                                    {
                                                        state = 3;
                                                        commandReply = replies[new Random().Next(0, replies.Count)];
                                                    }
                                                }*/
                                            }
                                        } catch(Exception ex)
                                        {
                                            Console.WriteLine("FAILED: " + ex.ToString());
                                        }
                                        break;
                                    }
                            }
                            // Request sent to client by server.
                            break;
                        }
                    case "client":
                        {
                            switch(packetId)
                            {
                                case "mP":
                                    {
                                        if (!updateLoc) break;
                                        //handleCommand("say", packet);
                                        break;
                                    }
                                case "WCM":
                                    {
                                        handleCommand(packet["msg"].stringValue, packet);
                                        break;
                                    }
                            }
                            break;
                        }
                }

                if (from.ToLower() == "client" && state == 1)
                {
                    /*BSONObject newPacket = new BSONObject();
                    newPacket["ID"] = "WCM";
                    newPacket["msg"] = "Client kicked by world staff. Added user to monitoring database.";
                    packets["m" + i] = newPacket;
                    Console.WriteLine("Server -> Kicked by world staff. Sent troll msg.");*/
                    state = 0;
                }

                if (from.ToLower() == "client" && state == 2)
                {
                    //BSONObject newPacket = new BSONObject();
                    //newPacket["ID"] = "WCM";
                    //newPacket["msg"] = "I believe I can fly.";
                    //packets["m" + i] = newPacket;
                    state = 0;
                }

                if (from.ToLower() == "client" && state == 3)
                {
                    BSONObject newPacket = new BSONObject();
                    newPacket["ID"] = "WCM";
                    newPacket["msg"] = commandReply;
                    packets["m" + i] = newPacket;
                    Console.WriteLine(commandReply + " (broadcasted to others)");
                    commandReply = "";
                    state = 0;
                }

                if (packet["ID"].stringValue.ToLower() == "trade")
                {
                    Console.WriteLine("Packet ID: " + packet["ID"]);
                    if (from.ToString().ToLower() == "server")
                    {
                        Console.WriteLine("Server:");
                    } else
                    {
                        Console.WriteLine("CLIENT:");
                    }
                    getObjValues(packet, from);
                }
                if (from.ToString().ToLower() == "server" && packet["ID"].stringValue.ToLower() != "mp" && packet["ID"].stringValue.ToLower() != "st" && packet["ID"].stringValue.ToLower() != "p" && packet["ID"].stringValue.ToLower() != "hbb" && packet["ID"].stringValue.ToLower() != "hb" && packet["ID"].stringValue.ToLower() != "bgm" && packet["ID"].stringValue.ToLower() != "psicu" && packet["ID"].stringValue.ToLower() != "mp" && packet["ID"].stringValue.ToLower() != "ha")
                {
                    getObjValues(packet, from);
                }

                if (from.ToString().ToLower() == "client" && packet["ID"].stringValue.ToLower() != "mp" && packet["ID"].stringValue.ToLower() != "st" && packet["ID"].stringValue.ToLower() != "p")
                {
                    getObjValues(packet, from);
                }

                ReadBSON(packet, from);

                bool ignorePacket = false;

                if (from.ToString().ToLower() == "client")
                {
                    string id = packet["ID"].stringValue;
                    switch(id)
                    {
                        case "Trade":
                            {
                                packet["MTy"] = new BSONArray() { 731, 0, 0, 0 };
                                packet["Amt"] = new BSONArray() { 5000, 0, 0, 0 };
                                packets["m" + i] = packet;
                                Console.WriteLine("UPDATED");
                                break;
                            }
                    }
                }

                /*if (from.ToString().ToLower() == "server")
                {
                    string id = packet["ID"].stringValue;
                    switch(id)
                    {
                        case "VChk":
                            {
                                Console.WriteLine("Detected version check, current server: " + packet["VN"].int32Value);
                                packet["VN"] = 101;
                                packets["m" + i] = packet;
                                break;
                            }
                        case "GPd":
                            {
                                if (packet["BPl"].int32Value == 1) // player banned
                                {
                                    packet["BPl"] = 0; // removing ban
                                    packets["m" + i] = packet;
                                }
                                break;
                            }
                        case "TTjW":
                            {
                                if (packet["JR"].int32Value == 5) // player banned
                                {
                                    packet["JR"] = 0; // removing ban, joinresult = success
                                    packet["BPl"] = 0; // removing bpl from context
                                    packets["m" + i] = packet;
                                }
                                break;
                            }
                        case "KErr":
                            {
                                packets["m" + i] = new BSONObject();
                                break;
                            }
                        case "mP":
                            {
                                Console.WriteLine("detected movement, returned revBuffer");
                                //packets.Remove("ID");
                                break;
                            }
                        case "MWli":
                            {
                                packet["Ct"] = -4;
                                packets["m" + i] = packet;
                                getObjValues(packet, from);
                                break;
                            }
                        default:
                            {
                                Console.WriteLine("ID: " + id);
                                break;
                            }
                    }
                }*/

                if (packet["ID"].stringValue == "OoIP")
                {
                    string ip = getIp(packet["IP"].stringValue);
                    if (ip != null)
                    {
                        packet["IP"] = "localhost";
                        if (ip != "localhost" && ip != "127.0.0.1")
                        {
                            PIXEL_IP = ip;
                        } 
                        Console.WriteLine("Performed server move to " + PIXEL_IP); 
                    } else
                    {
                        packet["ID"] = "TTjW";
                        packet["JR"] = 2;
                    }
                }
            }

            // Dump the BSON and add padding.
            MemoryStream memoryStream = new MemoryStream();
            using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
            {
                byte[] bsonDump = SimpleBSON.Dump(packets);

                binaryWriter.Write(bsonDump.Length + 4);
                binaryWriter.Write(bsonDump);
            }
            return memoryStream.ToArray();
        }

        public string getIp(string hostName)
        {
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(hostName);

                foreach (IPAddress ipAddress in hostEntry.AddressList)
                {
                    if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ipAddress.ToString();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null;
            }
        }


        public void ReadBSON(BSONObject SinglePacket, string sender, string Parent = "")
        {

                foreach (string Key in SinglePacket.Keys)
            {
                try
                {
                    BSONValue Packet = SinglePacket[Key];

                    /*if (sender.ToLower() == "server")
                    {
                        getObjValues(SinglePacket, sender);
                    }*/

                    dynamic type;
                    //Console.WriteLine("packet from " + sender);

                    switch (Packet.valueType)
                    {
                        case BSONValue.ValueType.String:
                            //Console.WriteLine($"{Parent} = {Key} | {Packet.valueType} = {Packet.stringValue}");
                            type = Packet.stringValue;
                            File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + $"{sender} > {Parent} = {Key} | {Packet.valueType} = {Packet.stringValue}");
                            break;
                        case BSONValue.ValueType.Boolean:
                            //Console.WriteLine($"{Parent} = {Key} | {Packet.valueType} = {Packet.boolValue}");
                            type = Packet.boolValue;
                            File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + $"{sender} > {Parent} = {Key} | {Packet.valueType} = {Packet.boolValue}");
                            break;
                        case BSONValue.ValueType.Int32:
                            //Console.WriteLine($"{Parent} = {Key} | {Packet.valueType} = {Packet.int32Value}");
                            type = Packet.int32Value;
                            File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + $"{sender} > {Parent} = {Key} | {Packet.valueType} = {Packet.int32Value}");
                            break;
                        case BSONValue.ValueType.Int64:
                            //Console.WriteLine($"{Parent} = {Key} | {Packet.valueType} = {Packet.int64Value}");
                            type = Packet.int64Value;
                            File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + $"{sender} > {Parent} = {Key} | {Packet.valueType} = {Packet.int64Value}");
                            break;
                        case BSONValue.ValueType.Binary: // BSONObject
                            //Console.WriteLine($"{Parent} = {Key} | {Packet.valueType}");
                            type = Packet.valueType;
                            File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + $"{sender} > {Parent} = {Key} | {Packet.valueType}");
                            ReadBSON(SimpleBSON.Load(Packet.binaryValue), sender, Key);
                            break;
                        case BSONValue.ValueType.Double:
                            //Console.WriteLine($"{Parent} = {Key} | {Packet.valueType} = {Packet.doubleValue}");
                            type = Packet.doubleValue;
                            File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + $"{sender} > {Parent} = {Key} | {Packet.valueType} = {Packet.doubleValue}");
                            break;
                        default:
                            type = Packet.valueType;
                            if (Key == "CmB")
                            {
                                string data = JsonSerializer.Serialize(Packet.valueType);
                                File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + sender + " > Serialized: " + data);
                            }
                            Console.WriteLine($"{Parent} = {Key} = {Packet.valueType}");
                            File.WriteAllText("F:/Projektit/pixel-proxy/packetlog.txt", File.ReadAllText("F:/Projektit/pixel-proxy/packetlog.txt") + "\n" + $"{sender} > {Parent} = {Key} = {Packet.valueType}");
                            break;
                    }

                    if (sender.ToLower() == "server")
                    {
                        //Console.WriteLine("P data: " + Packet.ToString());
                        if (Key == "ID")
                        {
                            string path = "F:/Projektit/pixel-proxy/Serverpackets.txt";
                            string serverPackets = File.ReadAllText(path);
                            partOfId = type.ToString();

                            if (partOfId != "AnP" && partOfId != "BGM" && partOfId != "mP")
                            {
                                if (partOfId == "TTjW")
                                {

                                }
                                else
                                {
                                    File.WriteAllText(path, serverPackets + "\n\n-- New ID --\nID name: " + partOfId);
                                }
                            }
                        }

                        if (Key != "ID")
                        {
                            if (Key == "invData")
                            {
                                //Console.WriteLine("Invdata Value:{0}", Key.Value.ToString());
                            }
                            if (partOfId != "AnP" && partOfId != "BGM" && partOfId != "mP")
                            {
                                string serverPackets = File.ReadAllText("F:/Projektit/pixel-proxy/Serverpackets.txt");
                                File.WriteAllText("F:/Projektit/pixel-proxy/Serverpackets.txt", serverPackets + "\n\n(Part of " + partOfId + ")\nParent: " + Parent + "\nKey: " + Key + "\nValue: " + Packet.valueType);
                            }
                        }
                    } 

                    string existingValues = File.ReadAllText("F:/Projektit/pixel-proxy/varnames.txt");

                    if (!existingValues.Contains(Parent))
                    {
                        Console.WriteLine("logged");
                        File.WriteAllText("F:/Projektit/pixel-proxy/varnames.txt", File.ReadAllText("F:/Projektit/pixel-proxy/varnames.txt") + "\n" + sender + " > Name: " + Parent + ", Value: " + Key + ", Type: " +  Packet.valueType);
                    }

                }
                catch (Exception ex) {
                   Console.WriteLine(ex.ToString() + " occured");
                }
            }
        }

        private void OnClientPacket(NetworkStream clientStream, NetworkStream serverStream)
        {
            byte[] buffer = new byte[4096];
            int revBytes;

            while (true)
            {
                try
                {
                    revBytes = clientStream.Read(buffer, 0, buffer.Length);
                    if (revBytes <= 0)
                        continue;

                    byte[] newBuffer = OnPacket(buffer, "Client");
                    if (newBuffer == buffer)
                        serverStream.Write(buffer, 0, revBytes);
                    else
                        serverStream.Write(newBuffer, 0, newBuffer.Length);
                }catch
                {
                    Server.Close();
                    break;
                }
            }
        }

        private void OnServerPacket(NetworkStream serverStream, NetworkStream clientStream)
        {
            byte[] buffer = new byte[4096];
            int revBytes;

            while (true)
            {
                try
                {
                    revBytes = serverStream.Read(buffer, 0, buffer.Length);
                    if (revBytes <= 0)
                        continue;

                    byte[] newBuffer = OnPacket(buffer, "Server");
                    if (newBuffer == buffer)
                        clientStream.Write(buffer, 0, revBytes);
                    else
                        clientStream.Write(newBuffer, 0, newBuffer.Length);
                }catch
                {
                    break;
                }
            }
        }
    }
}
