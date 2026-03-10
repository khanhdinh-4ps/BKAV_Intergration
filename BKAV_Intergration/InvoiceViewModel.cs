using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BKAV_Intergration
{
    public class InvoiceViewModel
    {
        public bool Select { get; set; } = false;
        public int STT { get; set; }             // So thu tu
        public string SAP_DocEntry { get; set; }
        public string PartnerID { get; set; }      // PartnerInvoiceID
        public string SoHD { get; set; } // InvoiceNo
        public string TrangThai { get; set; }    // Status description
        public DateTime? NgayHD { get; set; }    // InvoiceDate
        public string KhachHang { get; set; }    // BuyerName
        public double TongTien { get; set; }     // Amount
        public string GhiChu { get; set; }       // MessLog (Lỗi nếu có)
        public string InvoiceGUID { get; set; }
        public string MauSo { get; set; }        // InvoiceForm
        public string KyHieu { get; set; }       // InvoiceSerial
    }
}
