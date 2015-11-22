using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DA32ProtocolCsharp
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SKMessage skm = new SKMessage();
            SKMessage.mestype mt;


            byte[] by = System.Text.Encoding.UTF8.GetBytes(textBox1.Text);
            if (skm.decodemes(by, out mt))
            {
                MessageBox.Show(mt.ToString() + "\nSuccess");
                if (mt == SKMessage.mestype.TEXT)
                {
                    string c = "";
                    c += skm.get_last_text().id.ToString();
                    c += "\n";
                    c += skm.get_last_text().name;
                    c += "\n";
                    c += skm.get_last_text().time.ToString();
                    c += "\n";
                    c += skm.get_last_text().text;
                    c += "\n";
                    MessageBox.Show(c);
                }
            }
            else
                MessageBox.Show("failed.");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SKMessage skm = new SKMessage();
            byte[] by;
            skm.set_send_textmes("Hello", textBox2.Text, -1);
            skm.encodemes(SKMessage.mestype.TEXT,out by);
            textBox1.Text = Encoding.UTF8.GetString(by);
        }
    }
}
