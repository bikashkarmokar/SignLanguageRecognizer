using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace AirWriter
{
    public partial class NetworkControl : Form
    {
        // form border style None kore disi tai control box out hoe gache

        //Constants
        const int AW_SLIDE = 0X40000;
        const int AW_HOR_POSITIVE = 0X1;
        const int AW_HOR_NEGATIVE = 0X2;
        const int AW_BLEND = 0X80000;

        [DllImport("user32")]
        static extern bool AnimateWindow(IntPtr hwnd, int time, int flags);
    
        public NetworkControl()
        {
            InitializeComponent();
        }
        //Load the Form At Position of AirWriter
        int WidthOfMain = Application.OpenForms["AirWriter"].Width;
        int HeightofMain = Application.OpenForms["AirWriter"].Height;
        int LocationMainX = Application.OpenForms["AirWriter"].Location.X;
        int locationMainy = Application.OpenForms["AirWriter"].Location.Y;


        protected override void OnLoad(EventArgs e)
        {
            
            //Set the Location
            this.Location = new Point(LocationMainX + WidthOfMain, locationMainy + 10);

            //Animate form
            AnimateWindow(this.Handle, 500, AW_SLIDE | AW_HOR_POSITIVE);
            
        }

        private void button1_Click(object sender, EventArgs e)
        {            
            this.Close();
        }

        private void NetworkControl_FormClosing(object sender, FormClosingEventArgs e)
        {
            int wid = this.Width;
            while (wid > 0)
            {
                this.Width = wid;
                wid--;
                Application.DoEvents();
            }
        }
    }
}
