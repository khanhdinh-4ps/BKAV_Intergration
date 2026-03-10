using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static BKAV_Intergration.Models;
using System.Configuration;

namespace BKAV_Intergration
{
    public class BkavService
    {
        private readonly string _serviceUrl;
        private readonly string _partnerGuid;
        private readonly string _partnerToken;
        private static readonly HttpClient _httpClient = new HttpClient();

        // Lấy thông tin từ Constants của bạn
        public BkavService()
        {
            // URL Demo lấy từ file Default.aspx.cs
            _serviceUrl = "https://wsdemo.ehoadon.vn/WSPublicEHoaDon.asmx/ExecCommand";
            _partnerGuid = Constants.BkavPartnerGUID;
            _partnerToken = Constants.BkavPartnerToken;
        }

        /// <summary>
        /// Hàm thay thế cho remoteCommand.TransferCommandAndProcessResult
        /// </summary>
        public async Task<Result> ExecCommandAsync(int cmdType, object commandObject)
        {
            try
            {
                // 1. Chuẩn bị gói tin CommandData
                var cmdDataObj = new
                {
                    CmdType = cmdType,
                    CommandObject = commandObject
                };

                // 2. Mã hóa dữ liệu
                string encryptedData = Encrypt(cmdDataObj, _partnerToken);

                // 3. Tạo payload JSON gửi đi (Giống trong file DemoPostToWS.html)
                var payload = new
                {
                    partnerGUID = _partnerGuid,
                    CommandData = encryptedData
                };

                // 4. Gửi Request POST
                var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_serviceUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP Error: {response.StatusCode}");
                }

                string responseString = await response.Content.ReadAsStringAsync();

                // 5. WebService trả về XML dạng <string>EncryptedData</string>, cần parse lấy nội dung bên trong
                // Vì gọi endpoint /ExecCommand dạng JSON, kết quả trả về thường là JSON string bọc trong XML hoặc JSON thuần tùy config server.
                // Dựa theo file HTML, nó trả về dạng JSON object d.

                // Xử lý sơ bộ nếu server trả về XML string (thường gặp với ASMX)
                string encryptedResult = ParseResponse(responseString);

                // 6. Giải mã kết quả
                Result result = Decrypt<Result>(encryptedResult, _partnerToken);
                return result;
            }
            catch (Exception ex)
            {
                return new Result { Status = 1, MessLog = "Lỗi Client: " + ex.Message };
            }
        }

        // --- CÁC HÀM XỬ LÝ MÃ HÓA (CORE) ---

        private string Encrypt(object obj, string token)
        {
            string json = JsonConvert.SerializeObject(obj);
            byte[] compressed = GzipCompress(json);
            var (key, iv) = ParseToken(token);
            byte[] encrypted = AesEncrypt(compressed, key, iv);
            return Convert.ToBase64String(encrypted);
        }

        private T Decrypt<T>(string base64Input, string token)
        {
            if (string.IsNullOrEmpty(base64Input)) return default;
            byte[] encrypted = Convert.FromBase64String(base64Input);
            var (key, iv) = ParseToken(token);
            byte[] decrypted = AesDecrypt(encrypted, key, iv);
            string json = GzipDecompress(decrypted);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private (byte[] Key, byte[] IV) ParseToken(string token)
        {
            if (string.IsNullOrEmpty(token) || !token.Contains(":"))
            {
                throw new Exception("PartnerToken không đúng định dạng Key:IV");
            }

            var parts = token.Split(':');
            // .Trim() giúp loại bỏ khoảng trắng vô tình copy phải
            string keyBase64 = parts[0].Trim();
            string ivBase64 = parts[1].Trim();

            return (Convert.FromBase64String(keyBase64), Convert.FromBase64String(ivBase64));
        }

        private byte[] GzipCompress(string data)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            using (var ms = new MemoryStream())
            {
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zip.Write(buffer, 0, buffer.Length);
                }
                return ms.ToArray();
            }
        }

        private string GzipDecompress(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var zip = new GZipStream(ms, CompressionMode.Decompress))
            using (var reader = new StreamReader(zip, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private byte[] AesEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var encryptor = aes.CreateEncryptor())
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        private byte[] AesDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var decryptor = aes.CreateDecryptor())
                    return decryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        private string ParseResponse(string rawResponse)
        {
            try
            {
                // Trường hợp 1: Server trả về XML chuẩn (<string>...</string>)
                if (rawResponse.Contains("<string") && rawResponse.Contains(">"))
                {
                    var doc = System.Xml.Linq.XDocument.Parse(rawResponse);
                    // Lấy giá trị bên trong thẻ và Trim() sạch sẽ
                    return doc.Root.Value.Trim();
                }

                // Trường hợp 2: Server trả về JSON ({"d":"..."})
                if (rawResponse.Trim().StartsWith("{"))
                {
                    dynamic json = JsonConvert.DeserializeObject(rawResponse);
                    string data = json.d ?? json.result;
                    return data?.Trim();
                }

                // Trường hợp 3: Trả về chuỗi thô, chỉ cần Trim
                return rawResponse.Trim();
            }
            catch
            {
                // Nếu parse lỗi, trả về nguyên gốc để debug
                return rawResponse;
            }
        }
    }

    // Class chứa kết quả trả về tổng quát (Tương tự class Result trong BSECUS cũ)
    public class Result
    {
        /// <summary>
        /// 0: Thành công, Khác 0: Lỗi
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Dữ liệu trả về (Json String hoặc Object)
        /// </summary>
        public object Object { get; set; }

        /// <summary>
        /// Nội dung lỗi (nếu có)
        /// </summary>
        public string MessLog { get; set; }

        // Các trường phụ (có thể có hoặc không tùy version API)
        public string Code { get; set; }
        public bool isOk { get; set; }
        public bool isError { get; set; }
    }
}