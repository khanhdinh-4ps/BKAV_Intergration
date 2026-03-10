using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static BKAV_Intergration.Models;
using CommandType = BKAV_Intergration.Models.CommandType;

namespace BKAV_Intergration
{
    internal class PrepareSAPData
    {
        // --- CÁC HÀM TẠO DỮ LIỆU MẪU (Convert từ Default.aspx.cs) ---

        public List<InvoiceDataWS> PrepareInvoiceData(int commandType, string form = "", string serial = "")
        {
            List<InvoiceDataWS> list = new List<InvoiceDataWS>();

            // 1. Tạo Header hóa đơn
            InvoiceWS invoice = new InvoiceWS
            {
                InvoiceTypeID = 1,
                InvoiceDate = DateTime.Now,
                BuyerName = "Khách hàng téo buấn",
                BuyerTaxCode = "001187009296", //MST trên hóa đơn có cấu trúc:● 10 ký tự số ●	14 ký tự với ký tự thứ 11 là dấu ‘-’. Các ký tự còn lại là số
                BuyerUnitName = "Công ty Test",
                BuyerAddress = "Hà Nội",
                PayMethodID = 3,
                ReceiveTypeID = 3,
                ReceiverEmail = "teobuan@bkav.com", // Email nhận HĐ
                CurrencyID = "VND",
                ExchangeRate = 1.0,
                InvoiceStatusID = 1,
                ReceiverAddress = "Hà Nội",
                ReceiverName = "Test",
                BillCode = "RDM",
                BuyerBankAccount = "12411",
            };

            // 2. Xử lý riêng cho từng mã lệnh
            InvoiceDetailsWS detail = null;
            switch (commandType)
            {
                case CommandType.CreateInvoiceAdjustDiscount: // 110
                    // Bắt buộc phải truyền Mẫu số và Ký hiệu
                    invoice.InvoiceNo = 0;
                    invoice.InvoiceForm = form;
                    invoice.InvoiceSerial = "C26TRD";
                    detail = new InvoiceDetailsWS
                    {
                        ItemName = "Dịch vụ 110",
                        UnitName = "Lần",
                        Qty = 1,
                        Price = -100000,
                        Amount = -90000,
                        TaxRateID = Constants.TaxRateID.Muoi, // Thuế 10%
                        TaxAmount = 0,
                        IsDiscount = true,
                        DiscountRate = 10,
                        DiscountAmount = -10000
                    };
                    break;

            }

            var invoiceData = new InvoiceDataWS();
            invoiceData.Invoice = invoice;
            invoiceData.ListInvoiceDetailsWS = detail != null ? new List<InvoiceDetailsWS> { detail } : new List<InvoiceDetailsWS>();

            // ID định danh duy nhất cho lần gửi này (Client tự sinh)
            invoiceData.PartnerInvoiceID = long.Parse(DateTime.Now.ToString("ddMMyyyyHHmmss"));
            invoiceData.PartnerInvoiceStringID = "";

            list.Add(invoiceData);
            return list;
        }
        //---- SAP: CÁC HÀM MÃ HÓA/GIẢI MÃ AES VÀ KẾT NỐI SQL SERVER -----

