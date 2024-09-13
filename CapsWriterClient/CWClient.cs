using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using System.Net.WebSockets;
using System.Collections.Concurrent;

using Newtonsoft.Json;


// 转换为Python的时间戳
public static class PYTime
{
    public static double Time()
    {
        DateTime now = DateTime.UtcNow;
        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        double pytimevalue = (now - epoch).TotalMilliseconds / 1000.0f;

        return pytimevalue;
    }
}

// CapsWriterMessage 语音数据
public class CWM_Audio : ICloneable
{
    public Guid task_id { get; set; }
    public int seg_duration { get; set; }
    public int seg_overlap { get; set; }
    public bool is_final { get; set; }
    public double time_start { get; set; }
    public double time_frame { get; set; }
    public string source { get; set; }
    public string data { get; set; }

    public object Clone()
    {
        return (CWM_Audio)this.MemberwiseClone();
    }
}

// CapsWriterMessage 识别结果数据
public class CWM_Result
{
    public Guid task_id { get; set; }
    public float duration { get; set; }
    public double time_start { get; set; }
    public double time_submit { get; set; }
    public double time_complete { get; set; }
    public string[] tokens { get; set; }
    public double[] timestamps { get; set; }
    public string text { get; set; }
    public bool is_final { get; set; }
}

public class CWClient: IDisposable
{
    public const int mic_seg_duration = 15;  // 麦克风听写时分段长度：15秒
    public const int mic_seg_overlap = 2;    // 麦克风听写时分段重叠：2秒

    public string ServerAddress { get; set; }  // CapsWriter Server 地址
    public ushort ServerPort { get; set; }     // CapsWriter Server 端口

    

    public bool IsConnected
    {
        get { return clientWebSocket?.State == WebSocketState.Open; }
    }

    private ClientWebSocket clientWebSocket;
    private bool disposedValue;


    private static CancellationTokenSource cts;
    private static BlockingCollection<CWM_Audio> collection = new BlockingCollection<CWM_Audio>();  // 队列消息


    public CWClient(string ServerAddress, ushort ServerPort)
    {
        this.ServerAddress = ServerAddress;
        this.ServerPort = ServerPort;

        clientWebSocket = new ClientWebSocket();
        cts = new CancellationTokenSource();
    }

