#define Sleep
//#undef Sleep
//#define TransmitLog
#undef TransmitLog
//#define BreakpointLog
#undef BreakpointLog
using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace MFKJ.IO
{
    public static class FileTransmiter
    {
        #region NestedType
        private class SendWorker : IWorker
        {
            private long totalSent, totalSend;
            private byte[] buffer;
            private Socket sock;
            private FileStream reader;
            private Thread thread;
            private bool isFinished;

            public long TotalSent
            {
                get { return totalSent; }
            }
            public long TotalSend
            {
                get { return totalSend; }
            }
            public byte[] Buffer
            {
                get { return buffer; }
            }
            public Socket Client
            {
                get { return sock; }
            }
            public bool IsFinished
            {
                get { return isFinished; }
            }

            public SendWorker(IPEndPoint ip)
            {
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(ip);
                buffer = new byte[BufferSize];
            }
            public void Initialize(string path, long position, long length)
            {
                Initialize(path, position, length, 0L, length);
            }
            public void Initialize(string path, long position, long length, long worked, long total)
            {
                reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader.Position = position + worked;
                totalSent = worked;
                totalSend = total;
                thread = new Thread(new ParameterizedThreadStart(Work));
                thread.IsBackground = true;
#if TransmitLog
                thread.Name = position.ToString() + length.ToString();
                AppendTransmitLog(LogType.Transmit, thread.Name + " Initialized:" + totalSent + "/" + totalSend + ".");
#endif
            }
            private void Work(object obj)
            {
                int read, sent;
                bool flag;
                while (totalSent < totalSend)
                {
                    read = reader.Read(buffer, 0, Math.Min(BufferSize, (int)(totalSend - totalSent)));
                    sent = 0;
                    flag = true;
                    while ((sent += sock.Send(buffer, sent, read, SocketFlags.None)) < read)
                    {
                        flag = false;
                        totalSent += (long)sent;
#if TransmitLog
                        AppendTransmitLog(LogType.Transmit, thread.Name + ":" + totalSent + "/" + totalSend + ".");
#endif
#if Sleep
                        Thread.Sleep(200);
#endif
                    }
                    if (flag)
                    {
                        totalSent += (long)read;
#if TransmitLog
                        AppendTransmitLog(LogType.Transmit, thread.Name + ":" + totalSent + "/" + totalSend + ".");
#endif
#if Sleep
                        Thread.Sleep(200);
#endif
                    }
                }
                reader.Dispose();
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();
                EventWaitHandle waitHandle = obj as EventWaitHandle;
                if (waitHandle != null)
                {
                    waitHandle.Set();
                }
                isFinished = true;
            }

            public void ReportProgress(out long worked, out long total)
            {
                worked = totalSent;
                total = totalSend;
            }

            public void RunWork(EventWaitHandle waitHandle)
            {
                thread.Start(waitHandle);
            }
        }

        private class ReceiveWorker : IWorker
        {
            private long offset, totalReceived, totalReceive;
            private byte[] buffer;
            private Socket sock;
            private FileStream writer;
            private Thread thread;
            private bool isFinished;

            public long TotalReceived
            {
                get { return totalReceived; }
            }
            public long TotalReceive
            {
                get { return totalReceive; }
            }
            public byte[] Buffer
            {
                get { return buffer; }
            }
            public Socket Client
            {
                get { return sock; }
            }
            public bool IsFinished
            {
                get { return isFinished; }
            }

            public ReceiveWorker(Socket client)
            {
                sock = client;
                buffer = new byte[BufferSize];
            }
            public void Initialize(string path, long position, long length)
            {
                Initialize(path, position, length, 0L, length);
            }
            public void Initialize(string path, long position, long length, long worked, long total)
            {
                writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Write);
                writer.Position = position + worked;
                writer.Lock(position, length);
                offset = position;
                totalReceived = worked;
                totalReceive = total;
                thread = new Thread(new ParameterizedThreadStart(Work));
                thread.IsBackground = true;
#if TransmitLog
                thread.Name = position.ToString() + length.ToString();
                AppendTransmitLog(LogType.Transmit, thread.Name + " Initialized:" + totalReceived + "/" + totalReceive + ".");
#endif
            }
            private void Work(object obj)
            {
                int received;
                while (totalReceived < totalReceive)
                {
                    if ((received = sock.Receive(buffer)) == 0)
                    {
                        break;
                    }
                    writer.Write(buffer, 0, received);
                    writer.Flush();
                    totalReceived += (long)received;
#if TransmitLog
                    AppendTransmitLog(LogType.Transmit, thread.Name + ":" + totalReceived + "/" + totalReceive + ".");
#endif
#if Sleep
                    Thread.Sleep(200);
#endif
                }
                writer.Unlock(offset, totalReceive);
                writer.Dispose();
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();
                EventWaitHandle waitHandle = obj as EventWaitHandle;
                if (waitHandle != null)
                {
                    waitHandle.Set();
                }
                isFinished = true;
            }

            public void ReportProgress(out long worked, out long total)
            {
                worked = totalReceived;
                total = totalReceive;
            }

            public void RunWork(EventWaitHandle waitHandle)
            {
                thread.Start(waitHandle);
            }
        }

        private interface IWorker
        {
            bool IsFinished { get; }
            void Initialize(string path, long position, long length);
            void Initialize(string path, long position, long length, long worked, long total);
            void ReportProgress(out long worked, out long total);
            void RunWork(EventWaitHandle waitHandle);
        }
        #endregion

        #region Field
        public const int BufferSize = 1024;
        public const int PerLongCount = sizeof(long);
        public const int MinThreadCount = 1;
        public const int MaxThreadCount = 9;
        public const string PointExtension = ".dat";
        public const string TempExtension = ".temp";
        private const long SplitSize = 1024L * 1024L * 100L;
        public static readonly IPEndPoint TestIP;
#if TransmitLog
        private static StreamWriter transmitLoger;
#endif
#if BreakpointLog
        private static StreamWriter breakpointLoger;
#endif
        #endregion

        #region Constructor
        static FileTransmiter()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            TestIP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 520);
