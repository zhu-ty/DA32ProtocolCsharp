using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;

namespace DA32ProtocolCsharp
{
    /// <summary>
    /// 运行在字节层与JSON层之间的Server
    /// </summary>
    class SKServer
    {
        /// <summary>
        /// 连接监听端口
        /// </summary>
        public const int ListenPort = 3232;
        /// <summary>
        /// 最大同时连接数量
        /// </summary>
        public const int max_connection = 100;
        /// <summary>
        /// 最大单次收取字节数
        /// </summary>
        public const int max_byte_once = 100000;
        /// <summary>
        /// 包前缀长度
        /// </summary>
        public const int head_byte_size = 10;
        /// <summary>
        /// 包后缀长度
        /// </summary>
        public const int end_byte_size = 2;
        /// <summary>
        /// 包固定前缀的内容
        /// </summary>
        public static readonly byte[] head_2_bytes = { 0x32, 0xA0 };
        /// <summary>
        /// 包固定后缀内容
        /// </summary>
        public static readonly byte[] end_2_bytes = { 0x42,0xF0};
        /// <summary>
        /// 回调事件
        /// </summary>
        public event OnServerCall ServerCall;
        public delegate void OnServerCall(object sender, SKServerEventArgs e);
        /// <summary>
        /// 开始在3232上监听，请确保回调事件已经注册
        /// </summary>
        /// <returns></returns>
        public bool start_listening()
        {
            bool ret = false;
            server_lock.WaitOne();
            {
                if (started || ServerCall == null)
                    ret = false;
                else
                {
                    try
                    {
                        server_listen_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());
                        foreach (IPAddress ip in ips)
                        {
                            if (ip.AddressFamily.Equals(AddressFamily.InterNetwork))
                            {
                                server_listen_socket.Bind(new IPEndPoint(ip, ListenPort));
                                break;
                            }
                        }
                        server_listen_socket.Listen(max_connection);
                        started = true;
                        Thread listenthread = new Thread(main_listener);
                        listenthread.IsBackground = true;
                        listenthread.Start();
                        ret = true;
                    }
                    catch (Exception e)
                    {
                        started = false;
                        ret = false;
                    }
                }
            }
            server_lock.ReleaseMutex();
            return ret;
        }

        /// <summary>
        /// 结束接听和所有的连接
        /// </summary>
        /// <returns></returns>
        public bool stop_listening()
        {
            server_lock.WaitOne();
            started = false;
            server_communication_sockets.Clear();
            server_lock.ReleaseMutex();
            return true;
        }

        private void main_listener()
        {
            while (true)
            {
                server_lock.WaitOne();
                if (!started)
                    break;
                server_lock.ReleaseMutex();
                Socket c;
                c = server_listen_socket.Accept();
                server_lock.WaitOne();
                server_communication_sockets.Add(c);
                Thread new_communication_thread = new Thread(communication);
                new_communication_thread.IsBackground = true;
                new_communication_thread.Start(c);
                server_lock.ReleaseMutex();
            }
            server_lock.ReleaseMutex();
        }

        private void communication(object RecvServer)
        {
            Socket c = (Socket)RecvServer;
            c.ReceiveTimeout = 10 * 60 * 1000;//10min
            c.ReceiveBufferSize = max_byte_once;
            IPAddress this_ip = ((IPEndPoint)(c.RemoteEndPoint)).Address;
            while (true)
            {
                server_lock.WaitOne();
                if (!started)
                {
                    server_communication_sockets.Remove(c);
                    if(c != null)
                        c.Close();
                    server_lock.ReleaseMutex();
                    break;
                }
                server_lock.ReleaseMutex();
                //TODO(_SHADOWK) 并未添加上级主动停止某个ServerSocket工作的代码。
                try
                {
                    byte[] head = new byte[head_byte_size];
                    int len = c.Receive(head);
                    if(len == 0)
                        throw(new Exception());
                    if (len == head_byte_size && head[0] == head_2_bytes[0] && head[1] == head_2_bytes[1])
                    {
                        ulong len_then = (ulong)BitConverter.ToInt64(head, 2) - head_byte_size - end_byte_size;
                        byte[] then = new byte[len_then];
                        byte[] end = new byte[end_byte_size];
                        ulong len_recv = (ulong)c.Receive(then);
                        int end_recv = c.Receive(end);
                        if (len_recv != len_then || end_recv != end_byte_size || end[0] != end_2_bytes[0] || end[1] != end_2_bytes[1])
                            continue;
                        SKMessage.mestype type;
                        server_lock.WaitOne();
                        if (skmessage.decodemes(then, out type))
                        {
                            switch (type)
                            {
                                case SKMessage.mestype.EXIT:
                                    {
                                        SKServerEventArgs exit_event = new SKServerEventArgs();
                                        exit_event.type = SKMessage.mestype.EXIT;
                                        exit_event.ip = ((IPEndPoint)(c.RemoteEndPoint)).Address;
                                        ServerCall(this, exit_event);
                                        server_communication_sockets.Remove(c);
                                        c.Close();
                                        break;
                                    }
                                case SKMessage.mestype.RESPONSE:
                                    {
                                        SKServerEventArgs response_event = new SKServerEventArgs();
                                        response_event.type = SKMessage.mestype.RESPONSE;
                                        response_event.ip = ((IPEndPoint)(c.RemoteEndPoint)).Address;
                                        ServerCall(this, response_event);
                                        break;
                                    }
                                case SKMessage.mestype.TEXT:
                                    {
                                        SKServerTextEventArgs text_event = new SKServerTextEventArgs();
                                        text_event.type = SKMessage.mestype.TEXT;
                                        text_event.ip = ((IPEndPoint)(c.RemoteEndPoint)).Address;
                                        text_event.text_pack = skmessage.get_last_text();
                                        ServerCall(this, text_event);
                                        break;
                                    }
                                default:
                                    {
                                        break;
                                    }
                            }
                        }
                        server_lock.ReleaseMutex();
                    }
                }
                catch (Exception e)//超时或Socket已关闭
                {
                    server_lock.WaitOne();
                    SKServerEventArgs exit_event = new SKServerEventArgs();
                    exit_event.type = SKMessage.mestype.EXIT;
                    exit_event.ip = this_ip;
                    ServerCall(this, exit_event);
                    server_lock.ReleaseMutex();
                    if(c != null)
                        c.Close();
                    break;
                }
            }
        }

        private SKMessage skmessage = new SKMessage();
        private bool started = false;//线程共享
        private Mutex server_lock = new Mutex();//线程共享
        private Socket server_listen_socket;
        private List<Socket> server_communication_sockets = new List<Socket>();
    }

    class SKServerEventArgs : System.EventArgs
    {
        public SKMessage.mestype type;
        public IPAddress ip;
    }
    class SKServerTextEventArgs : SKServerEventArgs
    {
        public SKMessage.textmes text_pack;
    }
}
