using fileSearch;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AplicationSearchFiles {
    public partial class Form1 : Form {
        private BiggerFiles bf;
        private CancellationTokenSource cts;

        public Form1() {
            InitializeComponent();
            
        }

        private void button1_Click(object sender, EventArgs e) {
            if(cts != null)
                cts.Cancel();
        }

        private async void button2_Click(object sender, EventArgs e) {
            Process();
           
        }

        private async void Process() {
            if(String.IsNullOrEmpty(textBox1.Text))
                MessageBox.Show("Insira o path na caixa de texto ao lado da label 'dir path'");

            listView1.Items.Clear();
                     
            cts = new CancellationTokenSource();
            bf = new BiggerFiles(textBox1.Text, int.Parse(textBox2.Text), cts.Token);

            button2.Enabled = false;
            button1.Enabled = true;

            Task searching = bf.Start();

            while (!searching.IsCanceled && !searching.IsCompleted && !searching.IsFaulted) {
                FileInfo[] aux1 = bf.getBiggerFiles();
                foreach(var a in aux1) {
                    if (a != null) {
                        ListViewItem item1 = new ListViewItem(a.FullName);
                        item1.SubItems.Add("" + a.Length);
                        listView1.Items.Add(item1);
                    }
                }

                await Task.Delay(50).ConfigureAwait(true);
                listView1.Items.Clear();
            }

            listView1.Items.Clear();
                   
            FileInfo[] aux = bf.getBiggerFiles();
            foreach (var a in aux) {
                ListViewItem item1 = new ListViewItem(a.FullName);
                item1.SubItems.Add("" + a.Length);
                listView1.Items.Add(item1);
            }
            textBox3.Text = "" + bf.GetNumberOfFiles();

            button2.Enabled = true;
            button1.Enabled = false;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void label2_Click(object sender, EventArgs e) {

        }

        private void textBox1_TextChanged(object sender, EventArgs e) {

        }
    }
}