#if TransmitLog
            transmitLoger = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transmit.log"), true, Encoding.Default);
#endif
#if BreakpointLog
            breakpointLoger = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "breakpoint.log"), true, Encoding.Default);
#endif
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            StreamWriter writer = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exec.log"), true, Encoding.Default);
            writer.Write("Time:");
            writer.Write(DateTime.Now.ToShortTimeString());
            writer.Write(". ");
            writer.WriteLine(e.ExceptionObject);
            writer.Dispose();
        }

        #region Log
#if TransmitLog || BreakpointLog
        public enum LogType
        {
            Transmit,
            Breakpoint
        }

        public static void AppendTransmitLog(LogType type, string msg)
        {
            switch (type)
            {
                case LogType.Transmit:
#if TransmitLog
                    transmitLoger.Write(DateTime.Now.ToShortTimeString());
                    transmitLoger.Write('\t');
                    transmitLoger.WriteLine(msg);
                    transmitLoger.Flush();
#endif
                    break;
                case LogType.Breakpoint:
#if BreakpointLog
                    breakpointLoger.Write(DateTime.Now.ToShortTimeString());
                    breakpointLoger.Write('\t');
                    breakpointLoger.WriteLine(msg);
                    breakpointLoger.Flush();
#endif
                    break;
            }
        }