    #region Dispose
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: 释放托管状态(托管对象)
                clientWebSocket.Dispose();
            }

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~CWClient()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

    void IDisposable.Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region OnConnected
    public delegate void ConnectedHandler();
    public event ConnectedHandler OnConnected;
    protected virtual void Connected_Handle()
    {
        OnConnected?.Invoke();
    }
    #endregion

    #region OnDisconnected
    public delegate void DisconnectedHandler();
    public event DisconnectedHandler OnDisconnected;
    protected virtual void Disconnected_Handle()
    {
        OnDisconnected?.Invoke();
    }
    #endregion

    #region OnReceiveResult
    public class ResultEventArgs : EventArgs
    {
        private string result_json;

        public ResultEventArgs(string ResultJSON)
        {
            this.result_json = ResultJSON;
        }

        public string ResultJSON
        {
            get { return result_json; }
        }
    }


    public delegate void ReceiveResultHandler(object sender, ResultEventArgs e);
    public event ReceiveResultHandler OnReceiveResult;
    protected virtual void ReceiveResult_Handle(ResultEventArgs e)
    {
        OnReceiveResult?.Invoke(this, e);
    }
    #endregion


    public async void Connect()
    {
        try
        {
            await clientWebSocket.ConnectAsync(new Uri($"ws://{ServerAddress}:{ServerPort}/"), CancellationToken.None);

            Connected_Handle();

            // 启动发送任务
            _ = Task.Run(() => SendWebSocketMsg(clientWebSocket), cts.Token);

            while (true)
            {
                byte[] buffer = new byte[16384];
                WebSocketReceiveResult result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                while (!result.EndOfMessage)
                {
                    // 处理接收到的数据

                    // 继续接收剩余的数据
                    result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }

                ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, result.Count);
                byte[] receivedByte = segment.Array.Take(segment.Count).ToArray();
                string receiveString = Encoding.UTF8.GetString(receivedByte);

                ResultEventArgs e = new ResultEventArgs(receiveString);
                ReceiveResult_Handle(e);
            }
        }
        catch (Exception ex)
        {
            Disconnected_Handle();
        }
    }


    public void Disconnect()
    {
        if (clientWebSocket.State == WebSocketState.Open)
        {
            clientWebSocket.Abort();

            Disconnected_Handle();
        }
        else
        {
            Disconnected_Handle();
        }
    }


    public CWM_Audio CreateStartAudioMessage()
    {
        CWM_Audio msg = new CWM_Audio()
        {
            task_id = Guid.NewGuid(),
            seg_duration = mic_seg_duration,
            seg_overlap = mic_seg_overlap,
            is_final = false,
            time_start = PYTime.Time(),
            time_frame = PYTime.Time(),
            source = "mic",
            data = ""
        };

        return msg;
    }

    public CWM_Audio CreateProcessAudioMessage(CWM_Audio StartAudioMessage, byte[] AudioData)
    {
        CWM_Audio msg = (CWM_Audio)StartAudioMessage.Clone();

        msg.time_frame = PYTime.Time();

        // 归一化至-1.0-1.0
        float[] data = ConvertSampleFromByteToFloat(AudioData);

        // 移动平均滤波 降噪处理
        data = MovingAverageFilter(data, 512);

        byte[] bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        string base64String = Convert.ToBase64String(bytes);

        msg.data = base64String;

        return msg;
    }

    public CWM_Audio CreateFinalAudioMessage(CWM_Audio StartAudioMessage)
    {
        CWM_Audio msg = (CWM_Audio)StartAudioMessage.Clone();
        msg.time_frame = PYTime.Time();
        msg.is_final = true;
        msg.data = "";
        return msg;
    }



    // 16Bit的采样数据归一化至 -1.0 至 1.0
    private float[] ConvertSampleFromByteToFloat(byte[] bytesample)
    {
        int bytesRecorded = bytesample.Length;
        int samplesRecorded = bytesRecorded / 2;

        float[] res = new float[samplesRecorded];

        for (int index = 0; index < samplesRecorded; index++)
        {
            short sample = (short)((bytesample[index * 2 + 1] << 8) | bytesample[index * 2]);  //2字节的数据 高位在后 低位在前 移位操作
            var sampleValue = sample / 32768f;

            res[index] = sampleValue;
        }

        return res;
    }


    private async void SendWebSocketMsg(ClientWebSocket client)
    {
        // collection没有数据的话 会阻塞
        foreach (var data in collection.GetConsumingEnumerable(cts.Token))
        {
            // 将数据打包成 JSON
            var jsonData = JsonConvert.SerializeObject(data);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonData);

            // 通过 WebSocket 发送数据
            await clientWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);
            Console.WriteLine(jsonData);
        }
    }

    public void addMsg(CWM_Audio audioMsg)
    {
        collection.Add(audioMsg);
    }



    private static short[] Convert16BitSampleByteToShort(byte[] buffer)
    {
        int bytesRecorded = buffer.Length;

        // (对于16K 16BIT采样 每2个字节表示一次声音取样值)
        int samplesRecorded = bytesRecorded / 2;

        short[] samples = new short[samplesRecorded];
        for (int index = 0; index < samplesRecorded; index++)
        {
            samples[index] = (short)((buffer[index * 2 + 1] << 8) | buffer[index * 2]);  //2字节的数据 高位在后 低位在前 移位操作
        }

        return samples;
    }

    // 移动平均滤波 降噪
    private float[] MovingAverageFilter(float[] samples, int windowSize)
    {
        int halfWindowSize = windowSize / 2;
        float[] filteredSamples = new float[samples.Length];

        for (int i = 0; i < samples.Length; i++)
        {
            float sum = 0;
            int count = 0;

            for (int j = -halfWindowSize; j <= halfWindowSize; j++)
            {
                int index = i + j;
                if (index >= 0 && index < samples.Length)
                {
                    sum += samples[index];
                    count++;
                }
            }

            filteredSamples[i] = sum / count;
        }

        return filteredSamples;
    }

    // 中值滤波 降噪
    private float[] MedianFilter(float[] samples, int windowSize)
    {
        int halfWindowSize = windowSize / 2;
        float[] filteredSamples = new float[samples.Length];

        for (int i = 0; i < samples.Length; i++)
        {
            List<float> window = new List<float>();

            for (int j = -halfWindowSize; j <= halfWindowSize; j++)
            {
                int index = i + j;
                if (index >= 0 && index < samples.Length)
                {
                    window.Add(samples[index]);
                }
            }

            window.Sort();
            filteredSamples[i] = window[window.Count / 2];
        }

        return filteredSamples;
    }


}
