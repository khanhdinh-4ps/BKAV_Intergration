using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BKAV_Intergration.Models;

namespace BKAV_Intergration
{
    public class DbSaver
    {
        public DbSaver()
        {
            // Không cần connection string trong constructor vì lấy từ static method
        }

        /// <summary>
        /// Lưu hóa đơn vào SQL Server (Mapping Full thông tin từ Models)
        /// </summary>
        public void SaveInvoiceToDb(Models.InvoiceDataWS data, Models.InvoiceResult result, int sapDocEntry)
        {
            // Sử dụng hàm kết nối có sẵn của bạn
            using (SqlConnection conn = PrepareSAPData.ConnectToSQL_v2())
            {
                if (conn == null) throw new Exception("Không thể kết nối Database.");

                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // =================================================================================
                        // 1. INSERT HEADER (Bảng BKAV_InvoiceHeader)
                        // =================================================================================
                        string sqlHeader = @"
                        INSERT INTO BKAV_InvoiceHeader (
                            SAP_DocEntry, PartnerInvoiceID, PartnerInvoiceStringID, InvoiceGUID,
                            InvoiceTypeID, InvoiceDate, InvoiceForm, InvoiceSerial, InvoiceNo,
                            BuyerName, BuyerTaxCode, BuyerCCCD, BuyerUnitName, BuyerAddress, BuyerBankAccount,
                            PayMethodID, ReceiveTypeID, ReceiverEmail, ReceiverMobile, ReceiverAddress, ReceiverName,
                            Note, BillCode, UserDefine, CurrencyID, ExchangeRate, 
                            InvoiceStatusID, TypeCreateInvoice, MaCuaCQT, OriginalInvoiceIdentify,
                            CreatedDate, CreatedBy
                        ) VALUES (
                            @DocEntry, @PartnerID, @PartnerStringID, @Guid,
                            @TypeID, @Date, @Form, @Serial, @No,
                            @Buyer, @TaxCode, @CCCD, @UnitName, @Address, @BankAccount,
                            @PayMethod, @ReceiveType, @Email, @Mobile, @RecAddress, @RecName,
                            @Note, @BillCode, @UserDefine, @Currency, @Rate,
                            @StatusID, @TypeCreate, @MaCuaCQT, @OriginalID,
                            GETDATE(), @CreatedBy
                        );
                        SELECT SCOPE_IDENTITY();";

                        long headerId = 0;

                        using (SqlCommand cmd = new SqlCommand(sqlHeader, conn, trans))
                        {
                            // --- Khóa liên kết ---
                            cmd.Parameters.AddWithValue("@DocEntry", sapDocEntry);
                            cmd.Parameters.AddWithValue("@PartnerID", data.PartnerInvoiceID);
                            cmd.Parameters.AddWithValue("@PartnerStringID", (object)data.PartnerInvoiceStringID ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Guid", result.InvoiceGUID);

                            // --- Thông tin hóa đơn (Lấy từ Result trả về nếu có, hoặc từ Data gửi đi) ---
                            cmd.Parameters.AddWithValue("@TypeID", data.Invoice.InvoiceTypeID);
                            cmd.Parameters.AddWithValue("@Date", data.Invoice.InvoiceDate);

                            // Ưu tiên lấy Form/Serial/No từ kết quả trả về của BKAV
                            cmd.Parameters.AddWithValue("@Form", (object)result.InvoiceForm ?? data.Invoice.InvoiceForm ?? "");
                            cmd.Parameters.AddWithValue("@Serial", (object)result.InvoiceSerial ?? data.Invoice.InvoiceSerial ?? "");
                            cmd.Parameters.AddWithValue("@No", result.InvoiceNo); // Int
                            cmd.Parameters.AddWithValue("@StatusID", result.Status == 0 ? 1 : 0);

                            // --- Thông tin người mua ---
                            cmd.Parameters.AddWithValue("@Buyer", (object)data.Invoice.BuyerName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@TaxCode", (object)data.Invoice.BuyerTaxCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CCCD", (object)data.Invoice.CCCD ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@UnitName", (object)data.Invoice.BuyerUnitName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Address", (object)data.Invoice.BuyerAddress ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BankAccount", (object)data.Invoice.BuyerBankAccount ?? DBNull.Value);

                            // --- Thông tin nhận/thanh toán ---
                            cmd.Parameters.AddWithValue("@PayMethod", data.Invoice.PayMethodID);
                            cmd.Parameters.AddWithValue("@ReceiveType", data.Invoice.ReceiveTypeID);
                            cmd.Parameters.AddWithValue("@Email", (object)data.Invoice.ReceiverEmail ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Mobile", (object)data.Invoice.ReceiverMobile ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RecAddress", (object)data.Invoice.ReceiverAddress ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RecName", (object)data.Invoice.ReceiverName ?? DBNull.Value);

                            // --- Thông tin khác ---
                            cmd.Parameters.AddWithValue("@Note", (object)data.Invoice.Note ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BillCode", (object)data.Invoice.BillCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@UserDefine", (object)data.Invoice.UserDefine ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Currency", (object)data.Invoice.CurrencyID ?? "VND");
                            cmd.Parameters.AddWithValue("@Rate", data.Invoice.ExchangeRate);
                            cmd.Parameters.AddWithValue("@CreatedBy", System.Environment.UserDomainName + "//" + System.Environment.UserName);

                            // --- Trạng thái ---
                            // Nếu tạo mới thành công (Status 0) -> Gán DB là 1 (Mới tạo). 
                            // Hoặc lấy trực tiếp InvoiceStatusID nếu có (thường lúc tạo chưa có StatusID trả về từ result)

                            cmd.Parameters.AddWithValue("@TypeCreate", data.Invoice.TypeCreateInvoice);
                            cmd.Parameters.AddWithValue("@MaCuaCQT", (object)data.Invoice.MaCuaCQT ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@OriginalID", (object)data.Invoice.OriginalInvoiceIdentify ?? DBNull.Value);

                            // Thực thi và lấy ID Header
                            headerId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        // =================================================================================
                        // 2. INSERT DETAILS (Bảng BKAV_InvoiceDetail)
                        // =================================================================================
                        if (data.ListInvoiceDetailsWS != null && data.ListInvoiceDetailsWS.Count > 0)
                        {
                            string sqlDetail = @"
                            INSERT INTO BKAV_InvoiceDetail (
                                HeaderID, ItemTypeID, ItemName, UnitName,
                                Qty, Price, Amount,
                                TaxRateID, TaxAmount,
                                IsDiscount, IsIncrease, UserDefineDetails
                            ) VALUES (
                                @HeaderID, @ItemTypeID, @ItemName, @UnitName,
                                @Qty, @Price, @Amount,
                                @TaxRateID, @TaxAmount,
                                @IsDiscount, @IsIncrease, @UserDefineDetails
                            )";

                            foreach (var item in data.ListInvoiceDetailsWS)
                            {
                                using (SqlCommand cmdDetail = new SqlCommand(sqlDetail, conn, trans))
                                {
                                    cmdDetail.Parameters.AddWithValue("@HeaderID", headerId);

                                    cmdDetail.Parameters.AddWithValue("@ItemTypeID", item.ItemTypeID);
                                    cmdDetail.Parameters.AddWithValue("@ItemName", (object)item.ItemName ?? DBNull.Value);
                                    cmdDetail.Parameters.AddWithValue("@UnitName", (object)item.UnitName ?? DBNull.Value);

                                    cmdDetail.Parameters.AddWithValue("@Qty", item.Qty);
                                    cmdDetail.Parameters.AddWithValue("@Price", item.Price);
                                    cmdDetail.Parameters.AddWithValue("@Amount", item.Amount);

                                    cmdDetail.Parameters.AddWithValue("@TaxRateID", item.TaxRateID);
                                    cmdDetail.Parameters.AddWithValue("@TaxAmount", item.TaxAmount);

                                    cmdDetail.Parameters.AddWithValue("@IsDiscount", item.IsDiscount);
                                    // IsIncrease là nullable bool?
                                    cmdDetail.Parameters.AddWithValue("@IsIncrease", (object)item.IsIncrease ?? DBNull.Value);
                                    cmdDetail.Parameters.AddWithValue("@UserDefineDetails", (object)item.UserDefineDetails ?? DBNull.Value);

                                    cmdDetail.ExecuteNonQuery();
                                }
                            }
                        }

                        // Commit Transaction
                        trans.Commit();
                    }
                    catch (Exception)
                    {
                        trans.Rollback();
                        throw; // Ném lỗi ra ngoài để Form bắt được và log
                    }
                }
            }
        }
        public List<InvoiceViewModel> GetInvoicesByDate(DateTime fromDate, DateTime toDate)
        {
            var list = new List<InvoiceViewModel>();

            using (SqlConnection conn = PrepareSAPData.ConnectToSQL_v2())
            {
                if (conn == null) return list;

                // Câu truy vấn lấy dữ liệu từ bảng Header
                // Bạn có thể thêm điều kiện AND InvoiceStatusID IN (1, 11) nếu chỉ muốn lấy hóa đơn chưa phát hành
                string query = @"
                SELECT 
                    SAP_DocEntry, PartnerInvoiceID, InvoiceGUID,
                    InvoiceForm, InvoiceSerial, InvoiceNo, 
                    InvoiceDate, BuyerName,
                    BillCode, InvoiceStatusID
                FROM BKAV_InvoiceHeader
                WHERE InvoiceDate >= @FromDate AND InvoiceDate <= @ToDate
                ORDER BY InvoiceDate DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Set thời gian từ đầu ngày đến cuối ngày
                    cmd.Parameters.AddWithValue("@FromDate", fromDate.Date);
                    cmd.Parameters.AddWithValue("@ToDate", toDate.Date.AddDays(1).AddSeconds(-1));

                    using (SqlDataReader rd = cmd.ExecuteReader())
                    {
                        int stt = 1;
                        while (rd.Read())
                        {
                            // Xử lý số hóa đơn: Nếu null hoặc 0 thì hiển thị 00000000
                            int invNo = rd["InvoiceNo"] != DBNull.Value ? Convert.ToInt32(rd["InvoiceNo"]) : 0;
                            string strInvNo = invNo.ToString("00000000");

                            list.Add(new InvoiceViewModel
                            {
                                STT = stt++,
                                // Lấy SAP_DocEntry làm PartnerID để dễ tham chiếu ngược lại SAP
                                SAP_DocEntry = rd["SAP_DocEntry"] != DBNull.Value ? rd["SAP_DocEntry"].ToString() : "0",
                                PartnerID = rd["PartnerInvoiceID"] != DBNull.Value ? rd["PartnerInvoiceID"].ToString() : "0",
                                InvoiceGUID = rd["InvoiceGUID"] != DBNull.Value ? rd["InvoiceGUID"].ToString() : "",
                                MauSo = rd["InvoiceForm"].ToString(),
                                KyHieu = rd["InvoiceSerial"].ToString(),
                                SoHD = strInvNo,
                                NgayHD = rd["InvoiceDate"] != DBNull.Value ? Convert.ToDateTime(rd["InvoiceDate"]) : (DateTime?)null,
                                KhachHang = rd["BuyerName"].ToString(),

                                GhiChu = rd["BillCode"].ToString(),

                                // Lưu tạm StatusID (int) vào TrangThai, ở Form sẽ convert sang Text sau
                                TrangThai = rd["InvoiceStatusID"] != DBNull.Value ? rd["InvoiceStatusID"].ToString() : "0"
                            });
                        }
                    }
                }
            }
            return list;
        }
        public void UpdateSignedInvoice(string invoiceGuid, int newStatus, int invoiceNo)
        {
            using (SqlConnection conn = PrepareSAPData.ConnectToSQL_v2())
            {
                if (conn == null) return;

                // Logic SQL:
                // Cố tình dùng CAST(@No AS Int) để đảm bảo so sánh số học chính xác trong điều kiện CASE
                // Nhưng khi GÁN (THEN @No), nó sẽ lấy chuỗi đã định dạng
                string sql = @"UPDATE BKAV_InvoiceHeader 
               SET InvoiceStatusID = @Status,
                   InvoiceNo = CASE WHEN CAST(@No AS Int) > 0 THEN @No ELSE InvoiceNo END,
                   SignedDate = GETDATE()
               WHERE InvoiceGUID = @Guid";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Status", newStatus);

                    // --- SỬA Ở ĐÂY ---
                    // Chuyển số int thành chuỗi có 7 số 0 ở đầu (Standard hóa đơn)
                    // Ví dụ: 2222 -> "00002222"
                    string strInvoiceNo = invoiceNo.ToString("00000000");
                    cmd.Parameters.AddWithValue("@No", strInvoiceNo);
                    // -----------------

                    cmd.Parameters.AddWithValue("@Guid", Guid.Parse(invoiceGuid));
                    cmd.ExecuteNonQuery();
                }
            }
        }
        public void DeleteInvoiceFromDb(string sapDocEntry, string invoiceGuidString)
        {
            using (SqlConnection conn = PrepareSAPData.ConnectToSQL_v2())
            {
                if (conn == null) return;

                // SQL Xóa: Ưu tiên xóa theo GUID nếu có, nếu không thì theo SAP_DocEntry
                string sql = @"
                DELETE FROM BKAV_InvoiceHeader 
                WHERE (InvoiceGUID = @Guid AND @Guid IS NOT NULL) 
                   OR (SAP_DocEntry = @DocEntry AND @DocEntry IS NOT NULL AND @DocEntry <> '0')";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    // 1. Tham số GUID
                    object paramGuidValue = DBNull.Value;
                    if (!string.IsNullOrEmpty(invoiceGuidString) && Guid.TryParse(invoiceGuidString, out Guid validGuid))
                    {
                        paramGuidValue = validGuid;
                    }
                    cmd.Parameters.Add("@Guid", System.Data.SqlDbType.UniqueIdentifier).Value = paramGuidValue;

                    // 2. Tham số DocEntry (Chuyển sang int nếu DB lưu int)
                    // Vì trong DB cột SAP_DocEntry là [int], nên phải ép kiểu
                    int docEntryInt = 0;
                    int.TryParse(sapDocEntry, out docEntryInt);

                    cmd.Parameters.AddWithValue("@DocEntry", docEntryInt);

                    // 3. Thực thi
                    int rows = cmd.ExecuteNonQuery();

                    // Debug: Nếu rows == 0 nghĩa là không tìm thấy để xóa
                    // Console.WriteLine($"Deleted rows: {rows}");
                }
            }
        }
    }
}
