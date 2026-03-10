using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BKAV_Intergration
{
    internal class InvoiceData
    {
        public string ObjType { get; set; }
        public long DocEntry { get; set; }
        public string Sohoadon { get; set; }
        public string KyhieuHD { get; set; }
        public DateTime Ngaytao { get; set; }
        public DateTime Ngayky { get; set; }
        public string TrangThaihoadon { get; set; }
        public string MaCQT { get; set; }
    }
}
