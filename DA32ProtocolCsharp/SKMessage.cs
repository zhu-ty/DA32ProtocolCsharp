using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace DA32ProtocolCsharp
{
    /// <summary>
    /// 运行在json层与信息层之间的类，将剔除头尾和长度信息的字节层信息翻译成信息层。
    /// </summary>
    class SKMessage
    {
        /// <summary>
        /// 消息类型
        /// </summary>
        public enum mestype { TEXT, RESPONSE, EXIT, UNDEFINED };
        /// <summary>
        /// 一个text包的data内容
        /// </summary>
        public struct textmes
        {
            public int id;
            public string name;
            public DateTime time;
            public string text;
        }

        /// <summary>
        /// 构造函数，注意到内部的两个text_mes都只有默认值
        /// </summary>
        public SKMessage()
        {
            last_textmes.id = -1;
            last_textmes.name = "DA32";
            last_textmes.time = DateTime.Now;
            last_textmes.text = "Haven't set yet.";
            send_textmes.id = -1;
            send_textmes.name = "DA32";
            send_textmes.time = DateTime.Now;
            send_textmes.text = "Haven't set yet.";
        }

        ///<summary>
        ///当bool=true且type为TEXT时，更新last_textmes
        /// </summary>
        public bool decodemes(byte[] jsonb, out mestype type)
        {
            bool ret = false;
            type = mestype.UNDEFINED;
            try
            {
                string jsonmes = Encoding.UTF8.GetString(jsonb);
                JObject jsonobject = JObject.Parse(jsonmes);
                int id = (int)jsonobject["id"];
                string type_s = (string)jsonobject["type"];
                string time_s = (string)jsonobject["time"];
                string md5 = (string)jsonobject["md5"];
                md5 = md5.ToLower();
                type = get_type(type_s);
                switch (type)
                {
                    case mestype.RESPONSE:
                    case mestype.EXIT:
                        {
                            List<byte[]> con_byte = new List<byte[]>();
                            con_byte.Add(BitConverter.GetBytes(id));
                            con_byte.Add(Encoding.UTF8.GetBytes(type_s));
                            con_byte.Add(Encoding.UTF8.GetBytes(time_s));
                            if (md5_verification(byte_connect(con_byte),md5))
                                ret = true;
                            else
                            {
                                type = mestype.UNDEFINED;
                                ret = false;
                            }
                            break;
                        }
                    case mestype.TEXT:
                        {
                            List<byte[]> con_byte = new List<byte[]>();
                            string data_name = (string)jsonobject["data"]["name"];
                            string data_text_utf8 = (string)jsonobject["data"]["text"];
                            con_byte.Add(BitConverter.GetBytes(id));
                            con_byte.Add(Encoding.UTF8.GetBytes(type_s));
                            con_byte.Add(Encoding.UTF8.GetBytes(time_s));
                            con_byte.Add(Encoding.UTF8.GetBytes(data_name));
                            con_byte.Add(Encoding.UTF8.GetBytes(data_text_utf8));
                            if (md5_verification(byte_connect(con_byte), md5))
                            {
                                last_textmes.id = id;
                                last_textmes.time = DateTime.ParseExact(time_s, "yyyy.MM.dd HH:mm:ss", null);
                                last_textmes.name = data_name;
                                last_textmes.text = data_text_utf8;
                                ret = true;
                            }
                            else
                            {
                                type = mestype.UNDEFINED;
                                ret = false;
                            }
                            break;
                        }
                    default:
                        {
                            type = mestype.UNDEFINED;
                            ret = false;
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                ret = false;
            }
            return ret;
        }
        ///<summary>
        ///当bool=true且type为TEXT时，从send_textmes中拿取信息生成jsonb，否则直接生成其他的type
        ///<para>生成其他的type的时候，会利用到send_textmes中的id与Time信息。</para>
        /// </summary>
        public bool encodemes(mestype type, out byte[] jsonb)
        {
            bool ret = false;
            jsonb = null;
            try
            {
                string s = string.Empty;
                s += "{";
                s += "\"id\":" + send_textmes.id.ToString() + ",";
                switch (type)
                {
                    case mestype.EXIT:
                        {
                            s += "\"type\":\"exit\",";
                            s += "\"time\":\"" + send_textmes.time.ToString("yyyy.MM.dd HH:mm:ss") + "\",";
                            s += "\"else\":{},";
                            s += "\"data\":{},";
                            List<byte[]> con_byte = new List<byte[]>();
                            con_byte.Add(BitConverter.GetBytes(send_textmes.id));
                            con_byte.Add(Encoding.UTF8.GetBytes("exit"));
                            con_byte.Add(Encoding.UTF8.GetBytes(send_textmes.time.ToString("yyyy.MM.dd HH:mm:ss")));
                            s += "\"md5\":\"" + getmd5(byte_connect(con_byte)) + "\"";
                            s += "}";
                            jsonb = Encoding.UTF8.GetBytes(s);
                            ret = true;
                            break;
                        }
                    case mestype.RESPONSE:
                        {
                            s += "\"type\":\"response\",";
                            s += "\"time\":\"" + send_textmes.time.ToString("yyyy.MM.dd HH:mm:ss") + "\",";
                            s += "\"else\":{},";
                            s += "\"data\":{},";
                            List<byte[]> con_byte = new List<byte[]>();
                            con_byte.Add(BitConverter.GetBytes(send_textmes.id));
                            con_byte.Add(Encoding.UTF8.GetBytes("response"));
                            con_byte.Add(Encoding.UTF8.GetBytes(send_textmes.time.ToString("yyyy.MM.dd HH:mm:ss")));
                            s += "\"md5\":\"" + getmd5(byte_connect(con_byte)) + "\"";
                            s += "}";
                            jsonb = Encoding.UTF8.GetBytes(s);
                            ret = true;
                            break;
                        }
                    case mestype.TEXT:
                        {
                            s += "\"type\":\"text\",";
                            s += "\"time\":\"" + send_textmes.time.ToString("yyyy.MM.dd HH:mm:ss") + "\",";
                            s += "\"else\":{},";
                            s += "\"data\":{";
                            s += "\"name\":\"" + add_special_char(send_textmes.name) + "\",";
                            s += "\"text\":\"" + add_special_char(send_textmes.text) + "\"";
                            s += "},";
                            List<byte[]> con_byte = new List<byte[]>();
                            con_byte.Add(BitConverter.GetBytes(send_textmes.id));
                            con_byte.Add(Encoding.UTF8.GetBytes("text"));
                            con_byte.Add(Encoding.UTF8.GetBytes(send_textmes.time.ToString("yyyy.MM.dd HH:mm:ss")));
                            con_byte.Add(Encoding.UTF8.GetBytes(send_textmes.name));
                            con_byte.Add(Encoding.UTF8.GetBytes(send_textmes.text));
                            s += "\"md5\":\"" + getmd5(byte_connect(con_byte)) + "\"";
                            s += "}";
                            jsonb = Encoding.UTF8.GetBytes(s);
                            ret = true;
                            break;
                        }
                    default:
                        {
                            ret = false;
                            jsonb = null;
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                ret = false;
                jsonb = null;
            }
            return ret;
        }
        ///<summary>
        ///获得上一个译码后的消息
        /// </summary>
        public textmes get_last_text()
        {
            return last_textmes;
        }

        /// <summary>
        /// 设置下一个将要编码的消息，自动打时间戳
        /// <para>调用此函数将不会更改上一个待发送消息的Name和text，但仍旧会更新id与time</para>
        /// </summary>
        /// <param name="id">ID号</param>
        /// <returns>是否设置成功</returns>
        public bool set_send_mes(int id = 1)
        {
            send_textmes.id = id;
            send_textmes.time = DateTime.Now;
            return true;
        }

        /// <summary>
        /// 设置下一个将要编码的消息，手动打时间戳
        /// <para>调用此函数将不会更改上一个待发送消息的Name和text，但仍旧会更新id与time</para>
        /// </summary>
        /// <param name="dt">时间戳</param>
        /// <param name="id">ID号</param>
        /// <returns>是否设置成功</returns>
        public bool set_send_mes(DateTime dt, int id = 1)
        {
            send_textmes.id = id;
            send_textmes.time = dt;
            return true;
        }
        /// <summary>
        /// 设置下一个将要编码的消息，自动打时间戳
        /// </summary>
        /// <param name="name">发送方的名字</param>
        /// <param name="text">待编码的消息</param>
        /// <param name="id">ID号</param>
        /// <returns>是否设置成功</returns>
        public bool set_send_textmes(string name, string text, int id = 1)
        {
            send_textmes.id = id;
            send_textmes.time = DateTime.Now;
            send_textmes.text = text;
            send_textmes.name = name;
            return true;
        }
        /// <summary>
        /// 设置下一个将要编码的消息，手动打时间戳
        /// </summary>
        /// <param name="name">发送方的名字</param>
        /// <param name="text">待编码的消息</param>
        /// <param name="dt">时间戳</param>
        /// <param name="id">ID号</param>
        /// <returns>是否设置成功</returns>
        public bool set_send_textmes(string name, string text,DateTime dt, int id = 1)
        {
            send_textmes.id = id;
            send_textmes.time = dt;
            send_textmes.text = text;
            send_textmes.name = name;
            return true;
        }

        private mestype get_type(string s)
        {
            if (s.ToLower() == "text")
                return mestype.TEXT;
            else if (s.ToLower() == "response")
                return mestype.RESPONSE;
            else if (s.ToLower() == "exit")
                return mestype.EXIT;
            else
                return mestype.UNDEFINED;
        }
        private bool md5_verification(byte[] bt, string md5_v)
        {
            MD5 mymd5 = new MD5CryptoServiceProvider();
            byte[] md5o = mymd5.ComputeHash(bt);
            string mymd5_s = BitConverter.ToString(md5o).Replace("-", "");
            if (md5_v.ToLower() == mymd5_s.ToLower())
                return true;
            else
                return false;
        }
        private string getmd5(byte[] bt)
        {
            MD5 mymd5 = new MD5CryptoServiceProvider();
            byte[] md5o = mymd5.ComputeHash(bt);
            return (BitConverter.ToString(md5o).Replace("-", "").ToLower());
        }
        private byte[] byte_connect(List<byte[]> btlist)
        {
            int length = 0;
            int now = 0;
            for (int i = 0; i < btlist.Count; i++)
                length += btlist[i].Length;
            byte[] ret = new byte[length];
            for (int i = 0; i < btlist.Count; i++)
            {
                Array.Copy(btlist[i], 0, ret, now, btlist[i].Length);
                now += btlist[i].Length;
            }
            return ret;
        }
        private string add_special_char(string origin)
        {
            string ret = origin;
            ret = ret.Replace("\\", "\\\\"); 
            ret = ret.Replace("\n", "\\n");
            ret = ret.Replace("\r", "\\r");
            ret = ret.Replace("\"", "\\\"");
            ret = ret.Replace("\'", "\\\'");
            return ret;
        }
        private textmes last_textmes;
        private textmes send_textmes;
    }
}
