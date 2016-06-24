using System;
using System.Threading.Tasks;
using Microsoft.Practices.Prism.Commands;

using MFKJ.IO;
using Microsoft.Practices.Prism.ViewModel;
using Microsoft.Win32;

namespace IOTest
{
    public class MainWindowViewModel : NotificationObject, IDisposable
    {
        private MainWindow _mainWindow;
        private FileServer _fileServer;
        private FileClient _fileClient;
        private bool _isDisposed;

        #region Data Members
        private string _selectFilePath = string.Empty;
        public string SelectFilePath
        {
            get { return _selectFilePath; }
            set
            {
                if (_selectFilePath == value) return;
                _selectFilePath = value;
                RaisePropertyChanged(nameof(SelectFilePath));
            }
        }

        private string _revMsg = string.Empty;
        public string RevMsg
        {
            get { return _revMsg; }
            set
            {
                if (_revMsg == value) return;
                _revMsg = value;
                RaisePropertyChanged(nameof(RevMsg));
            }
        }
        #endregion

        public MainWindowViewModel(MainWindow window)
        {
            _mainWindow = window;
            
            InitCommands();
        }

        #region Command
        public DelegateCommand<object> StartServerCommand { get; private set; }
        public DelegateCommand<object> SelectFileCommand { get; private set; }
        public DelegateCommand<object> UploadCommand { get; private set; }

        private void InitCommands()
        {
            StartServerCommand = new DelegateCommand<object>((o) =>
            {
                Task.Factory.StartNew(() =>
                {
                    _fileServer = FileServer.GetFileServer(@"127.0.0.1", "8888", @"E:/");
                    _fileServer.UploadREventHandler+= FileServerOnUploadREventHandler;
                });
            });

            SelectFileCommand = new DelegateCommand<object>((o) =>
            {
                OpenFileDialog dlg = new OpenFileDialog();
                bool? result = dlg.ShowDialog();
                if (result == false)
                    return;

                SelectFilePath = dlg.FileName;
            });

            UploadCommand = new DelegateCommand<object>((o) =>
            {
                Task.Factory.StartNew(() =>
                {
                    //_fileClient = new FileClient(@"127.0.0.1", "8888", SelectFilePath);
                    //_fileClient.SendFile(SelectFilePath);
                });
            });
        }

        private void FileServerOnUploadREventHandler(object sender, RevEventArg revEventArg)
        {
            RevMsg +=
                $@"Progress:{revEventArg.CurrentFileLength}/{revEventArg.CurrentFileLength},Speed:{revEventArg.SpeedKb},State;{revEventArg
                    .State}\r";
        }

        #endregion

        public void Dispose()
        {
            _isDisposed = true;
            if (_isDisposed)
            {

            }
        }
    }
}
