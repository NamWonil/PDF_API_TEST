using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly string _pdfDirectory;

        public PdfController()
        {
            _pdfDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ReceivedPdfs");
            if (!Directory.Exists(_pdfDirectory))
                Directory.CreateDirectory(_pdfDirectory);
        }

        // 原有的multipart/form-data上传接口
        [HttpPost("upload")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UploadPdf([FromForm] IFormFile file)
        {
            try
            {
                // 记录请求的详细信息
                Console.WriteLine($"=== Multipart请求详细信息 ===");
                Console.WriteLine($"Content-Type: {Request.ContentType}");
                Console.WriteLine($"Content-Length: {Request.ContentLength}");
                Console.WriteLine($"Method: {Request.Method}");

                // 检查文件
                if (file == null || file.Length == 0)
                {
                    Console.WriteLine("❌ 未收到文件");
                    return Ok(new
                    {
                        IsResult = false,
                        ErrorMsg = "未收到文件"
                    });
                }

                Console.WriteLine($"✅ 收到文件: {file.FileName}, 大小: {file.Length} bytes");

                // 检查表单字段
                var form = Request.Form;
                Console.WriteLine($"表单字段数量: {form.Count}");

                foreach (var field in form)
                {
                    Console.WriteLine($"字段 [{field.Key}] = [{string.Join(",", field.Value)}]");
                }

                var fileSize = form["fileSize"].FirstOrDefault();
                var uploadTime = form["uploadTime"].FirstOrDefault();

                Console.WriteLine($"fileSize: {fileSize}");
                Console.WriteLine($"uploadTime: {uploadTime}");
                Console.WriteLine($"=====================");

                if (Path.GetExtension(file.FileName).ToLower() != ".pdf")
                {
                    return Ok(new
                    {
                        IsResult = false,
                        ErrorMsg = "只支持PDF文件"
                    });
                }

                if (string.IsNullOrEmpty(fileSize) || string.IsNullOrEmpty(uploadTime))
                {
                    return Ok(new
                    {
                        IsResult = false,
                        ErrorMsg = $"缺少表单字段 - fileSize: {!string.IsNullOrEmpty(fileSize)}, uploadTime: {!string.IsNullOrEmpty(uploadTime)}"
                    });
                }

                // 获取桌面路径
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(desktopPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok(new
                {
                    IsResult = true,
                    ErrorMsg = "",
                    ReceivedFileSize = fileSize,
                    ReceivedUploadTime = uploadTime,
                    ActualFileSize = file.Length,
                    ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 服务器异常: {ex}");
                return Ok(new
                {
                    IsResult = false,
                    ErrorMsg = $"处理失败: {ex.Message}"
                });
            }
        }

        // 新的Base64上传接口
        [HttpPost("upload-base64")]
        [Consumes("application/json")]
        public async Task<IActionResult> UploadPdfBase64([FromBody] Base64PdfRequest requestData)
        {
            try
            {
                // 记录请求的详细信息
                Console.WriteLine($"=== Base64 JSON请求详细信息 ===");
                Console.WriteLine($"Content-Type: {Request.ContentType}");
                Console.WriteLine($"Content-Length: {Request.ContentLength}");
                Console.WriteLine($"Method: {Request.Method}");

                if (requestData == null)
                {
                    Console.WriteLine("❌ 请求数据为空");
                    return Ok(new
                    {
                        IsResult = false,
                        ErrorMsg = "请求数据为空"
                    });
                }

                Console.WriteLine($"✅ 收到Base64请求");
                Console.WriteLine($"ID: {requestData.UserID}");
                Console.WriteLine($"测试事件: {requestData.DateTimes}");
                Console.WriteLine($"文件大小: {requestData.FileSize}");
                Console.WriteLine($"上传时间: {requestData.UploadTime}");
                Console.WriteLine($"Base64数据长度: {requestData.resultSheetPDF?.Length ?? 0}");

                // 验证必要字段
                if (string.IsNullOrEmpty(requestData.resultSheetPDF))
                {
                    Console.WriteLine("❌ Base64数据为空");
                    return Ok(new
                    {
                        IsResult = false,
                        ErrorMsg = "Base64数据为空"
                    });
                }



                // 解码Base64数据
                byte[] pdfBytes;
                try
                {
                    // 清理Base64数据（去除可能的空格和换行）
                    string cleanBase64 = requestData.resultSheetPDF.Trim()
                        .Replace("\n", "")
                        .Replace("\r", "")
                        .Replace(" ", "");

                    pdfBytes = Convert.FromBase64String(cleanBase64);
                    Console.WriteLine($"✅ Base64解码成功，字节数: {pdfBytes.Length}");
                }
                catch (FormatException ex)
                {
                    Console.WriteLine($"❌ Base64格式错误: {ex.Message}");
                    return Ok(new
                    {
                        IsResult = false,
                        ErrorMsg = "Base64数据格式错误"
                    });
                }

                // 验证是否为有效的PDF文件
                if (!IsValidPdf(pdfBytes))
                {
                    Console.WriteLine("❌ 不是有效的PDF文件");
                    return Ok(new
                    {
                        IsResult = false,
                        ErrorMsg = "不是有效的PDF文件"
                    });
                }

                // 验证文件大小
                if (!string.IsNullOrEmpty(requestData.FileSize))
                {
                    if (long.TryParse(requestData.FileSize, out long declaredSize))
                    {
                        Console.WriteLine($"声明大小: {declaredSize}, 实际大小: {pdfBytes.Length}");
                        if (declaredSize != pdfBytes.Length)
                        {
                            Console.WriteLine($"⚠️ 文件大小不匹配");
                        }
                    }
                }

                //// 保存文件到桌面
                //var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                //var fileName = $"{requestData.UserID}_{requestData.DateTimes}.pdf";
                //var filePath = Path.Combine(desktopPath, fileName);

                //await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);
                //Console.WriteLine($"💾 文件保存成功: {filePath}");

                //// 同时保存到应用目录
                //var appFilePath = Path.Combine(_pdfDirectory, fileName);
                //await System.IO.File.WriteAllBytesAsync(appFilePath, pdfBytes);

                return Ok(new
                {
                    IsResult = true,
                    ErrorMsg = "",
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 服务器异常: {ex}");
                return Ok(new
                {
                    IsResult = false,
                    ErrorMsg = $"处理失败: {ex.Message}"
                });
            }
        }

        // 验证PDF文件格式
        private bool IsValidPdf(byte[] data)
        {
            // PDF文件以 "%PDF-" 开头（前5个字节：25 50 44 46 2D）
            if (data.Length >= 5)
            {
                return data[0] == 0x25 && // %
                       data[1] == 0x50 && // P
                       data[2] == 0x44 && // D
                       data[3] == 0x46 && // F
                       data[4] == 0x2D;   // -
            }
            return false;
        }

        [HttpPost("upload-any-json")]
        [Consumes("application/json")]
        public async Task<IActionResult> UploadAnyJson()
        {
            try
            {
                // 读取原始body
                Request.EnableBuffering();
                string rawBody = await new StreamReader(Request.Body).ReadToEndAsync();
                Request.Body.Position = 0;

                Console.WriteLine($"📦 收到任意JSON数据:");
                Console.WriteLine(rawBody);

                //// 什么都不解析，直接保存原始数据
                //var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                //var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_received_data.json";
                //var filePath = Path.Combine(desktopPath, fileName);

                //await System.IO.File.WriteAllTextAsync(filePath, rawBody, Encoding.UTF8);

                return Ok(new
                {
                    IsResult = true,
                    ErrorMsg = "",
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    IsResult = false,
                    ErrorMsg = ex.Message
                });
            }
        }
    }

    // Base64 PDF请求模型
    public class Base64PdfRequest
    {
        public string UserID { get; set; }

        public string DateTimes { get; set; }
        public string FileSize { get; set; }
        public string UploadTime { get; set; }
        public string resultSheetPDF { get; set; }
    }
}