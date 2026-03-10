using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BKAV_Intergration
{
    public static class SAPHelper
    {
        public static Company ConnectToSAP()
        {
            Company oCompany = new Company();
            try
            {
                // 1. Đọc config
                string sServer = ConfigurationManager.AppSettings["SAPServer"];
                string sDb = ConfigurationManager.AppSettings["SAPDB"];
                string sUser = ConfigurationManager.AppSettings["SAPUser"];
                string sPass = ConfigurationManager.AppSettings["SAPPass"];
                string sDbUser = ConfigurationManager.AppSettings["DBUser"];
                string sDbPass = ConfigurationManager.AppSettings["DBPass"];
                string sLicense = ConfigurationManager.AppSettings["SAP_LicenseServer"];

                // Kiểm tra xem có đọc được không
                if (string.IsNullOrEmpty(sServer) || string.IsNullOrEmpty(sUser))
                {
                    throw new Exception("Không đọc được file cấu hình App.config. Vui lòng kiểm tra lại Key/Value.");
                }

                // 2. Gán vào Company Object
                oCompany.Server = sServer;
                oCompany.CompanyDB = sDb;
                oCompany.UserName = sUser;
                oCompany.Password = sPass;
                oCompany.DbUserName = sDbUser;
                oCompany.DbPassword = sDbPass;
                oCompany.LicenseServer = sLicense; // Phải có dạng IP:Port (VD: 192.168.1.1:30000)

                // 3. Chọn Version SQL
                // Nếu SQL server của bạn là 2019, hãy đổi dòng dưới thành dst_MSSQL2019
                oCompany.DbServerType = BoDataServerTypes.dst_MSSQL2017;

                // 4. Kết nối
                int ret = oCompany.Connect();

                if (ret != 0)
                {
                    oCompany.GetLastError(out int errCode, out string errMsg);
                    throw new Exception($"SAP Connect Error {errCode}: {errMsg}");
                }

                return oCompany;
            }
            catch (Exception ex)
            {
                if (oCompany != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(oCompany);
                throw;
            }
        }
    }
}
