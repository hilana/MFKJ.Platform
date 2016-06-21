using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MFKJ.IO
{
    public class FileServer
    {
        private static FileServer _fileServer = null;

        private readonly string _baseFolderPath = @"";

        private readonly string _serverIp = @"127.0.0.1";
        private readonly int _serverPort = 8974;
        public FileServer(string serverIp, string serverPort, string baseFolderPath)
        {
            _serverIp = string.IsNullOrEmpty(serverIp) ? _serverIp : serverIp;
            if(!int.TryParse(serverPort,out _serverPort))
                _serverPort= int.Parse(serverPort);
            _baseFolderPath = string.IsNullOrEmpty(baseFolderPath) ? _baseFolderPath : baseFolderPath;
            InitFileServer();
        }

        public static FileServer GetFileServer(string serverIp,string serverPort,string baseFolderPath)
        {
            if (_fileServer == null)
            {
                try
                {
                    _fileServer = new FileServer(serverIp, serverPort, baseFolderPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Cannot create repositroy object: " + e.Message);
                }
            }
            return _fileServer;
        }

        private void InitFileServer()
        {
            FileTransmiter.SupperReceive(new IPEndPoint(IPAddress.Parse(_serverIp), _serverPort), _baseFolderPath);
        }
    }
}