#endif
        #endregion
        #endregion

        #region Single
        public static void Send(IPEndPoint ip, string path)
        {
            Stopwatch watcher = new Stopwatch();
            watcher.Start();
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Connect(ip);
            byte[] buffer = new byte[BufferSize];
            using (FileStream reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                long send, length = reader.Length;
                Buffer.BlockCopy(BitConverter.GetBytes(length), 0, buffer, 0, PerLongCount);
                string fileName = Path.GetFileName(path);
                sock.Send(buffer, 0, PerLongCount + Encoding.Default.GetBytes(fileName, 0, fileName.Length, buffer, PerLongCount), SocketFlags.None);
                Console.WriteLine("Sending file:" + fileName + ".Plz wait...");
                sock.Receive(buffer);
                reader.Position = send = BitConverter.ToInt64(buffer, 0);
#if BreakpointLog
                Console.WriteLine("Breakpoint " + reader.Position);
#endif
                int read, sent;
                bool flag;
                while ((read = reader.Read(buffer, 0, BufferSize)) != 0)
                {
                    sent = 0;
                    flag = true;
                    while ((sent += sock.Send(buffer, sent, read, SocketFlags.None)) < read)
                    {
                        flag = false;
                        send += (long)sent;
#if TransmitLog
                        Console.WriteLine("Sent " + send + "/" + length + ".");
#endif
#if Sleep
                        Thread.Sleep(200);
#endif
                    }
                    if (flag)
                    {
                        send += (long)read;
#if TransmitLog
                        Console.WriteLine("Sent " + send + "/" + length + ".");
#endif
#if Sleep
                        Thread.Sleep(200);
#endif
                    }
                }
            }
            sock.Shutdown(SocketShutdown.Both);
            sock.Close();
            watcher.Stop();
            Console.WriteLine("Send finish.Span Time:" + watcher.Elapsed.TotalMilliseconds + " ms.");
        }

        public static void Receive(IPEndPoint ip, string path)
        {
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(ip);
            listener.Listen(MinThreadCount);
            Socket client = listener.Accept();
            Stopwatch watcher = new Stopwatch();
            watcher.Start();
            byte[] buffer = new byte[BufferSize];
            int received = client.Receive(buffer);
            long receive, length = BitConverter.ToInt64(buffer, 0);
            string fileName = Encoding.Default.GetString(buffer, PerLongCount, received - PerLongCount);
            Console.WriteLine("Receiveing file:" + fileName + ".Plz wait...");
            FileInfo file = new FileInfo(Path.Combine(path, fileName));
            using (FileStream writer = file.Open(file.Exists ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                receive = writer.Length;
                client.Send(BitConverter.GetBytes(receive));
#if BreakpointLog
                Console.WriteLine("Breakpoint " + receive);
#endif
                while (receive < length)
                {
                    if ((received = client.Receive(buffer)) == 0)
                    {
                        Console.WriteLine("Send Stop.");
                        return;
                    }
                    writer.Write(buffer, 0, received);
                    writer.Flush();
                    receive += (long)received;
#if TransmitLog
                    Console.WriteLine("Received " + receive + "/" + length + ".");
#endif
#if Sleep
                    Thread.Sleep(200);
#endif
                }
            }
            client.Shutdown(SocketShutdown.Both);
            client.Close();
            watcher.Stop();
            Console.WriteLine("Receive finish.Span Time:" + watcher.Elapsed.TotalMilliseconds + " ms.");
        }
        #endregion

        #region Supper
        #region Extensions
        private static int ReportProgress(this IWorker[] workers, out long worked, out long total)
        {
            worked = total = 0L;
            long w, t;
            foreach (IWorker worker in workers)
            {
                worker.ReportProgress(out w, out t);
                worked += w;
                total += t;
            }
            return (int)(worked / total) * 100;
        }
        private static int ReportSpeed(this IWorker[] workers, ref long oldValue)
        {
            long w, t;
            workers.ReportProgress(out w, out t);
            int speed = (int)((w - oldValue) / 8L);
            oldValue = w;
            return speed;
        }
        private static bool IsAllFinished(this IWorker[] workers)
        {
            bool flag = true;
            foreach (IWorker worker in workers)
            {
                if (!worker.IsFinished)
                {
                    flag = false;
                    break;
                }
            }
            return flag;
        }
        #endregion

        #region Helper
        public static void Write(long value, byte[] buffer, int offset)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 0x10);
            buffer[offset++] = (byte)(value >> 0x18);
            buffer[offset++] = (byte)(value >> 0x20);
            buffer[offset++] = (byte)(value >> 40);
            buffer[offset++] = (byte)(value >> 0x30);
            buffer[offset] = (byte)(value >> 0x38);
        }
        public static void Read(out long value, byte[] buffer, int offset)
        {
            uint num = (uint)(((buffer[offset++] | (buffer[offset++] << 8)) | (buffer[offset++] << 0x10)) | (buffer[offset++] << 0x18));
            uint num2 = (uint)(((buffer[offset++] | (buffer[offset++] << 8)) | (buffer[offset++] << 0x10)) | (buffer[offset] << 0x18));
            value = (long)((num2 << 0x20) | num);
        }
        #endregion

        public static int GetThreadCount(long fileSize)
        {
            int count = (int)(fileSize / SplitSize);
            if (count < MinThreadCount)
            {
                count = MinThreadCount;
            }
            else if (count > MaxThreadCount)
            {
                count = MaxThreadCount;
            }
            return count;
        }

        public static void SupperSend(IPEndPoint ip, string path)
        {
            Stopwatch watcher = new Stopwatch();
            watcher.Start();
            FileInfo file = new FileInfo(path);
#if DEBUG
            if (!file.Exists)
            {
                throw new FileNotFoundException();
            }
#endif
            SendWorker worker = new SendWorker(ip);
            long fileLength = file.Length;
            Buffer.BlockCopy(BitConverter.GetBytes(fileLength), 0, worker.Buffer, 0, PerLongCount);
            string fileName = file.Name;
            worker.Client.Send(worker.Buffer, 0, PerLongCount + Encoding.Default.GetBytes(fileName, 0, fileName.Length, worker.Buffer, PerLongCount), SocketFlags.None);
            Console.WriteLine("Sending file:" + fileName + ".Plz wait...");
            int threadCount = GetThreadCount(fileLength);
            SendWorker[] workers = new SendWorker[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                workers[i] = i == 0 ? worker : new SendWorker(ip);
            }
            #region Breakpoint
            int perPairCount = PerLongCount * 2, count = perPairCount * threadCount;
            byte[] bufferInfo = new byte[count];
            long oddSize, avgSize = Math.DivRem(fileLength, (long)threadCount, out oddSize);
            if (worker.Client.Receive(bufferInfo) == 4)
            {
                for (int i = 0; i < threadCount; i++)
                {
                    workers[i].Initialize(path, i * avgSize, i == threadCount - 1 ? avgSize + oddSize : avgSize);
                }
            }
            else
            {
                long w, t;
                for (int i = 0; i < threadCount; i++)
                {
                    Read(out w, bufferInfo, i * perPairCount);
                    Read(out t, bufferInfo, i * perPairCount + PerLongCount);
                    workers[i].Initialize(path, i * avgSize, i == threadCount - 1 ? avgSize + oddSize : avgSize, w, t);
#if BreakpointLog
                    AppendTransmitLog(LogType.Breakpoint, i + " read:" + w + "/" + t + ".");
#endif
                }
            }
            Thread.Sleep(200);
            #endregion
            AutoResetEvent reset = new AutoResetEvent(true);
            for (int i = 0; i < threadCount; i++)
            {
                workers[i].RunWork(i == threadCount - 1 ? reset : null);
            }
            reset.WaitOne();
            #region Breakpoint
            int speed;
            long value = 0L;
            do
            {
                speed = workers.ReportSpeed(ref value);
                Console.WriteLine("waiting for other threads. Progress:" + value + "/" + fileLength + ";Speed:" + speed + "kb/s.");
                Thread.Sleep(1000);
            }
            while (!workers.IsAllFinished());
            speed = workers.ReportSpeed(ref value);
            Console.WriteLine("waiting for other threads. Progress:" + value + "/" + fileLength + ";Speed:" + speed + "kb/s.");
            #endregion
            watcher.Stop();
            Console.WriteLine("Send finish.Span Time:" + watcher.Elapsed.TotalMilliseconds + " ms.");
        }

        public static void SupperReceive(IPEndPoint ip, string path)
        {
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(ip);
            listener.Listen(MaxThreadCount);
            ReceiveWorker worker = new ReceiveWorker(listener.Accept());
            Stopwatch watcher = new Stopwatch();
            watcher.Start();
            int recv = worker.Client.Receive(worker.Buffer);
            long fileLength = BitConverter.ToInt64(worker.Buffer, 0);
            string fileName = Encoding.Default.GetString(worker.Buffer, PerLongCount, recv - PerLongCount);
            Console.WriteLine("Receiveing file:" + fileName + ".Plz wait...");
            int threadCount = GetThreadCount(fileLength);
            ReceiveWorker[] workers = new ReceiveWorker[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                workers[i] = i == 0 ? worker : new ReceiveWorker(listener.Accept());
            }
            #region Breakpoint
            int perPairCount = PerLongCount * 2, count = perPairCount * threadCount;
            byte[] bufferInfo = new byte[count];
            string filePath = Path.Combine(path, fileName), pointFilePath = Path.ChangeExtension(filePath, PointExtension), tempFilePath = Path.ChangeExtension(filePath, TempExtension);
            FileStream pointStream;
            long oddSize, avgSize = Math.DivRem(fileLength, (long)threadCount, out oddSize);
            if (File.Exists(pointFilePath) && File.Exists(tempFilePath))
            {
                pointStream = new FileStream(pointFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                pointStream.Read(bufferInfo, 0, count);
                long w, t;
                for (int i = 0; i < threadCount; i++)
                {
                    Read(out w, bufferInfo, i * perPairCount);
                    Read(out t, bufferInfo, i * perPairCount + PerLongCount);
                    workers[i].Initialize(tempFilePath, i * avgSize, i == threadCount - 1 ? avgSize + oddSize : avgSize, w, t);
#if BreakpointLog
                    AppendTransmitLog(LogType.Breakpoint, i + " read:" + w + "/" + t + ".");
#endif
                }
                worker.Client.Send(bufferInfo);
            }
            else
            {
                pointStream = new FileStream(pointFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                FileStream stream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Write);
                stream.SetLength(fileLength);
                stream.Flush();
                stream.Dispose();
                for (int i = 0; i < threadCount; i++)
                {
                    workers[i].Initialize(tempFilePath, i * avgSize, i == threadCount - 1 ? avgSize + oddSize : avgSize);
                }
                worker.Client.Send(bufferInfo, 0, 4, SocketFlags.None);
            }
            Timer timer = new Timer(state =>
            {
                long w, t;
                for (int i = 0; i < threadCount; i++)
                {
                    workers[i].ReportProgress(out w, out t);
                    Write(w, bufferInfo, i * perPairCount);
                    Write(t, bufferInfo, i * perPairCount + PerLongCount);
#if BreakpointLog
                    AppendTransmitLog(LogType.Breakpoint, i + " write:" + w + "/" + t + ".");
#endif
                }
                pointStream.Position = 0L;
                pointStream.Write(bufferInfo, 0, count);
                pointStream.Flush();

            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            #endregion
            AutoResetEvent reset = new AutoResetEvent(true);
            for (int i = 0; i < threadCount; i++)
            {
                workers[i].RunWork(i == threadCount - 1 ? reset : null);
            }
            reset.WaitOne();
            #region Breakpoint
            int speed;
            long value = 0L;
            do
            {
                speed = workers.ReportSpeed(ref value);
                Console.WriteLine("waiting for other threads. Progress:" + value + "/" + fileLength + ";Speed:" + speed + "kb/s.");
                Thread.Sleep(1000);
            }
            while (!workers.IsAllFinished());
            speed = workers.ReportSpeed(ref value);
            Console.WriteLine("waiting for other threads. Progress:" + value + "/" + fileLength + ";Speed:" + speed + "kb/s.");
            timer.Dispose();
            pointStream.Dispose();
            File.Delete(pointFilePath);
            File.Move(tempFilePath, filePath);
            #endregion
            watcher.Stop();
            Console.WriteLine("Receive finish.Span Time:" + watcher.Elapsed.TotalMilliseconds + " ms.");
        }
        #endregion
    }
}