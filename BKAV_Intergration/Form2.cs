using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BKAV_Intergration
{
    public partial class Form2 : Form
    {
        private DataTable _allData;
        private HashSet<string> _selectedCodes = new HashSet<string>();
        public Form2()
        {
            InitializeComponent();
        }
        public List<string> SelectedCardCodes { get; private set; } = new List<string>();


        // In Form2_Load, hide tabPage1 and tabPage3
        private void Form2_Load(object sender, EventArgs e)
        {
            this.Size = new Size(850, 400);
            using (var conn = PrepareSAPData.ConnectToSQL())
            {
                try
                {
                    var cmd = new SqlCommand("SELECT CardCode, CardName FROM OCRD WHERE CardCode LIKE 'C%' ORDER BY CardCode", conn);
                    var adapter = new SqlDataAdapter(cmd);
                    _allData = new DataTable();
                    adapter.Fill(_allData);
                    BindData(_allData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (_allData == null) return;
            SaveCurrentSelections();
            string filter = txtSearch.Text.Trim().Replace("'", "''");
            string rowFilter = $"CardCode LIKE '%{filter}%' OR CardName LIKE '%{filter}%'";
            DataView dv = new DataView(_allData);
            dv.RowFilter = rowFilter;
            BindData(dv.ToTable());
        }
        private void BindData(DataTable dt)
        {
            dtGV_OCRD.Rows.Clear();
            foreach (DataRow row in dt.Rows)
            {
                int idx = dtGV_OCRD.Rows.Add();
                string cardCode = row["CardCode"].ToString();
                dtGV_OCRD.Rows[idx].Cells["CardCode"].Value = cardCode;
                dtGV_OCRD.Rows[idx].Cells["CardName"].Value = row["CardName"];
                dtGV_OCRD.Rows[idx].Cells["Select"].Value = _selectedCodes.Contains(cardCode);
            }
        }
        private void SaveCurrentSelections()
        {
            foreach (DataGridViewRow row in dtGV_OCRD.Rows)
            {
                if (row.Cells["Select"].Value is bool selected && selected)
                {
                    string cardCode = row.Cells["CardCode"].Value?.ToString();
                    if (!string.IsNullOrEmpty(cardCode))
                        _selectedCodes.Add(cardCode);
                }
                else
                {
                    string cardCode = row.Cells["CardCode"].Value?.ToString();
                    if (!string.IsNullOrEmpty(cardCode))
                        _selectedCodes.Remove(cardCode);
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dtGV_OCRD.Rows)
            {
                row.Cells["Select"].Value = true;
            }
        }

        private void btnDeselectAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dtGV_OCRD.Rows)
            {
                row.Cells["Select"].Value = false;
            }
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            SelectedCardCodes.Clear();

            foreach (DataGridViewRow row in dtGV_OCRD.Rows)
            {
                if (row.Cells["Select"].Value is bool selected && selected)
                {
                    string cardCode = row.Cells["CardCode"].Value?.ToString();
                    if (!string.IsNullOrEmpty(cardCode))
                    {
                        SelectedCardCodes.Add(cardCode);
                    }
                }
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
