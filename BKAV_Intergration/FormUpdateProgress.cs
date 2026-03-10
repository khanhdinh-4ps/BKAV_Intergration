using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BKAV_Intergration
{
    public partial class FormUpdateProgress : Form
    {
        public FormUpdateProgress()
        {
            InitializeComponent();
        }
        public void UpdateStatus(string message, int percent = -1)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message, percent)));
                return;
            }

            labelStatus.Text = message;
            if (percent >= 0 && percent <= 100)
            {
                progressBar1.Value = percent;
            }
        }
    }
}