        public string EncryptAES(string plainText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                var keyBytes = new Rfc2898DeriveBytes(key, Encoding.UTF8.GetBytes("s@ltValue"), 1000);
                aes.Key = keyBytes.GetBytes(32);
                aes.IV = keyBytes.GetBytes(16);

                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(inputBytes, 0, inputBytes.Length);
                    cs.Close();
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
        public string DecryptAES(string cipherText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                var keyBytes = new Rfc2898DeriveBytes(key, Encoding.UTF8.GetBytes("s@ltValue"), 1000);
                aes.Key = keyBytes.GetBytes(32);
                aes.IV = keyBytes.GetBytes(16);

                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    byte[] cipherBytes = Convert.FromBase64String(cipherText);
                    cs.Write(cipherBytes, 0, cipherBytes.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
        public static SqlConnection ConnectToSQL()
        {
            string decryptKey = "34bcf4830ab7dfa70e9fd4c5daacd7ed2983099b31d65c8c4089d3d2b2b26b40";
            string connStrRaw = ConfigurationManager.ConnectionStrings["SAP_DATABASE"].ConnectionString;

            string finalConnectionString = connStrRaw;
            try
            {
                // Sử dụng hàm DecryptAES của chính PrepareSAPData (không dùng Form1)
                var decryptor = new PrepareSAPData();
                finalConnectionString = decryptor.DecryptAES(connStrRaw, decryptKey);
            }
            catch
            {
                // Nếu giải mã lỗi thì dùng chuỗi gốc
            }

            var conn = new SqlConnection(connStrRaw);

            try
            {
                conn.Open();
                return conn;
            }
            catch
            {
                return null;
            }
        }
        public static SqlConnection ConnectToSQL_v2()
        {
            string decryptKey = "34bcf4830ab7dfa70e9fd4c5daacd7ed2983099b31d65c8c4089d3d2b2b26b40";

            string connStrRaw = ConfigurationManager.ConnectionStrings["SQL_DATABASE"]?.ConnectionString;

            if (string.IsNullOrEmpty(connStrRaw))
            {
                throw new Exception("Không tìm thấy chuỗi kết nối 'SQL_DATABASE' trong file Config.");
            }

            string finalConnectionString = connStrRaw;
            try
            {
                var decryptor = new PrepareSAPData();
                finalConnectionString = decryptor.DecryptAES(connStrRaw, decryptKey);
            }
            catch
            {

            }

            var builder = new SqlConnectionStringBuilder(finalConnectionString);
            builder.ConnectTimeout = 60;

            var conn = new SqlConnection(builder.ToString());

            try
            {
                conn.Open();
                return conn;
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi mở kết nối SQL: {ex.Message}");
            }
        }
        public static InvoiceDataWS PrepareInvoiceData_SAP(int commandType, string form, string serial, DataRow headerRow, List<InvoiceDetailsWS> details, int sapDocEntry)
        {
            // Tạo PartnerID (ID gói tin)
            string uniqueIDString = DateTime.Now.ToString("HHmmss")+sapDocEntry.ToString();
            long uniqueID = long.Parse(uniqueIDString);

            // TẠO MÃ CQT THEO CHUẨN 23 KÝ TỰ
            string strMaCuaCQT = GenerateMaCQT(sapDocEntry);

            InvoiceWS invoice = new InvoiceWS
            {
                InvoiceTypeID = 1,
                InvoiceDate = DateTime.Now,
                BuyerName = headerRow["CardName"].ToString(),
                BuyerTaxCode = headerRow.Table.Columns.Contains("LicTradNum") ? headerRow["LicTradNum"].ToString() : "",
                BuyerUnitName = headerRow["CardName"].ToString(),
                BuyerAddress = headerRow.Table.Columns.Contains("Address") ? headerRow["Address"].ToString() : "",
                PayMethodID = 3,
                ReceiveTypeID = 3,
                ReceiverEmail = "teobuan@bkav.com",
                CurrencyID = "VND",
                ExchangeRate = 1.0,
                InvoiceStatusID = 1,
                CCCD = "066202012353",
                BillCode = "Base on Delivery: " + sapDocEntry.ToString(),

                InvoiceNo = 0, // Mới tạo phải bằng 0
                InvoiceForm = form,
                InvoiceSerial = serial, // Lưu ý: Serial phải có chữ 'M', vd: C25MYY

                // --- GÁN MÃ CQT ---
                // Chuỗi này sẽ có dạng: M5-24-POS01-00000069730
                MaCuaCQT = strMaCuaCQT,
                // ------------------

                UserDefine = uniqueID.ToString(),
                BuyerBankAccount = "",
                ReceiverAddress = "",
                ReceiverName = "",
            };

            var invoiceData = new InvoiceDataWS
            {
                Invoice = invoice,
                ListInvoiceDetailsWS = details,
                PartnerInvoiceID = uniqueID,
                PartnerInvoiceStringID = "",
            };

            return invoiceData;
        }
        public static InvoiceDataWS PrepareInvoiceData_SAPPO(int commandType, string form, string serial, DataRow headerRow, List<InvoiceDetailsWS> details, int sapDocEntry)
        {
            // Tạo PartnerID (ID gói tin)
            string uniqueIDString = DateTime.Now.ToString("HHmmss") + sapDocEntry.ToString();
            long uniqueID = long.Parse(uniqueIDString);

            // TẠO MÃ CQT THEO CHUẨN 23 KÝ TỰ
            string strMaCuaCQT = GenerateMaCQT(sapDocEntry);

            InvoiceWS invoice = new InvoiceWS
            {
                InvoiceTypeID = 1,
                InvoiceDate = DateTime.Now,
                BuyerName = headerRow["NumAtCard"].ToString(),
                BuyerTaxCode = headerRow.Table.Columns.Contains("LicTradNum") ? headerRow["LicTradNum"].ToString() : "",
                BuyerUnitName = headerRow["CardName"].ToString(),
                BuyerAddress = headerRow.Table.Columns.Contains("Address") ? headerRow["Address"].ToString() : "",
                PayMethodID = 3,
                ReceiveTypeID = 3,
                ReceiverEmail = "hoadondientu@winmart.masangroup.com",
                CurrencyID = "VND",
                ExchangeRate = 1.0,
                InvoiceStatusID = 1,
                CCCD = "066202012353",
                BillCode = "Base on Delivery: " + sapDocEntry.ToString(),

                InvoiceNo = 0, // Mới tạo phải bằng 0
                InvoiceForm = form,
                InvoiceSerial = serial, // Lưu ý: Serial phải có chữ 'M', vd: C25MYY

                // --- GÁN MÃ CQT ---
                // Chuỗi này sẽ có dạng: M5-24-POS01-00000069730
                MaCuaCQT = strMaCuaCQT,
                // ------------------

                UserDefine = uniqueID.ToString(),
                BuyerBankAccount = "",
                ReceiverAddress = "03 Lương Yên, Phường Bạch Đằng, Hà Nội",
                ReceiverName = "",
            };

            var invoiceData = new InvoiceDataWS
            {
                Invoice = invoice,
                ListInvoiceDetailsWS = details,
                PartnerInvoiceID = uniqueID,
                PartnerInvoiceStringID = "",
            };

            return invoiceData;
        }
        public static InvoiceDataWS PrepareInvoiceData_SAPName_PO(int commandType, string form, string serial, DataRow headerRow, List<InvoiceDetailsWS> details, int sapDocEntry)
        {
            // Tạo PartnerID (ID gói tin)
            string uniqueIDString = DateTime.Now.ToString("HHmmss") + sapDocEntry.ToString();
            long uniqueID = long.Parse(uniqueIDString);

            // TẠO MÃ CQT THEO CHUẨN 23 KÝ TỰ
            string strMaCuaCQT = GenerateMaCQT(sapDocEntry);

            InvoiceWS invoice = new InvoiceWS
            {
                InvoiceTypeID = 1,
                InvoiceDate = DateTime.Now,
                BuyerName = $"{headerRow["CardName"].ToString() + " + " + headerRow["NumAtCard"].ToString()}",
                BuyerTaxCode = headerRow.Table.Columns.Contains("LicTradNum") ? headerRow["LicTradNum"].ToString() : "",
                BuyerUnitName = headerRow["CardName"].ToString(),
                BuyerAddress = headerRow.Table.Columns.Contains("Address") ? headerRow["Address"].ToString() : "",
                PayMethodID = 3,
                ReceiveTypeID = 3,
                ReceiverEmail = "teobuan@bkav.com",
                CurrencyID = "VND",
                ExchangeRate = 1.0,
                InvoiceStatusID = 1,

                BillCode = "Base on Delivery: " + sapDocEntry.ToString(),

                InvoiceNo = 0, // Mới tạo phải bằng 0
                InvoiceForm = form,
                InvoiceSerial = serial, // Lưu ý: Serial phải có chữ 'M', vd: C25MYY

                // --- GÁN MÃ CQT ---
                // Chuỗi này sẽ có dạng: M5-24-POS01-00000069730
                MaCuaCQT = strMaCuaCQT,
                // ------------------

                UserDefine = uniqueID.ToString(),
                BuyerBankAccount = "",
                ReceiverAddress = "",
                ReceiverName = "",
            };

            var invoiceData = new InvoiceDataWS
            {
                Invoice = invoice,
                ListInvoiceDetailsWS = details,
                PartnerInvoiceID = uniqueID,
                PartnerInvoiceStringID = "",
            };

            return invoiceData;
        }
        public List<InvoiceDetailsWS> LoadInvoiceDetails(int docEntry)
        {
            List<InvoiceDetailsWS> list = new List<InvoiceDetailsWS>();

            using (var conn = ConnectToSQL())
            {
                // --- SỬA Ở ĐÂY: Thay DLN1 bằng INV1, RDN1/ORDN bằng RIN1/ORIN (Credit Memo) ---
                string query = @"
                SELECT 
                    ROW_NUMBER() OVER (ORDER BY T0.LineNum) AS STT,
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
                    CAST(CAST(ROUND(T0.VatPrcnt, 0) AS INT) AS VARCHAR(10)) AS VatPrcnt,
                    T0.GTotal,             
                    T0.WhsCode, 
                    T0.AcctCode,
                    T0.LineStatus
                FROM INV1 T0 WITH(NOLOCK) -- Bảng chi tiết Invoice
                LEFT JOIN (
                    -- Tính tổng số lượng đã bị trả lại/giảm trừ bởi Credit Memo (RIN1)
                    SELECT R1.BaseEntry, R1.BaseLine, SUM(R1.Quantity) AS ReturnQty
                    FROM RIN1 R1 WITH(NOLOCK)
                    INNER JOIN ORIN R0 WITH(NOLOCK) ON R0.DocEntry = R1.DocEntry
                    WHERE R0.CANCELED = 'N' 
                      AND R1.BaseType = 13 -- BaseType 13 là A/R Invoice
                    GROUP BY R1.BaseEntry, R1.BaseLine
                ) TR ON TR.BaseEntry = T0.DocEntry AND TR.BaseLine = T0.LineNum
                WHERE T0.DocEntry = @DocEntry
                ORDER BY T0.LineNum";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DocEntry", docEntry);

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            string itemName = rd["Dscription"].ToString();
                            string unitName = rd["UomCode"].ToString();

                            double qty = Convert.ToDouble(rd["NetQuantity"]);

                            // Nếu số lượng sau khi trừ hàng trả lại <= 0 thì bỏ qua dòng này (hoặc xử lý tùy nghiệp vụ)
                            if (qty <= 0) continue;

                            double price = Convert.ToDouble(rd["PriceBefDi"]);

                            // Lưu ý: Amount (Thành tiền chưa thuế) = Price * Qty
                            // Nếu dùng LineTotal của SAP thì nó đã trừ chiết khấu dòng.
                            // BKAV thường yêu cầu: Amount = Price * Qty (nếu không có dòng chiết khấu riêng)
                            // Ở đây tôi tính lại Amount theo Qty thực tế
                            double amount = price * qty;

                            double taxPercent = Convert.ToDouble(rd["VatPrcnt"]);
                            int taxRateID = MapSapTaxPercentToBkavId(taxPercent);

                            // Tính tiền thuế
                            double taxAmount = Math.Round(amount * (taxPercent / 100), 0);

                            list.Add(new InvoiceDetailsWS
                            {
                                ItemName = itemName,
                                UnitName = string.IsNullOrEmpty(unitName) ? "Lần" : unitName,
                                Qty = qty,
                                Price = price,
                                Amount = amount,
                                TaxRateID = taxRateID,
                                TaxAmount = taxAmount,
                                IsDiscount = false,
                                ItemTypeID = 0
                            });
                        }
                    }
                }
            }
            return list;
        }

