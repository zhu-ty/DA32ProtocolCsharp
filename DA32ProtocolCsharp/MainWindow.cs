using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;

namespace DA32ProtocolCsharp
{
    public partial class MainWindow : Form
    {
        SKServer s = new SKServer();
        SKClient c = new SKClient();

        public MainWindow()
        {
            InitializeComponent();
        }
        private void button3_Click(object sender, EventArgs e)
        {
            IPAddress ip;
            try
            {
                ip = IPAddress.Parse(textBox4.Text);
            }
            catch (Exception ee)
            {
                return;
            }
            if (c.SendText(1, textBox5.Text, textBox2.Text, ip, DateTime.Now))
            {
                textBox5.Text = "";
                textBox3.AppendText("\n");
                textBox3.AppendText(textBox5.Text + " " + DateTime.Now.ToString() + "\n");
                textBox3.AppendText(textBox2.Text);
            }
        }

        private void OnServerCall(object sender, SKServerEventArgs e)
        {
            if (e.type == SKMessage.mestype.TEXT)
            {
                SKServerTextEventArgs et = (SKServerTextEventArgs)e;
                if (textBox1.InvokeRequired)
                {
                    Action<SKMessage.textmes> textbox1act = (x) => 
                    {
                        textBox1.AppendText("\n");
                        textBox1.AppendText(x.name + " " + x.time.ToString() + "\n");
                        textBox1.AppendText(x.text);
                    };
                    textBox1.Invoke(textbox1act, et.text_pack);
                }
                c.SendResponse(1, e.ip, DateTime.Now);
            }
            else if (e.type == SKMessage.mestype.EXIT)
            {
                c.SendExit(1, e.ip, DateTime.Now);
            }
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            s.ServerCall += OnServerCall;
            s.start_listening();
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            c.SendExitToAll();
            System.Threading.Thread.Sleep(1000);
            s.stop_listening();
        }
    }
}
