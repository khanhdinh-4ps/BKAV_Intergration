using BKAV_Intergration;
using Newtonsoft.Json;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static BKAV_Intergration.Models;
using static BKAV_Intergration.PrepareSAPData;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using CommandType = BKAV_Intergration.Models.CommandType;
using Message = System.Windows.Forms.Message;
using Timer = System.Windows.Forms.Timer;

namespace BKAV_Intergration
{
    public partial class Form1 : Form
    {
        private string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private string versionUrl = "https://github.com/khanhdinh-4ps/BKAV_Intergration/releases/latest/download/version.txt";
        private string zipUrl = "https://github.com/khanhdinh-4ps/BKAV_Intergration/releases/latest/download/Debug.zip";
        
        private void CheckForUpdate()
        {
            try
            {
                string serverVersion;

                try
                {
                    serverVersion = DownloadStringFromUrl(versionUrl);
                    if (string.IsNullOrWhiteSpace(serverVersion))
                    {
                        throw new Exception("Không tải được thông tin version.");
                    }
                    serverVersion = serverVersion.Trim();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Không thể lấy thông tin phiên bản từ server:\n" + ex.Message,
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }

                if (serverVersion != currentVersion)
                {
                    DialogResult dr = MessageBox.Show(
                        $"Phiên bản hiện tại: {currentVersion}\n" +
                        $"Phiên bản mới: {serverVersion}\n\n" +
                        $"Bạn có muốn cập nhật không?",
                        "Cập nhật phần mềm",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (dr == DialogResult.Yes)
                    {
                        using (var progressForm = new FormUpdateProgress())
                        {
                            progressForm.Show();
                            progressForm.UpdateStatus("Đang tải bản cập nhật...");
                            Application.DoEvents();

                            string tempZip = Path.Combine(Path.GetTempPath(), "Update_" + Guid.NewGuid().ToString("N") + ".zip");

                            try
                            {
                                DownloadFileFromUrl(zipUrl, tempZip, progressForm);
                            }
                            catch (Exception ex)
                            {
                                progressForm.Close();
                                MessageBox.Show(
                                    "Tải file cập nhật thất bại:\n" + ex.Message,
                                    "Lỗi",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error
                                );
                                return;
                            }

                            progressForm.UpdateStatus("Đang cài đặt...");
                            Application.DoEvents();

                            // Gọi Updater
                            string updaterPath = Path.Combine(Application.StartupPath, "Updater.exe");
                            var psi = new ProcessStartInfo
                            {
                                FileName = updaterPath,
                                Arguments = $"\"{tempZip}\" \"{Application.ExecutablePath}\"",
                                UseShellExecute = true
                            };
                            Process.Start(psi);

                            progressForm.UpdateStatus("Đang khởi động lại ứng dụng...");
                            Thread.Sleep(1500);

                            progressForm.Close();
                            Environment.Exit(0);
                        }
                    }
                }
                else
                {
                    // Tùy chọn: Thông báo nếu đã là phiên bản mới nhất
                    // MessageBox.Show("Bạn đang sử dụng phiên bản mới nhất.", "Cập nhật", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi kiểm tra cập nhật:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void DownloadFileFromUrl(string url, string destinationPath, FormUpdateProgress progressForm = null)
        {
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                using (var stream = response.Content.ReadAsStreamAsync().Result)
                using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[81920];
                    long downloadedBytes = 0;
                    int bytesRead;

                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        if (totalBytes.HasValue)
                        {
                            int percent = (int)(downloadedBytes * 100 / totalBytes.Value);
                            progressForm?.UpdateStatus($"Đang tải bản cập nhật... {percent}%", percent);
                        }
                    }
                }
            }
        }

        private string DownloadStringFromUrl(string url)
        {
            string logFile = Path.Combine(Path.GetTempPath(), "UpdaterDownload.log");
            LogDownload(logFile, $"Downloading text from: {url}");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                try
                {
                    var response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();

                    string content = response.Content.ReadAsStringAsync().Result.Trim();
                    LogDownload(logFile, $"Downloaded text content: {content}");

                    return content;
                }
                catch (HttpRequestException ex)
                {
                    LogDownload(logFile, $"HTTP Error: {ex.Message}");
                    throw new Exception($"Không thể tải thông tin version:\n{ex.Message}");
                }
                catch (Exception ex)
                {
                    LogDownload(logFile, $"Error: {ex.Message}");
                    throw;
                }
            }
        }
        private void LogDownload(string logFile, string message)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(logFile, line, Encoding.UTF8);
            }
            catch
            { }
        }
        private CancellationTokenSource _cts;
        private bool _isRunning = false;

        private int _syncLoopCount = 0;
        private System.Windows.Forms.Timer _syncTimer;
        private bool _isSyncing = false;
        private BkavService _bkavService;
        private PrepareSAPData _sap = new PrepareSAPData();
        private bool _tabPagesVisible = false;
        private bool _tabPagesVisible2 = false;
        public Form1()
        {
            CheckForUpdate();
            InitializeComponent();
            _bkavService = new BkavService();
            _sap.CleanUpOldTempFiles();
            if (tabControl1.TabPages.Contains(tabPage6))
                tabControl1.TabPages.Remove(tabPage6);
            if (tabControl1.TabPages.Contains(tabPage3))
                tabControl1.TabPages.Remove(tabPage3);
            if (tabControl1.TabPages.Contains(tabPage2))
                tabControl1.TabPages.Remove(tabPage2);
            _tabPagesVisible = false;
            _syncTimer = new System.Windows.Forms.Timer();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_tabPagesVisible && keyData == (Keys.Control | Keys.Shift | Keys.V))
            {
                tabControl1.TabPages.Add(tabPage3);

                _tabPagesVisible = true;
                return true;
            }
            if (!_tabPagesVisible && keyData == (Keys.Control | Keys.Shift | Keys.Z))
            {
                tabControl1.TabPages.Add(tabPage6);
                tabControl1.TabPages.Add(tabPage2);

                _tabPagesVisible2 = true;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void btnEcrypt_Click(object sender, EventArgs e)
        {
            string key = "34bcf4830ab7dfa70e9fd4c5daacd7ed2983099b31d65c8c4089d3d2b2b26b40";
            string input = txtInput.Text;
            try
            {
                string encrypted = _sap.EncryptAES(input, key);
                txtOutput.Text = encrypted;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Encryption failed: " + ex.Message);
            }
        }

        private void btnDecrypt_Click(object sender, EventArgs e)
        {
            string key = "34bcf4830ab7dfa70e9fd4c5daacd7ed2983099b31d65c8c4089d3d2b2b26b40";
            string cipherText = txtOutput.Text;
            try
            {
                string decrypted = _sap.DecryptAES(cipherText, key);
                txtInput.Text = decrypted;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Decryption failed: " + ex.Message);
            }
        }


        private async void btnCreate110_Click(object sender, EventArgs e)
        {
            try
            {
                string inputForm = txtForm.Text.Trim();
                string inputSerial = cbSerial.Text.Trim();

                if (string.IsNullOrEmpty(inputForm) || string.IsNullOrEmpty(inputSerial))
                {
                    MessageBox.Show("Vui lòng nhập Mẫu số và Ký hiệu!");
                    return;
                }

                txtResult110.Text = "Đang gửi lệnh 110...";

                var listInvoiceData = _sap.PrepareInvoiceData(CommandType.CreateInvoiceAdjustDiscount, inputForm, inputSerial);

                var result = await _bkavService.ExecCommandAsync(CommandType.CreateInvoiceAdjustDiscount, listInvoiceData);

                if (result.Status != 0)
                {
                    txtResult110.Text = "Lỗi hệ thống: " + result.MessLog;
                    return;
                }

                var listResult = JsonConvert.DeserializeObject<List<InvoiceResult>>(result.Object.ToString());

                string log = "";
                foreach (var item in listResult)
                {
                    if (item.Status == 0)
                    {
                        log += "--- TẠO HÓA ĐƠN THÀNH CÔNG (CMD 110) ---\r\n";
                        log += $"PartnerInvoiceID: {item.PartnerInvoiceID}\r\n";
                        log += $"Mẫu số: {item.InvoiceForm}\r\n";
                        log += $"Ký hiệu: {item.InvoiceSerial}\r\n";
                        log += $"Số HĐ: {item.InvoiceNo} (Mới tạo nên thường là 0)\r\n";
                        txtPartnerInvoiceID.Text = $"{item.PartnerInvoiceID}\r\n";
                    }
                    else
                    {
                        log += $"Lỗi dòng {item.PartnerInvoiceID}: {item.MessLog}\r\n";
                    }
                }
                txtResult110.Text = log;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            using (var f2 = new Form2())
            {
                if (f2.ShowDialog() == DialogResult.OK)
                {
                    txtCustomer.Text = string.Join(", ", f2.SelectedCardCodes);
                }
            }
        }

        private void btnLoadDelivery_Click(object sender, EventArgs e)
        {
            if (!GV_Header_Delivery.Columns.Contains("Select"))
            {
                DataGridViewCheckBoxColumn selectColumn = new DataGridViewCheckBoxColumn();
                selectColumn.Name = "Select";
                selectColumn.HeaderText = "Chọn";
                selectColumn.Width = 65;
                GV_Header_Delivery.Columns.Insert(0, selectColumn);
            }

            string fromDate = dtFromDate.Value.ToString("yyyy-MM-dd");
            string toDate = dtToPick.Value.ToString("yyyy-MM-dd");

            string statusFilter = "";
            if (checkBox_Open.Checked && !checkBox_Closed.Checked)
                statusFilter = "AND T0.DocStatus = 'O'";
            else if (!checkBox_Open.Checked && checkBox_Closed.Checked)
                statusFilter = "AND T0.DocStatus = 'C'";

            var cardCodes = txtCustomer.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToList();

            if (cardCodes.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn khách hàng trước.");
                return;
            }

            string cardCodeFilter = string.Join(",", cardCodes.Select(c => $"'{c}'"));

            DataTable dtSAP = new DataTable();
            dtSAP.Columns.Add("STT");
            dtSAP.Columns.Add("DocEntry");
            dtSAP.Columns.Add("CardCode");
            dtSAP.Columns.Add("CardName");
            dtSAP.Columns.Add("NumAtCard");
            dtSAP.Columns.Add("DocStatus");
            dtSAP.Columns.Add("DocType");
            dtSAP.Columns.Add("DocDate");
            dtSAP.Columns.Add("DocDueDate");
            dtSAP.Columns.Add("LicTradNum");
            dtSAP.Columns.Add("DocTotal");
            dtSAP.Columns.Add("Comments");
            dtSAP.Columns.Add("ReturnDocEntry");

            using (var conn = ConnectToSQL())
            {
                if (conn == null)
                {
                    MessageBox.Show("Kết nối SQL Server thất bại!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string queryInvoice = $@"
                SELECT TOP(50) 
                    ROW_NUMBER() OVER (ORDER BY T0.DocEntry) AS STT,
                    T0.DocEntry, 
                    T0.CardCode, 
                    T0.CardName, 
                    T0.NumAtCard,
                    T0.DocStatus, 
                    T0.DocType, 
                    CONVERT(varchar(10), T0.DocDate, 103) AS DocDate, 
                    CONVERT(varchar(10), T0.DocDueDate, 103) AS DocDueDate, 
                    T0.LicTradNum,
                    T0.DocTotal,
                    T0.Comments,
                    ISNULL(R0.DocEntry, 0) AS ReturnDocEntry -- Đây là DocEntry của Return Delivery (nếu có)
                FROM ODLN T0 WITH(NOLOCK)
                OUTER APPLY (
                    SELECT TOP 1 R0.DocEntry
                    FROM ORDN R0 WITH(NOLOCK)              -- Bảng Return Delivery
                    INNER JOIN RDN1 R1 ON R1.DocEntry = R0.DocEntry
                    WHERE R1.BaseEntry = T0.DocEntry       -- BaseEntry trỏ về DocEntry của Delivery
                      AND R1.BaseType = 15                 -- 15 là Object Code của Delivery
                      AND R0.CANCELED = 'N'
                    ORDER BY R0.DocEntry
                ) R0
                WHERE T0.CardCode IN ({cardCodeFilter})
                  AND T0.DocDate BETWEEN '{fromDate}' AND '{toDate}'
                  {statusFilter}
                  AND T0.CANCELED = 'N' -- (Tùy chọn) Thường sẽ lọc bỏ delivery đã bị hủy trong SAP
                ORDER BY T0.DocEntry";

                using (var cmd = new SqlCommand(queryInvoice, conn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dtSAP);
                }
            }
            GV_Header_Delivery.DataSource = dtSAP;
        }
        private async void btnCreateAR_Click(object sender, EventArgs e)
        {
            var selectedDeliveries = GV_Header_Delivery.Rows
                .Cast<DataGridViewRow>()
                .Where(r => Convert.ToBoolean(r.Cells["Select"].Value) == true)
                .Select(r => Convert.ToInt32(r.Cells["DocEntry"].Value))
                .ToList();

            if (selectedDeliveries.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Delivery để tạo Invoice.", "Thông báo");
                return;
            }

            btnCreateAR.Enabled = false;
            string resultLog = "";

            try
            {
                resultLog = await Task.Run(() =>
                {
                    SAPbobsCOM.Company oCompany = null;
                    Documents oDelivery = null;
                    Documents oARInvoice = null;
                    StringBuilder log = new StringBuilder();
                    int successCount = 0;
                    int failCount = 0;

                    try
                    {
                        oCompany = SAPHelper.ConnectToSAP(); 
                        if (oCompany == null || !oCompany.Connected) return "Không kết nối được SAP B1!";

                        oDelivery = (Documents)oCompany.GetBusinessObject(BoObjectTypes.oDeliveryNotes);

                        foreach (int docEntry in selectedDeliveries)
                        {
                            try
                            {
                                if (!oDelivery.GetByKey(docEntry))
                                {
                                    log.AppendLine($"❌ Không tìm thấy Delivery DocEntry: {docEntry}");
                                    failCount++;
                                    continue;
                                }

                                if (oDelivery.DocumentStatus == BoStatus.bost_Close)
                                {
                                    log.AppendLine($"⚠️ Delivery {oDelivery.DocNum} đã đóng (Closed). Bỏ qua.");
                                    failCount++;
                                    continue;
                                }

                                oARInvoice = (Documents)oCompany.GetBusinessObject(BoObjectTypes.oInvoices);

                                oARInvoice.CardCode = oDelivery.CardCode;
                                oARInvoice.DocDate = DateTime.Now;
                                oARInvoice.DocDueDate = DateTime.Now;
                                oARInvoice.Comments = $"Based on Delivery {oDelivery.DocNum}.";

                                oARInvoice.NumAtCard = oDelivery.NumAtCard;
                                oARInvoice.BPL_IDAssignedToInvoice = oDelivery.BPL_IDAssignedToInvoice; 

                                bool hasLines = false;

                                for (int i = 0; i < oDelivery.Lines.Count; i++)
                                {
                                    oDelivery.Lines.SetCurrentLine(i);

                                    if (oDelivery.Lines.LineStatus != BoStatus.bost_Open)
                                        continue;

                                    if (hasLines)
                                    {
                                        oARInvoice.Lines.Add();
                                    }

                                    oARInvoice.Lines.BaseType = 15; 
                                    oARInvoice.Lines.BaseEntry = docEntry;
                                    oARInvoice.Lines.BaseLine = i; 
                                    hasLines = true;
                                }

                                if (!hasLines)
                                {
                                    log.AppendLine($"⚠️ Delivery {oDelivery.DocNum} không còn dòng nào mở để tạo Invoice.");
                                    failCount++;
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(oARInvoice);
                                    oARInvoice = null;
                                    continue;
                                }
                                int newDocEntry = int.Parse(oCompany.GetNewObjectKey());
                                Documents oARUpdate =
                                    (Documents)oCompany.GetBusinessObject(BoObjectTypes.oInvoices);

                                if (oARUpdate.GetByKey(newDocEntry))
                                {
                                    for (int line = 0; line < oARUpdate.Lines.Count; line++)
                                    {
                                        oARUpdate.Lines.SetCurrentLine(line);

                                        oARUpdate.Lines.UserFields.Fields.Item("U_BaseType").Value = "15";
                                        oARUpdate.Lines.UserFields.Fields.Item("U_BaseEntry").Value = docEntry.ToString();
                                        oARUpdate.Lines.UserFields.Fields.Item("U_BaseLine").Value = line;
                                    }

                                    int updRet = oARUpdate.Update();
                                    if (updRet != 0)
                                    {
                                        oCompany.GetLastError(out int uErr, out string uMsg);
                                        log.AppendLine($"⚠️ Invoice {newDocEntry}: Update UDF lỗi - {uMsg}");
                                    }
                                }

                                Marshal.ReleaseComObject(oARUpdate);
                                if (oARInvoice.Add() == 0)
                                {
                                    string newKey = oCompany.GetNewObjectKey();
                                    log.AppendLine($"✅ Thành công: Delivery {oDelivery.DocNum} -> Invoice DocEntry {newKey}");
                                    successCount++;
                                }

                                else
                                {
                                    oCompany.GetLastError(out int errCode, out string errMsg);
                                    log.AppendLine($"❌ Lỗi Delivery {oDelivery.DocNum}: {errMsg}");
                                    failCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                log.AppendLine($"❌ Lỗi ngoại lệ DocEntry {docEntry}: {ex.Message}");
                                failCount++;
                            }
                            finally
                            {
                                if (oARInvoice != null)
                                {
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(oARInvoice);
                                    oARInvoice = null;
                                }
                            }
                        } 

                        return $"Kết quả xử lý:\n- Thành công: {successCount}\n- Thất bại: {failCount}\n\nChi tiết:\n{log}";
                    }
                    catch (Exception ex)
                    {
                        return "Lỗi hệ thống: " + ex.Message;
                    }
                    finally
                    {
                        if (oDelivery != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(oDelivery);
                        if (oCompany != null && oCompany.Connected) oCompany.Disconnect();
                    }
                });

                MessageBox.Show(resultLog, "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
            finally
            {
                btnCreateAR.Enabled = true;
            }
        }
        /*private void btnCheckConnection_Click(object sender, EventArgs e)
        {
            // Kiểm tra kết nối SQL Server
            using (var conn = ConnectToSQL())
            {
                if (conn != null && conn.State == ConnectionState.Open)
                {
                    MessageBox.Show("Kết nối SQL Server thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    conn.Close();
                }
                else
                {
                    MessageBox.Show("Kết nối SQL Server thất bại!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private async void btnCreate110_Click(object sender, EventArgs e)
        {
            try
            {
                string inputForm = txtForm.Text.Trim();  
                string inputSerial = cbSerial.Text.Trim(); 

                if (string.IsNullOrEmpty(inputForm) || string.IsNullOrEmpty(inputSerial))
                {
                    MessageBox.Show("Vui lòng nhập Mẫu số và Ký hiệu!");
                    return;
                }

                txtResult110.Text = "Đang gửi lệnh 110...";

                // 1. Chuẩn bị dữ liệu với CmdType 110
                var listInvoiceData = _sap.PrepareInvoiceData(CommandType.CreateInvoiceWithFormSerial, inputForm, inputSerial);

                // 2. Gửi lệnh thông qua BkavService
                var result = await _bkavService.ExecCommandAsync(CommandType.CreateInvoiceWithFormSerial, listInvoiceData);

                if (result.Status != 0)
                {
                    txtResult110.Text = "Lỗi hệ thống: " + result.MessLog;
                    return;
                }

                // 3. Xử lý kết quả trả về
                // Bkav trả về List<InvoiceResult> dưới dạng JSON string trong result.Object
                var listResult = JsonConvert.DeserializeObject<List<InvoiceResult>>(result.Object.ToString());

                string log = "";
                foreach (var item in listResult)
                {
                    if (item.Status == 0)
                    {
                        log += "--- TẠO HÓA ĐƠN THÀNH CÔNG (CMD 110) ---\r\n";
                        log += $"PartnerInvoiceID: {item.PartnerInvoiceID}\r\n";
                        log += $"Mẫu số: {item.InvoiceForm}\r\n";
                        log += $"Ký hiệu: {item.InvoiceSerial}\r\n";
                        log += $"Số HĐ: {item.InvoiceNo} (Mới tạo nên thường là 0)\r\n";
                        txtPartnerInvoiceID.Text = $"{item.PartnerInvoiceID}\r\n";
                    }
                    else
                    {
                        log += $"Lỗi dòng {item.PartnerInvoiceID}: {item.MessLog}\r\n";
                    }
                }
                txtResult110.Text = log;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
        }
        // 2. Chức năng Lấy thông tin hóa đơn (Cmd 800)
        private async void btnGetInfo_Click(object sender, EventArgs e)
        {
            try
            {
                // Giả sử lấy theo InvoiceGUID (nhập từ textbox hoặc hardcode test)
                string inputGuid = txtPartnerInvoiceID.Text;

                // Cmd 800 nhận đầu vào là chuỗi GUID hoặc PartnerID
                var result = await _bkavService.ExecCommandAsync(CommandType.GetInvoiceDataWS, inputGuid);

                if (result.Status == 0)
                {
                    // Convert Object trả về thành InvoiceDataWS
                    var invoiceData = JsonConvert.DeserializeObject<InvoiceDataWS>(result.Object.ToString());

                    txtResult2.Text = $"Số HĐ: {invoiceData.Invoice.InvoiceNo}\r\n" +
                                     $"Người mua: {invoiceData.Invoice.BuyerName}\r\n" +
                                     $"Trạng thái hóa đơn: {invoiceData.Invoice.InvoiceStatusID}\r\n" +
                                     $"Tổng tiền: {invoiceData.ListInvoiceDetailsWS[0].Amount}";
                }
                else
                {
                    txtResult2.Text = "Lỗi: " + result.MessLog;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void GV_Header_Delivery_SelectionChanged(object sender, EventArgs e)
        {
            if (GV_Header_Delivery.SelectedRows.Count == 0)
                return;

            var selectedRow = GV_Header_Delivery.SelectedRows[0];
            if (selectedRow.Cells["DocEntry"].Value == null)
                return;

            int docEntry;
            if (!int.TryParse(selectedRow.Cells["DocEntry"].Value.ToString(), out docEntry))
                return;

            // Use ConnectToSQL instead of ConnectToSAP3
            using (var conn = ConnectToSQL())
            {
                if (conn == null)
                    return;

                string query = $@"
                SELECT 
                    ROW_NUMBER() OVER (ORDER BY T0.DocEntry) AS STT,
                    T0.DocEntry, 
                    T0.LineNum, 
                    T0.ItemCode, 
                    T0.Dscription,
                    T0.UomCode,
                    T0.Quantity, 
                    ISNULL(TR.ReturnQty,0) AS ReturnQty,
                    (T0.Quantity - ISNULL(TR.ReturnQty,0)) AS NetQuantity,
                    T0.PriceBefDi,
                    T0.Currency,
                    T0.LineTotal, 
                    T0.VatGroup,
                    CAST(CAST(ROUND(T0.VatPrcnt, 0) AS INT) AS VARCHAR(10)) + ' %' AS VatPrcnt,
                    T0.LineVAT,
                    T0.GTotal, 
                    T0.WhsCode, 
                    T0.AcctCode,
                    T0.TrgetEntry,
                    T0.LineStatus
                FROM DLN1 T0 WITH(NOLOCK)
                LEFT JOIN (
                    SELECT R1.BaseEntry, R1.BaseLine, SUM(R1.Quantity) AS ReturnQty
                    FROM RDN1 R1 WITH(NOLOCK)
                    INNER JOIN ORDN R0 WITH(NOLOCK) ON R0.DocEntry = R1.DocEntry
                    WHERE R0.CANCELED = 'N'
                    GROUP BY R1.BaseEntry, R1.BaseLine
                ) TR ON TR.BaseEntry = T0.DocEntry AND TR.BaseLine = T0.LineNum
                WHERE T0.DocEntry = {docEntry}
                ORDER BY T0.LineNum
                ";

                DataTable dt = new DataTable();
                dt.Columns.Add("STT");
                dt.Columns.Add("DocEntry");
                dt.Columns.Add("LineNum");
                dt.Columns.Add("Dscription");
                dt.Columns.Add("UomCode");
                dt.Columns.Add("ItemCode");
                dt.Columns.Add("Quantity");
                dt.Columns.Add("ReturnQty");
                dt.Columns.Add("NetQuantity");
                dt.Columns.Add("PriceBefDi");
                dt.Columns.Add("Currency");
                dt.Columns.Add("WhsCode");
                dt.Columns.Add("AcctCode");
                dt.Columns.Add("VatGroup");
                dt.Columns.Add("VatPrcnt");
                dt.Columns.Add("LineVAT");
                dt.Columns.Add("LineTotal");
                dt.Columns.Add("GTotal");
                dt.Columns.Add("TrgetEntry");
                dt.Columns.Add("LineStatus");

                using (var cmd = new SqlCommand(query, conn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }

                GV_Body_Delivery.DataSource = dt;

                if (GV_Body_Delivery.Columns.Contains("AcctCode"))
                {
                    GV_Body_Delivery.Columns["AcctCode"].HeaderText = "G\\L Account";
                }
                if (GV_Body_Delivery.Columns.Contains("Dscription"))
                {
                    GV_Body_Delivery.Columns["Dscription"].HeaderText = "ItemName";
                }
                if (GV_Body_Delivery.Columns.Contains("TrgetEntry"))
                {
                    GV_Body_Delivery.Columns["TrgetEntry"].HeaderText = "Return DocEntry";
                }
                if (GV_Body_Delivery.Columns.Contains("PriceBefDi"))
                {
                    GV_Body_Delivery.Columns["PriceBefDi"].HeaderText = "Unit Price";
                }
            }
        }

        /////////////-------------BUTTON CREATE INVOICE-----------------///////////////
        private async void btnCreateInvoice_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Validate (Giữ nguyên)
                string inputForm = txtForm.Text.Trim();
                string inputSerial = cbSerial.Text.Trim();

                if (string.IsNullOrEmpty(inputForm) || string.IsNullOrEmpty(inputSerial))
                {
                    MessageBox.Show("Vui lòng nhập Mẫu số và Ký hiệu!");
                    return;
                }

                // 2. Lấy list Delivery được chọn (Giữ nguyên)
                var selectedRows = GV_Header_Delivery.Rows
                    .Cast<DataGridViewRow>()
                    .Where(r => r.Cells["Select"].Value is bool b && b)
                    .ToList();

                if (selectedRows.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một Delivery!");
                    return;
                }

                txtResult110.Text = $"Đang xử lý {selectedRows.Count} hóa đơn...";
                List<InvoiceDataWS> lstInvoices = new List<InvoiceDataWS>();

                // 3. Build dữ liệu (Giữ nguyên)
                foreach (var row in selectedRows)
                {
                    int docEntry = Convert.ToInt32(row.Cells["DocEntry"].Value);
                    DataRow headerRow = ((DataRowView)row.DataBoundItem).Row;
                    var details = _sap.LoadInvoiceDetails(docEntry);

                    var inv = PrepareInvoiceData_SAP(
                        CommandType.CreateInvoiceWithFormSerial,
                        inputForm,
                        inputSerial,
                        headerRow,
                        details,
                        docEntry);

                    lstInvoices.Add(inv);
                }

                // 4. Gửi lên Bkav (Giữ nguyên)
                var result = await _bkavService.ExecCommandAsync(
                    CommandType.CreateInvoiceWithFormSerial,
                    lstInvoices);

                // 5. Kiểm tra lỗi hệ thống
                if (result.Status != 0)
                {
                    txtResult110.Text = "Lỗi hệ thống Bkav: " + result.MessLog;
                    return;
                }

                // 6. PHÂN TÍCH KẾT QUẢ & LƯU DATABASE
                var listResult = JsonConvert.DeserializeObject<List<InvoiceResult>>(result.Object.ToString());

                // --- SỬA ĐOẠN NÀY ---
                // Không cần truyền connectionString nữa vì DbSaver sẽ tự gọi ConnectToSQL_v2
                DbSaver dbSaver = new DbSaver();
                // --------------------

                int successCount = 0;
                string log = "";

                for (int i = 0; i < listResult.Count; i++)
                {
                    var resItem = listResult[i];
                    var reqItem = lstInvoices[i];
                    int sapDocEntry = Convert.ToInt32(selectedRows[i].Cells["DocEntry"].Value);

                    if (resItem.Status == 0)
                    {
                        successCount++;
                        try
                        {
                            // Hàm SaveInvoiceToDb bây giờ tự mở kết nối bên trong
                            dbSaver.SaveInvoiceToDb(reqItem, resItem, sapDocEntry);

                            log += $"✔ Thành công & Đã lưu DB (DocEntry: {sapDocEntry})\r\n";
                            log += $"    GUID  : {resItem.InvoiceGUID}\r\n";
                            log += $"    Parnert Invoice ID : {resItem.PartnerInvoiceID}\r\n";
                            log += $"    Số HĐ : {resItem.InvoiceNo}\r\n";

                            txtPartnerInvoiceID.Text = $"{resItem.PartnerInvoiceID}";
                        }
                        catch (Exception dbEx)
                        {
                            log += $"⚠️ Tạo BKAV OK nhưng LỖI LƯU DB (DocEntry: {sapDocEntry})\r\n";
                            log += $"    Lỗi DB: {dbEx.Message}\r\n";
                        }
                    }
                    else
                    {
                        log += $"❌ Lỗi BKAV (DocEntry: {sapDocEntry})\r\n";
                        log += $"    Chi tiết: {resItem.MessLog}\r\n";
                    }
                    log += "--------------------------------------------------\r\n";
                }

                txtResult110.Text = $"Tổng kết: {successCount}/{listResult.Count} thành công.\r\n\r\n" + log;
                MessageBox.Show($"Tổng kết: {successCount}/{listResult.Count} hóa đơn thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
        }
        private async void btnCreateInvoicePO_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Validate (Giữ nguyên)
                string inputForm = txtForm.Text.Trim();
                string inputSerial = cbSerial.Text.Trim();

                if (string.IsNullOrEmpty(inputForm) || string.IsNullOrEmpty(inputSerial))
                {
                    MessageBox.Show("Vui lòng nhập Mẫu số và Ký hiệu!");
                    return;
                }

                // 2. Lấy list Delivery được chọn (Giữ nguyên)
                var selectedRows = GV_Header_Delivery.Rows
                    .Cast<DataGridViewRow>()
                    .Where(r => r.Cells["Select"].Value is bool b && b)
                    .ToList();

                if (selectedRows.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một Delivery!");
                    return;
                }

                txtResult110.Text = $"Đang xử lý {selectedRows.Count} hóa đơn...";
                List<InvoiceDataWS> lstInvoices = new List<InvoiceDataWS>();

                // 3. Build dữ liệu (Giữ nguyên)
                foreach (var row in selectedRows)
                {
                    int docEntry = Convert.ToInt32(row.Cells["DocEntry"].Value);
                    DataRow headerRow = ((DataRowView)row.DataBoundItem).Row;
                    var details = _sap.LoadInvoiceDetails(docEntry);

                    var inv = PrepareInvoiceData_SAPPO(
                        CommandType.CreateInvoiceWithFormSerial,
                        inputForm,
                        inputSerial,
                        headerRow,
                        details,
                        docEntry);

                    lstInvoices.Add(inv);
                }

                // 4. Gửi lên Bkav (Giữ nguyên)
                var result = await _bkavService.ExecCommandAsync(
                    CommandType.CreateInvoiceWithFormSerial,
                    lstInvoices);

                // 5. Kiểm tra lỗi hệ thống
                if (result.Status != 0)
                {
                    txtResult110.Text = "Lỗi hệ thống Bkav: " + result.MessLog;
                    return;
                }

                // 6. PHÂN TÍCH KẾT QUẢ & LƯU DATABASE
                var listResult = JsonConvert.DeserializeObject<List<InvoiceResult>>(result.Object.ToString());

                // --- SỬA ĐOẠN NÀY ---
                // Không cần truyền connectionString nữa vì DbSaver sẽ tự gọi ConnectToSQL_v2
                DbSaver dbSaver = new DbSaver();
                // --------------------

                int successCount = 0;
                string log = "";

                for (int i = 0; i < listResult.Count; i++)
                {
                    var resItem = listResult[i];
                    var reqItem = lstInvoices[i];
                    int sapDocEntry = Convert.ToInt32(selectedRows[i].Cells["DocEntry"].Value);

                    if (resItem.Status == 0)
                    {
                        successCount++;
                        try
                        {
                            // Hàm SaveInvoiceToDb bây giờ tự mở kết nối bên trong
                            dbSaver.SaveInvoiceToDb(reqItem, resItem, sapDocEntry);

                            log += $"✔ Thành công & Đã lưu DB (DocEntry: {sapDocEntry})\r\n";
                            log += $"    GUID  : {resItem.InvoiceGUID}\r\n";
                            log += $"    Parnert Invoice ID : {resItem.PartnerInvoiceID}\r\n";
                            log += $"    Số HĐ : {resItem.InvoiceNo}\r\n";

                            txtPartnerInvoiceID.Text = $"{resItem.PartnerInvoiceID}";
                        }
                        catch (Exception dbEx)
                        {
                            log += $"⚠️ Tạo BKAV OK nhưng LỖI LƯU DB (DocEntry: {sapDocEntry})\r\n";
                            log += $"    Lỗi DB: {dbEx.Message}\r\n";
                        }
                    }
                    else
                    {
                        log += $"❌ Lỗi BKAV (DocEntry: {sapDocEntry})\r\n";
                        log += $"    Chi tiết: {resItem.MessLog}\r\n";
                    }
                    log += "--------------------------------------------------\r\n";
                }

                txtResult110.Text = $"Tổng kết: {successCount}/{listResult.Count} thành công.\r\n\r\n" + log;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
        }
        private async void btnCreateInvoiceName_PO_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Validate (Giữ nguyên)
                string inputForm = txtForm.Text.Trim();
                string inputSerial = cbSerial.Text.Trim();

                if (string.IsNullOrEmpty(inputForm) || string.IsNullOrEmpty(inputSerial))
                {
                    MessageBox.Show("Vui lòng nhập Mẫu số và Ký hiệu!");
                    return;
                }

                // 2. Lấy list Delivery được chọn (Giữ nguyên)
                var selectedRows = GV_Header_Delivery.Rows
                    .Cast<DataGridViewRow>()
                    .Where(r => r.Cells["Select"].Value is bool b && b)
                    .ToList();

                if (selectedRows.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một Delivery!");
                    return;
                }

                txtResult110.Text = $"Đang xử lý {selectedRows.Count} hóa đơn...";
                List<InvoiceDataWS> lstInvoices = new List<InvoiceDataWS>();

                // 3. Build dữ liệu (Giữ nguyên)
                foreach (var row in selectedRows)
                {
                    int docEntry = Convert.ToInt32(row.Cells["DocEntry"].Value);
                    DataRow headerRow = ((DataRowView)row.DataBoundItem).Row;
                    var details = _sap.LoadInvoiceDetails(docEntry);

                    var inv = PrepareInvoiceData_SAPName_PO(
                        CommandType.CreateInvoiceWithFormSerial,
                        inputForm,
                        inputSerial,
                        headerRow,
                        details,
                        docEntry);

                    lstInvoices.Add(inv);
                }

                // 4. Gửi lên Bkav (Giữ nguyên)
                var result = await _bkavService.ExecCommandAsync(
                    CommandType.CreateInvoiceWithFormSerial,
                    lstInvoices);

                // 5. Kiểm tra lỗi hệ thống
                if (result.Status != 0)
                {
                    txtResult110.Text = "Lỗi hệ thống Bkav: " + result.MessLog;
                    return;
                }

                // 6. PHÂN TÍCH KẾT QUẢ & LƯU DATABASE
                var listResult = JsonConvert.DeserializeObject<List<InvoiceResult>>(result.Object.ToString());

                // --- SỬA ĐOẠN NÀY ---
                // Không cần truyền connectionString nữa vì DbSaver sẽ tự gọi ConnectToSQL_v2
                DbSaver dbSaver = new DbSaver();
                // --------------------

                int successCount = 0;
                string log = "";

                for (int i = 0; i < listResult.Count; i++)
                {
                    var resItem = listResult[i];
                    var reqItem = lstInvoices[i];
                    int sapDocEntry = Convert.ToInt32(selectedRows[i].Cells["DocEntry"].Value);

                    if (resItem.Status == 0)
                    {
                        successCount++;
                        try
                        {
                            // Hàm SaveInvoiceToDb bây giờ tự mở kết nối bên trong
                            dbSaver.SaveInvoiceToDb(reqItem, resItem, sapDocEntry);

                            log += $"✔ Thành công & Đã lưu DB (DocEntry: {sapDocEntry})\r\n";
                            log += $"    GUID  : {resItem.InvoiceGUID}\r\n";
                            log += $"    Parnert Invoice ID : {resItem.PartnerInvoiceID}\r\n";
                            log += $"    Số HĐ : {resItem.InvoiceNo}\r\n";

                            txtPartnerInvoiceID.Text = $"{resItem.PartnerInvoiceID}";
                        }
                        catch (Exception dbEx)
                        {
                            log += $"⚠️ Tạo BKAV OK nhưng LỖI LƯU DB (DocEntry: {sapDocEntry})\r\n";
                            log += $"    Lỗi DB: {dbEx.Message}\r\n";
                        }
                    }
                    else
                    {
                        log += $"❌ Lỗi BKAV (DocEntry: {sapDocEntry})\r\n";
                        log += $"    Chi tiết: {resItem.MessLog}\r\n";
                    }
                    log += "--------------------------------------------------\r\n";
                }

                txtResult110.Text = $"Tổng kết: {successCount}/{listResult.Count} thành công.\r\n\r\n" + log;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
        }
        private async void btnGetList_Click(object sender, EventArgs e)
        {
            try
            {
                var filter = new
                {
                    InvoiceDateFrom = dtpFrom?.Value.ToString("yyyy-MM-dd"),
                    InvoiceDateTo = dtpTo?.Value.ToString("yyyy-MM-dd"),
                    PageNumber = 1
                };

                if (_bkavService == null)
                {
                    MessageBox.Show("BkavService chưa được khởi tạo.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var result = await _bkavService.ExecCommandAsync(853, filter);

                if (result == null)
                {
                    MessageBox.Show("Không nhận được kết quả từ dịch vụ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (result.Status != 0)
                {
                    MessageBox.Show("Lỗi: " + result.MessLog);
                    return;
                }

                if (result.Object == null)
                {
                    MessageBox.Show("Dữ liệu trả về rỗng.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var listAll = JsonConvert.DeserializeObject<List<InvoiceDataWS>>(result.Object.ToString());

                if (listAll == null || listAll.Count == 0)
                {
                    MessageBox.Show("Không có dữ liệu.");
                    return;
                }

                List<InvoiceDataWS> listFiltered = listAll;

                var listView = new List<InvoiceViewModel>();
                int stt = 1;
                foreach (var item in listFiltered)
                {
                    if (item?.Invoice == null || item.ListInvoiceDetailsWS == null)
                        continue;
                    listView.Add(new InvoiceViewModel
                    {
                        STT = stt++,
                        PartnerID = item.PartnerInvoiceID.ToString(),
                        MauSo = item.Invoice.InvoiceForm,
                        KyHieu = item.Invoice.InvoiceSerial,
                        SoHD = item.Invoice.InvoiceNo.ToString("00000000"),
                        NgayHD = item.Invoice.InvoiceDate,
                        KhachHang = item.Invoice.BuyerName,
                        TongTien = item.ListInvoiceDetailsWS.Sum(x => x?.Amount ?? 0),
                        GhiChu = item.Invoice.BillCode,
                        TrangThai = _sap.GetStatusName(item.Invoice.InvoiceStatusID),                        
                        InvoiceGUID = item.Invoice.InvoiceGUID.ToString(),
                    });
                }
                dgvInvoices.DataSource = listView;
                if (dgvInvoices.Columns["SAP_DocEntry"] != null)
                {
                    dgvInvoices.Columns["SAP_DocEntry"].Visible = false;
                }
                _sap.FormatGrid(dgvInvoices);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }
        private async void btnPreview_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Lấy ID hóa đơn cần xem (Ví dụ lấy từ dòng đang chọn trên GridView)
                if (dgvInvoices.CurrentRow == null)
                {
                    MessageBox.Show("Vui lòng chọn hóa đơn cần xem!");
                    return;
                }

                // Lấy PartnerID hoặc InvoiceGUID từ Grid
                // Giả sử cột PartnerID là cột đầu tiên hoặc có tên "PartnerID"
                string partnerInvoiceID = dgvInvoices.CurrentRow.Cells["PartnerID"].Value.ToString();

                txtPreview.Text = "Đang tải PDF preview...";

                // 2. Gọi API 808
                // Input của 808 là chuỗi String (PartnerID hoặc GUID)
                var result = await _bkavService.ExecCommandAsync(808, partnerInvoiceID);

                if (result.Status != 0)
                {
                    MessageBox.Show("Lỗi: " + result.MessLog);
                    return;
                }

                // 3. Xử lý kết quả trả về
                // Kết quả API 808 là chuỗi JSON: {"PDF": "Base64String...", "XML": null}
                // Bạn cần tạo class hứng hoặc dùng dynamic
                dynamic fileData = JsonConvert.DeserializeObject(result.Object.ToString());
                string base64Pdf = fileData.PDF;

                if (string.IsNullOrEmpty(base64Pdf))
                {
                    MessageBox.Show("Không lấy được dữ liệu PDF.");
                    return;
                }

                // 4. Giải mã Base64
                byte[] pdfBytes = Convert.FromBase64String(base64Pdf);

                // Đặt tên file mặc định (Gọn gàng)
                string baseFileName = $"Invoice_{partnerInvoiceID}.pdf";
                string tempFolderPath = Path.GetTempPath();
                string finalFilePath = Path.Combine(tempFolderPath, baseFileName);

                try
                {
                    // Thử xóa file cũ nếu tồn tại
                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath);
                    }
                }
                catch (IOException)
                {
                    // NẾU LỖI (Do file đang mở): 
                    // Thì mới phải đổi tên file thêm giờ phút giây để tránh trùng
                    string uniqueFileName = $"Invoice_{partnerInvoiceID}_{DateTime.Now.Ticks}.pdf";
                    finalFilePath = Path.Combine(tempFolderPath, uniqueFileName);
                }

                // Ghi ra file (Lúc này finalFilePath chắc chắn là đường dẫn ghi được)
                File.WriteAllBytes(finalFilePath, pdfBytes);

                // 5. Mở file
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = finalFilePath,
                    UseShellExecute = true
                });

                txtPreview.Text = "Đã mở file: " + finalFilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }
        private void btnGetPending_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Reset GridView
                dgvInvoicesPending.DataSource = null;
                txtResultPending.Text = "Đang tải dữ liệu từ Database nội bộ...";

                DateTime fromDate = dtpFromPending.Value;
                DateTime toDate = dtpToPending.Value;

                // 2. Gọi hàm truy vấn SQL
                DbSaver dbSaver = new DbSaver();
                List<InvoiceViewModel> listFromDb = dbSaver.GetInvoicesByDate(fromDate, toDate);

                if (listFromDb == null || listFromDb.Count == 0)
                {
                    MessageBox.Show($"Không tìm thấy hóa đơn nào trong Database từ {fromDate:dd/MM} đến {toDate:dd/MM}.");
                    return;
                }

                // 3. Cập nhật tên Trạng thái
                foreach (var item in listFromDb)
                {
                    if (int.TryParse(item.TrangThai, out int statusId))
                    {
                        item.TrangThai = _sap.GetStatusName(statusId);
                    }
                    // Mặc định chưa chọn dòng nào
                    item.Select = false;
                }

                // 4. Gán dữ liệu
                dgvInvoicesPending.DataSource = listFromDb;

                // 5. CẤU HÌNH GIAO DIỆN (QUAN TRỌNG)

                // Cho phép chỉnh sửa (để tích được checkbox)
                dgvInvoicesPending.ReadOnly = false;

                foreach (DataGridViewColumn col in dgvInvoicesPending.Columns)
                {
                    // Chỉ cột "Chon" là được sửa, các cột khác khóa lại
                    if (col.Name == "Select")
                    {
                        col.HeaderText = "Chọn";
                        col.ReadOnly = false;
                        col.Width = 50;
                    }
                    else
                    {
                        col.ReadOnly = true;
                    }
                }
*//*
                // Ẩn cột InvoiceGUID để giao diện gọn gàng
                if (dgvInvoicesPending.Columns["InvoiceGUID"] != null)
                    dgvInvoicesPending.Columns["InvoiceGUID"].Visible = false;*//*

                // Format tiền tệ, ngày tháng
                _sap.FormatGrid(dgvInvoicesPending);

                txtResultPending.Text = $"Đã tải {listFromDb.Count} hóa đơn từ Database.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi truy vấn Database: " + ex.Message);
            }
        }
        private async void btnSignInvoice_Click(object sender, EventArgs e)
        {
            dgvInvoicesPending.EndEdit();

            // 1. Lọc và Validate (Giữ nguyên)
            var dataSource = dgvInvoicesPending.DataSource as List<InvoiceViewModel>;
            if (dataSource == null) return;
            var selectedInvoices = dataSource.Where(x => x.Select).ToList();

            if (selectedInvoices.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất một hóa đơn để ký!");
                return;
            }

            if (MessageBox.Show($"Bạn có chắc chắn muốn KÝ {selectedInvoices.Count} hóa đơn đã chọn?",
                "Xác nhận ký hàng loạt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                return;
            }

            txtResultPending.Text = "Đang thực hiện ký...";
            int successCount = 0;
            string log = "";
            DbSaver dbSaver = new DbSaver();

            // 2. VÒNG LẶP KÝ (Giữ nguyên)
            foreach (var invoice in selectedInvoices)
            {
                if (string.IsNullOrEmpty(invoice.InvoiceGUID)) continue;

                try
                {
                    var resultSign = await _bkavService.ExecCommandAsync(205, invoice.InvoiceGUID);

                    if (resultSign.Status == 0)
                    {
                        // Cập nhật trạng thái 2 (Đã phát hành), Số tạm để 0
                        dbSaver.UpdateSignedInvoice(invoice.InvoiceGUID, 2, 0);
                        successCount++;
                        log += $"✅ PartnerID {invoice.PartnerID}: Ký thành công (Đang chờ cấp số).\r\n";
                    }
                    else
                    {
                        // ... (Xử lý lỗi giữ nguyên)
                    }
                }
                catch (Exception ex)
                {
                    log += $"❌ PartnerID {invoice.PartnerID}: Lỗi Exception ({ex.Message})\r\n";
                }
            }

            // 3. ĐỒNG BỘ SỐ HÓA ĐƠN (SỬA ĐOẠN NÀY)
            if (successCount > 0)
            {
                txtResultPending.Text = log + "\r\n⏳ Đang chờ BKAV cấp số (Vui lòng đợi 5s)...";

                // --- THÊM DÒNG NÀY ---
                // Chờ 5 giây để Server BKAV kịp cập nhật dữ liệu
                await Task.Delay(5000);
                // ---------------------

                DateTime syncDate = selectedInvoices.First().NgayHD ?? DateTime.Now;

                // Gọi hàm đồng bộ
                await SyncInvoiceInfoFromBkav(syncDate);

                log += "\r\n✅ Đã cập nhật Số hóa đơn chính thức về Database.";
            }

            // 4. Kết thúc
            MessageBox.Show($"Hoàn tất quá trình ký.\nThành công: {successCount}/{selectedInvoices.Count}");
            txtResultPending.Text = log;

            btnGetPending_Click(null, null);
        }
        private async Task SyncInvoiceInfoFromBkav(DateTime syncDate)
        {
            try
            {
                // 1. Gọi API 853 lấy danh sách hóa đơn trong ngày
                var filter = new
                {
                    InvoiceDateFrom = syncDate.ToString("yyyy-MM-dd"),
                    InvoiceDateTo = syncDate.ToString("yyyy-MM-dd"),
                    PageNumber = 1 // Lưu ý: Nếu nhiều hóa đơn cần vòng lặp lấy hết các trang
                };

                var result = await _bkavService.ExecCommandAsync(853, filter);

                if (result.Status != 0) return; // Lỗi thì bỏ qua

                var listBkav = JsonConvert.DeserializeObject<List<InvoiceDataWS>>(result.Object.ToString());

                if (listBkav == null || listBkav.Count == 0) return;

                // 2. Cập nhật vào DB
                DbSaver dbSaver = new DbSaver();
                int updateCount = 0;

                foreach (var item in listBkav)
                {
                    // Chỉ cập nhật những hóa đơn đã có số (Status 2, 3, 5...)
                    if (item.Invoice.InvoiceNo > 0)
                    {
                        // Tìm trong DB theo GUID để cập nhật lại số hóa đơn chính xác
                        // Status 2: Đã phát hành (Hoặc lấy theo item.Invoice.InvoiceStatusID thực tế)
                        dbSaver.UpdateSignedInvoice(
                            item.Invoice.InvoiceGUID.ToString(),
                            item.Invoice.InvoiceStatusID,
                            item.Invoice.InvoiceNo
                        );
                        updateCount++;
                    }
                }

                // (Optional) Thông báo nhỏ
                // MessageBox.Show($"Đã đồng bộ số hóa đơn cho {updateCount} phiếu.");
            }
            catch (Exception ex)
            {
                // Ghi log lỗi ngầm, không show popup để tránh làm phiền user
                Console.WriteLine("Sync Error: " + ex.Message);
            }
        }
        private async void btnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Xác nhận dữ liệu trên Grid
                dgvInvoicesPending.EndEdit();

                // Lấy danh sách nguồn
                var dataSource = dgvInvoicesPending.DataSource as List<InvoiceViewModel>;
                if (dataSource == null) return;

                // Lọc các dòng được tích chọn (x.Select hoặc x.Chon tùy theo tên biến trong ViewModel của bạn)
                var selectedInvoices = dataSource.Where(x => x.Select).ToList();

                if (selectedInvoices.Count == 0)
                {
                    MessageBox.Show("Vui lòng tích chọn ít nhất một hóa đơn để xóa!");
                    return;
                }

                // 2. Hỏi xác nhận
                if (MessageBox.Show($"Bạn có chắc chắn muốn XÓA {selectedInvoices.Count} hóa đơn đã chọn không?\n\nHành động này sẽ xóa hóa đơn khỏi hệ thống BKAV và Database nội bộ.",
                    "Xác nhận xóa hàng loạt", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                {
                    return;
                }

                txtResultPending.Text = "Đang thực hiện xóa...";
                int successCount = 0;
                string log = "";

                // Khởi tạo DbSaver
                DbSaver dbSaver = new DbSaver();

                // 3. Vòng lặp xử lý từng hóa đơn
                foreach (var invoice in selectedInvoices)
                {
                    // --- A. VALIDATION TRƯỚC KHI GỌI API ---

                    // Kiểm tra trạng thái: Chỉ xóa được hóa đơn Mới tạo (Status 1) hoặc Chờ ký (Status 11)
                    // Nếu đã phát hành (Status 2) hoặc Đã hủy (Status 3) thì bỏ qua
                    if (!string.IsNullOrEmpty(invoice.TrangThai) &&
                       (invoice.TrangThai.Contains("Đã phát hành") || invoice.TrangThai.Contains("Đã hủy")))
                    {
                        log += $"⚠️ PartnerID {invoice.PartnerID}: Bỏ qua (Trạng thái không được phép xóa)\r\n";
                        continue;
                    }

                    // Chuẩn bị ID
                    string invoiceGuid = invoice.InvoiceGUID;
                    string partnerId = invoice.PartnerID.ToString();

                    // --- B. CHUẨN BỊ GÓI TIN ---
                    int cmdType = 0;
                    object cmdData = null;

                    // Ưu tiên xóa theo GUID (Cmd 303) nếu có, ngược lại xóa theo PartnerID (Cmd 301)
                    if (!string.IsNullOrEmpty(invoiceGuid))
                    {
                        cmdType = CommandType.DeleteInvoiceByInvoiceGUID; // 303
                        cmdData = new List<object>
                        {
                            new
                            {
                                Invoice = new { InvoiceGUID = invoiceGuid },
                                Reason = "Hủy bỏ hóa đơn nháp (Xóa hàng loạt từ App)"
                            }
                        };
                    }
                    else if (!string.IsNullOrEmpty(partnerId))
                    {
                        cmdType = CommandType.DeleteInvoiceByPartnerInvoiceID; // 301
                        cmdData = new List<object>
                        {
                            new
                            {
                                PartnerInvoiceID = partnerId,
                                PartnerInvoiceStringID = "",
                                Invoice = new { Reason = "Hủy bỏ hóa đơn nháp (Xóa hàng loạt từ App)" }
                            }
                        };
                    }
                    else
                    {
                        log += $"❌ PartnerID {partnerId}: Lỗi (Thiếu thông tin ID để xóa)\r\n";
                        continue;
                    }

                    // --- C. GỌI API & XỬ LÝ ---
                    try
                    {
                        var result = await _bkavService.ExecCommandAsync(cmdType, cmdData);

                        if (result.Status == 0)
                        {
                            // QUAN TRỌNG: Lấy đúng SAP_DocEntry từ ViewModel để xóa trong DB
                            string sapDocEntry = invoice.SAP_DocEntry;

                            // Gọi hàm xóa
                            dbSaver.DeleteInvoiceFromDb(sapDocEntry, invoiceGuid);

                            successCount++;
                            log += $"✅ DocEntry {sapDocEntry}: Đã xóa thành công.\r\n";
                        }
                        else
                        {
                            // Lấy thông báo lỗi
                            string errorMsg = result.Object != null ? result.Object.ToString() : result.MessLog;
                            log += $"❌ PartnerID {partnerId}: Lỗi BKAV ({errorMsg})\r\n";
                        }
                    }
                    catch (Exception exLoop)
                    {
                        log += $"❌ PartnerID {partnerId}: Lỗi Exception ({exLoop.Message})\r\n";
                    }
                }

                // 4. Kết thúc và Cập nhật giao diện
                MessageBox.Show($"Hoàn tất quá trình xóa.\nThành công: {successCount}/{selectedInvoices.Count}");
                txtResultPending.Text = log;

                // Refresh lại lưới để ẩn các dòng đã xóa
                btnGetPending_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi hệ thống: " + ex.Message);
            }
        }
        
        private DateTime? GetDateFromCell(object cellValue)
        {
            if (cellValue == null || cellValue == DBNull.Value) return null;

            if (DateTime.TryParse(cellValue.ToString(), out DateTime dt))
            {
                return dt;
            }
            return null;
        }
*/
        private void btnLoadheaderinvoice_Click(object sender, EventArgs e)
        {
            string fromDate = dateTimePicker1.Value.ToString("yyyy-MM-dd");
            string toDate = dateTimePicker2.Value.ToString("yyyy-MM-dd");

            string statusFilter = "";
            if (checkBox_Open.Checked && !checkBox_Closed.Checked)
                statusFilter = "AND T0.DocStatus = 'O'";
            else if (!checkBox_Open.Checked && checkBox_Closed.Checked)
                statusFilter = "AND T0.DocStatus = 'C'";

            var cardCodes = textBox5.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToList();
            string whereCardCodes = " AND T0.CardCode LIKE 'C%' ";
            if (cardCodes.Count > 0)
            {
                string codeList = string.Join(",", cardCodes.Select(c => $"'{c}'"));
                whereCardCodes = $"AND T0.CardCode IN ({codeList})";
            }
            var itemCodes = textBox4.Text
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .ToList();
            string whereItemCodes = "";
            if (itemCodes.Count > 0)
            {
                string codeList = string.Join(",", itemCodes.Select(c => $"'{c}'"));
                whereItemCodes = $"AND SAP_DocEntry IN ({codeList})";
            }
            DataTable dtSAP = new DataTable();
            dtSAP.Columns.Add("STT");
            dtSAP.Columns.Add("DocEntry");
            dtSAP.Columns.Add("CardCode");
            dtSAP.Columns.Add("CardName");
            dtSAP.Columns.Add("NumAtCard");
            dtSAP.Columns.Add("DocStatus");
            dtSAP.Columns.Add("DocType");
            dtSAP.Columns.Add("DocDate");
            dtSAP.Columns.Add("DocDueDate");
            dtSAP.Columns.Add("LicTradNum");
            dtSAP.Columns.Add("DocTotal");
            dtSAP.Columns.Add("Comments");
            dtSAP.Columns.Add("ReturnDocEntry"); 

            using (var conn = ConnectToSQL_v2())
            {
                if (conn == null)
                {
                    MessageBox.Show("Kết nối SQL Server thất bại!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string queryInvoice = $@"
                SELECT 
                    ROW_NUMBER() OVER (ORDER BY T0.DocEntry) AS STT,
                    T0.DocEntry, 
                    T0.CardCode, 
                    T0.CardName, 
                    T0.NumAtCard,
                    T0.DocStatus, 
                    T0.DocType, 
                    CONVERT(varchar(10), T0.DocDate, 103) AS DocDate, 
                    CONVERT(varchar(10), T0.DocDueDate, 103) AS DocDueDate, 
                    T0.LicTradNum,
                    T0.DocTotal,
                    T0.Comments,
                    ISNULL(R0.DocEntry, 0) AS ReturnDocEntry,
                    T0.U_InvCode,
                    T0.U_InvSerial,
                    T0.U_InvDate,
                    T0.U_DeclarePd,
                    T0.U_MaHD,
                    T0.U_EInvoiceCode 
                FROM [KITCHEN_4PS_SAP_DB].[dbo].[OINV] T0 WITH(NOLOCK)
                OUTER APPLY (
                    SELECT TOP 1 R0.DocEntry
                    FROM [KITCHEN_4PS_SAP_DB].[dbo].[ORIN] R0 WITH(NOLOCK)              
                    INNER JOIN [KITCHEN_4PS_SAP_DB].[dbo].[RIN1] R1 ON R1.DocEntry = R0.DocEntry
                    WHERE R1.BaseEntry = T0.DocEntry       
                      AND R1.BaseType = 13                 
                      AND R0.CANCELED = 'N'
                    ORDER BY R0.DocEntry
                ) R0
                WHERE T0.DocDate BETWEEN '{fromDate}' AND '{toDate}'
                  {whereItemCodes}
                  {whereCardCodes} 
                  {statusFilter}
                  AND T0.CANCELED = 'N' -- (Tùy chọn) Thường sẽ lọc bỏ AR Invoice đã bị hủy trong SAP
                ORDER BY T0.DocEntry";

                using (var cmd = new SqlCommand(queryInvoice, conn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dtSAP);
                }
            }
            dtgridviewLoadheaderinvoice.DataSource = dtSAP;
        }
        private async void btnsyncdbwhsar_Click(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                _cts.Cancel();
                _isRunning = false;
                btnsyncdbwhsar.Text = "Start";
                Log("🛑 ĐÃ DỪNG PIPELINE");
                return;
            }

            if (!int.TryParse(textBox1.Text, out int step1Delay) || step1Delay <= 0 ||
                !int.TryParse(textBox2.Text, out int step2Delay) || step2Delay <= 0)
            {
                MessageBox.Show("Nhập thời gian hợp lệ cho Step 1 và Step 2");
                return;
            }

            richTextBox1.Clear();
            btnsyncdbwhsar.Text = "Stop";

            _cts = new CancellationTokenSource();
            _isRunning = true;
            await RunPipelineAsync(step1Delay, step2Delay, _cts.Token);
        }
        private async Task RunPipelineAsync(int step1Delay, int step2Delay, CancellationToken token)
        {
            int cycle = 0;
            int consecutiveErrorCount = 0;
            const int maxConsecutiveErrors = 3;

            while (!token.IsCancellationRequested)
            {
                cycle++;
                DateTime runDate = DateTime.Now;

                try
                {
                    Log($"🔄 VÒNG {cycle} - [PHASE 1] BẮT ĐẦU LẤY DATA SAP");

                    // Chạy tuần tự 3 store mới tách
                    await Task.Run(() => RunStep1_Sequence(runDate));

                    Log($"✔️ VÒNG {cycle} - [PHASE 1] HOÀN TẤT");

                    Log($"⏳ Phase 1 Delay: {step1Delay}s");
                    await Task.Delay(step1Delay * 1000, token);

                    Log($"🧾 VÒNG {cycle} - [PHASE 2] BẮT ĐẦU UPDATE DBWHS");

                    await Task.Run(() => RunStep2_UpdateDBWHS(runDate));

                    Log($"✔️ VÒNG {cycle} - [PHASE 2] HOÀN TẤT");

                    Log($"⏳ Phase 2 Delay: {step2Delay}s");
                    await Task.Delay(step2Delay * 1000, token);

                    // Reset error count if successful
                    consecutiveErrorCount = 0;
                }
                catch (OperationCanceledException)
                {
                    Log("🛑 PIPELINE BỊ HỦY BỞI NGƯỜI DÙNG");
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrorCount++;
                    Log($"❌ VÒNG {cycle} - LỖI: {ex.Message} (Liên tiếp: {consecutiveErrorCount}/{maxConsecutiveErrors})");
                    if (consecutiveErrorCount >= maxConsecutiveErrors)
                    {
                        Log("🛑 ĐÃ GẶP LỖI 3 LẦN LIÊN TIẾP, DỪNG PIPELINE!");
                        break;
                    }
                    await Task.Delay(5000, token);
                }
            }
            if (InvokeRequired) Invoke(new Action(() => btnsyncdbwhsar.Text = "Start"));
            else btnsyncdbwhsar.Text = "Start";
            _isRunning = false;
        }
        private void RunStep1_Sequence(DateTime runDate)
        {
            string dateStr = runDate.ToString("ddMMyyyy");
            using (var sqlConn = PrepareSAPData.ConnectToSQL_v2())
            {
                // 1. Insert Header
                Log("  ├─ 1.1 Tạo Header...");
                ExecuteStore(sqlConn, "usp_BKAV_InsertHeader_Step1", dateStr);

                // 3. Insert Detail
                Log("  ├─ 1.2 Tạo Detail...");
                ExecuteStore(sqlConn, "usp_BKAV_InsertDetail_Step2", dateStr);

                // 2. Update SAP
                Log("  ├─ 1.3 Update SAP...");
                //ExecuteStore(sqlConn, "usp_BKAV_UpdateSAPFlag_Step3", dateStr);
            }
        }
        private void RunStep2_UpdateDBWHS(DateTime runDate)
        {
            Log("  └─ 2.1 Update DBWHS to SAP Header");

            using (var sqlConn = PrepareSAPData.ConnectToSQL_v2())
            {
                using (var cmd = new SqlCommand("usp_BKAV_UpdateSAP_Step4", sqlConn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandTimeout = 0;
                    cmd.Parameters.Add("@Date", SqlDbType.NVarChar, 50).Value = runDate.ToString("ddMMyyyy");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ExecuteStore(SqlConnection conn, string storeName, string dateParam)
        {
            using (SqlCommand cmd = new SqlCommand(storeName, conn))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add("@Date", SqlDbType.NVarChar, 50).Value = dateParam;
                cmd.CommandTimeout = 0; 
                cmd.ExecuteNonQuery();
            }
        }

        private void Log(string message)
        {
            if (richTextBox1.InvokeRequired)
            {
                richTextBox1.Invoke(new Action(() => Log(message)));
                return;
            }

            if (richTextBox1.TextLength > 50000) 
            {
                richTextBox1.Clear();
                richTextBox1.AppendText("[...Clearing old logs...]\n");
            }

            richTextBox1.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            richTextBox1.ScrollToCaret();
        }
        private async void btnStartManual_Click(object sender, EventArgs e)
        {
            if (_isSyncing)
            {
                MessageBox.Show("Hệ thống đang chạy auto job, vui lòng chờ hoặc dừng trước.");
                return;
            }

            DateTime selectedDate = dateTimePicker3.Value.Date;

            var confirm = MessageBox.Show(
                $"Bạn có chắc muốn UPDATE SAP theo ngày {selectedDate:dd/MM/yyyy} ?",
                "Xác nhận chạy tay",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            Log("===================================");
            Log($"🟡 CHẠY MANUAL UPDATE SAP");
            Log($"📅 Ngày được chọn: {selectedDate:dd/MM/yyyy}");

            try
            {
                _isSyncing = true;

                await Task.Run(() =>
                {
                    RunStep1_Sequence(selectedDate);
                });
                await Task.Run(() =>
                {
                    RunStep2_UpdateDBWHS(selectedDate);
                });

                Log("✅ MANUAL UPDATE SAP HOÀN TẤT");
            }
            catch (Exception ex)
            {
                Log($"❌ MANUAL UPDATE SAP LỖI: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
                Log("===================================");
            }
        }
        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in GV_Header_Delivery.Rows)
            {
                row.Cells["Select"].Value = true;
            }
        }

        private void btnDeselectAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in GV_Header_Delivery.Rows)
            {
                row.Cells["Select"].Value = false;
            }
        }
        private void btnCheckConnection_Click(object sender, EventArgs e)
        {
            using (var conn = PrepareSAPData.ConnectToSQL())
            {
                if (conn != null && conn.State == System.Data.ConnectionState.Open)
                {
                    richTextBox3.AppendText($"[{DateTime.Now:HH:mm:ss}] Kết nối SQL Server thành công!{Environment.NewLine}");
                }
                else
                {
                    richTextBox3.AppendText($"[{DateTime.Now:HH:mm:ss}] Kết nối SQL Server thất bại!{Environment.NewLine}");
                }
            }
        }

        private void btnconnectionCheck_Click(object sender, EventArgs e)
        {
            using (var conn = PrepareSAPData.ConnectToSQL_v2())
            {
                if (conn != null && conn.State == System.Data.ConnectionState.Open)
                {
                    richTextBox2.AppendText($"[{DateTime.Now:HH:mm:ss}] Kết nối SQL Server thành công!{Environment.NewLine}");
                }
                else
                {
                    richTextBox2.AppendText($"[{DateTime.Now:HH:mm:ss}] Kết nối SQL Server thất bại!{Environment.NewLine}");
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string fromDate = dateTimePicker5.Value.ToString("yyyy-MM-dd");
            string toDate = dateTimePicker4.Value.ToString("yyyy-MM-dd");

            var itemCodes = textBox3.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToList();

            string whereItemCodes = "";
            if (itemCodes.Count > 0)
            {
                string codeList = string.Join(",", itemCodes.Select(c => $"'{c}'"));
                whereItemCodes = $"AND SAP_DocEntry IN ({codeList})";
            }

            DataTable dtSAP = new DataTable();

            dtSAP.Columns.Add("STT");
            dtSAP.Columns.Add("SAP_DocEntry");
            dtSAP.Columns.Add("SAP_ObjType");
            dtSAP.Columns.Add("PartnerInvoiceID");
            dtSAP.Columns.Add("PartnerInvoiceStringID");
            dtSAP.Columns.Add("InvoiceGUID");
            dtSAP.Columns.Add("InvoiceTypeID");
            dtSAP.Columns.Add("InvoiceDate");
            dtSAP.Columns.Add("InvoiceForm");
            dtSAP.Columns.Add("InvoiceSerial");
            dtSAP.Columns.Add("InvoiceNo");
            //dtSAP.Columns.Add("InvoiceCode");
            dtSAP.Columns.Add("SignedDate");
            dtSAP.Columns.Add("InvoiceStatusID");
            dtSAP.Columns.Add("BuyerName");
            dtSAP.Columns.Add("BuyerTaxCode");
            dtSAP.Columns.Add("BuyerCCCD");
            dtSAP.Columns.Add("BuyerUnitName");
            dtSAP.Columns.Add("BuyerAddress");
            dtSAP.Columns.Add("BuyerBankAccount");
            dtSAP.Columns.Add("ReceiverName");
            dtSAP.Columns.Add("ReceiverEmail");
            dtSAP.Columns.Add("ReceiverMobile");
            dtSAP.Columns.Add("ReceiverAddress");
            dtSAP.Columns.Add("ReceiveTypeID");
            dtSAP.Columns.Add("PayMethodID");
            dtSAP.Columns.Add("CurrencyID");
            dtSAP.Columns.Add("ExchangeRate");
            //dtSAP.Columns.Add("TotalPayment");
            dtSAP.Columns.Add("Note");
            dtSAP.Columns.Add("BillCode");
            dtSAP.Columns.Add("UserDefine");
            dtSAP.Columns.Add("TypeCreateInvoice");
            dtSAP.Columns.Add("MaCuaCQT");
            //dtSAP.Columns.Add("OriginalInvoiceIdentify");
            dtSAP.Columns.Add("CreatedDate");
            dtSAP.Columns.Add("CreatedBy");
            //dtSAP.Columns.Add("AccountantReq");
            //dtSAP.Columns.Add("KTVApproved");
            dtSAP.Columns.Add("InvoiceSyncStatus");
            dtSAP.Columns.Add("UpdatedDate");
            dtSAP.Columns.Add("CANCELED");
            dtSAP.Columns.Add("isSigned");
            dtSAP.Columns.Add("isError");

            using (var conn = ConnectToSQL_v2())
            {
                if (conn == null)
                {
                    MessageBox.Show("Kết nối SQL Server thất bại!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string queryInvoice = $@"
                    SELECT ROW_NUMBER() OVER (ORDER BY SAP_DocEntry) AS STT
                          ,[SAP_DocEntry]
                          ,[SAP_ObjType]
                          ,[PartnerInvoiceID]
                          ,[PartnerInvoiceStringID]
                          ,[InvoiceGUID]
                          ,[InvoiceTypeID]
                          ,[InvoiceDate]
                          ,[InvoiceForm]
                          ,[InvoiceSerial]
                          ,[InvoiceNo]
                          --,[InvoiceCode]
                          ,[SignedDate]
                          ,[InvoiceStatusID]
                          ,[BuyerName]
                          ,[BuyerTaxCode]
                          ,[BuyerCCCD]
                          ,[BuyerUnitName]
                          ,[BuyerAddress]
                          ,[BuyerBankAccount]
                          ,[ReceiverName]
                          ,[ReceiverEmail]
                          ,[ReceiverMobile]
                          ,[ReceiverAddress]
                          ,[ReceiveTypeID]
                          ,[PayMethodID]
                          ,[CurrencyID]
                          ,[ExchangeRate]
                          --,[TotalPayment]
                          ,[Note]
                          ,[BillCode]
                          ,[UserDefine]
                          ,[TypeCreateInvoice]
                          ,[MaCuaCQT]
                          --,[OriginalInvoiceIdentify]
                          ,[CreatedDate]
                          ,[CreatedBy]
                          --,[AccountantReq]
                          --,[KTVApproved]
                          ,[InvoiceSyncStatus]
                          ,[UpdatedDate]
                          ,[CANCELED]
                          ,[isSigned]
                          ,[isError]
                      FROM [FMCG_AR].[dbo].[BKAV_InvoiceHeader] WITH(NOLOCK) 
                    WHERE CreatedDate >= '{fromDate}' AND CreatedDate < DATEADD(DAY, 1,'{toDate}')
                    {whereItemCodes}
                    ORDER BY SAP_DocEntry";
                using (var cmd = new SqlCommand(queryInvoice, conn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dtSAP);
                }
            }
            dataGridView1.DataSource = dtSAP;
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;

            var selectedRow = dataGridView1.SelectedRows[0];
            if (selectedRow.Cells["SAP_DocEntry"].Value == null)
                return;

            int docEntry;
            if (!int.TryParse(selectedRow.Cells["SAP_DocEntry"].Value.ToString(), out docEntry))
                return;

            // Use ConnectToSQL instead of ConnectToSAP3
            using (var conn = ConnectToSQL_v2())
            {
                if (conn == null)
                    return;

                string query = $@"
                SELECT TOP (1000) D.[ID]
                      ,D.[HeaderID]
                      ,D.[ItemTypeID]
                      ,D.[ItemName]
                      ,D.[UnitName]
                      ,D.[Qty]
                      ,D.[Price]
                      ,D.[Amount]
                      ,D.[TaxRateID]
                      ,D.[TaxAmount]
                      ,D.[IsDiscount]
                      ,D.[IsIncrease]
                      ,D.[UserDefineDetails]
                      ,D.[SAP_LineNum]
                      ,D.[DiscountRate]
                      ,D.[DiscountAmount]
                      ,D.[SAP_DocEntry]
                FROM [FMCG_AR].[dbo].[BKAV_InvoiceDetail] D WITH(NOLOCK)
                LEFT JOIN [FMCG_AR].[dbo].[BKAV_InvoiceHeader] H ON H.SAP_DocEntry = D.SAP_DocEntry
                WHERE D.SAP_DocEntry = {docEntry}
                ORDER BY D.SAP_LineNum
                ";

                DataTable dt = new DataTable();
                using (var cmd = new SqlCommand(query, conn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }

                dataGridView2.DataSource = dt;
            }
        }
    }
}