        //---- CÁC HÀM PHỤ TRỢ ĐỊNH DẠNG HIỂN THỊ TRÊN DATAGRIDVIEW -----//
        // Thêm tham số DataGridView vào hàm FormatGrid để truyền dgvInvoices từ bên ngoài
        public void FormatGrid(DataGridView dgvInvoices)
        {
            if (dgvInvoices.Columns["STT"] != null)
            {
                dgvInvoices.Columns["STT"].Width = 50;
                dgvInvoices.Columns["STT"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgvInvoices.Columns["STT"].ReadOnly = true;
            }
            if (dgvInvoices.Columns["TongTien"] != null)
            {
                dgvInvoices.Columns["TongTien"].DefaultCellStyle.Format = "N0"; // Dạng số: 1,000,000
                dgvInvoices.Columns["TongTien"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (dgvInvoices.Columns["NgayHD"] != null)
            {
                dgvInvoices.Columns["NgayHD"].DefaultCellStyle.Format = "dd/MM/yyyy";
            }
        }
        public string GetStatusName(int statusId)
        {
            switch (statusId)
            {
                case 1: return "1-Mới tạo (Nháp)";
                case 2: return "2-Đã phát hành";
                case 3: return "3-Đã hủy";
                case 5: return "5-Chờ thay thế";
                case 6: return "6-Đã bị thay thế";
                case 7: return "7-Chờ điều chỉnh";
                case 8: return "8-Đã điều chỉnh";
                case 11: return "11-Chờ ký";
                default: return "Khác (" + statusId + ")";
            }
        }
        private int MapSapTaxPercentToBkavId(double taxPercent)
        {
            if (taxPercent == 0) return 1; // 0%
            if (taxPercent == 5) return 2;
            if (taxPercent == 8) return 9;
            if (taxPercent == 7) return 8;
            if (taxPercent == 10) return 3; // 10%

            return 6; // Khác
        }
        public void CleanUpOldTempFiles()
        {
            try
            {
                string tempFolder = Path.GetTempPath();
                // Tìm tất cả file bắt đầu bằng "Invoice_" và đuôi .pdf
                string[] oldFiles = Directory.GetFiles(tempFolder, "Invoice_*.pdf");

                foreach (string file in oldFiles)
                {
                    try
                    {
                        File.Delete(file); // Cố gắng xóa
                    }
                    catch
                    {
                        // Nếu file đang mở thì bỏ qua, không làm gì cả
                    }
                }
            }
            catch { /* Bỏ qua mọi lỗi dọn dẹp */ }
        }
        private static string GenerateMaCQT(int sapDocEntry)
        {
            string time = DateTime.Now.ToString("HHmmss")+sapDocEntry.ToString();
            string maCQT = "25";      // 02 số cuối của năm phát hành hóa đơn được sinh tự động từ phần mềm bán hàng của NNT.
            string maMay = "ZZEMZ";   // Mã máy tính tiền (5 ký tự) - Đã đăng ký với Thuế
            string yearChar = "1";    // 
            string sequence = time.ToString().PadLeft(11, '0');
            return $"M{yearChar}-{maCQT}-{maMay}-{sequence}";
        }
        public string MapInvoiceStatus(string statusId)
        {
            switch (statusId)
            {
                case "0":
                    return "0 - Đang chờ tạo hóa đơn";
                case "1":
                    return "1 - Hóa đơn chờ ký";
                case "2":
                    return "2 - Hóa đơn đã ký";
                default:
                    return statusId;
            }
        }
    }
}
