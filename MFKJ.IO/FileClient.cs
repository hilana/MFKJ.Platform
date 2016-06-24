using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MFKJ.IO
{
    public class FileClient
    {
        private readonly string _serverIp = @"127.0.0.1";
        private readonly string _serverPort = @"8974";
        private readonly string _baseFolderPath = @"";

        public FileClient(string serverIp, string serverPort, string baseFolderPath)
        {
            _serverIp = string.IsNullOrEmpty(serverIp) ? _serverIp : serverIp;
            _serverPort = string.IsNullOrEmpty(serverPort) ? _serverIp : serverPort;
            _baseFolderPath = string.IsNullOrEmpty(baseFolderPath) ? _baseFolderPath : baseFolderPath;
        }

        #region Upload
        /// <summary>
        /// 上传单一文件
        /// </summary>
        public void SendFile(string filePath)
        {
            try
            {
                FileTransmiter.SupperSend(new IPEndPoint(IPAddress.Parse(_serverIp), Convert.ToInt32(_serverPort)),
                    string.IsNullOrEmpty(filePath) ? _baseFolderPath : filePath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// 上传多文件或文件夹
        /// </summary>
        public void SendFiles()
        {
            try
            {
                FileTransmiter.SupperSend(new IPEndPoint(IPAddress.Parse(_serverIp), Convert.ToInt32(_serverPort)), _baseFolderPath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        #endregion

        #region Download
        #endregion
    }
}
