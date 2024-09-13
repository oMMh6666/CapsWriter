using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Microsoft.DirectX.DirectSound;


public class AudioReceive
{
    // 采样频率
    public int SamplesPerSecond { get; set; }
    // 采样位数
    public short BitsPerSample { get; set; }
    // 采样通道数
    public short Channels { get; set; }


    //音频数据获取相关
    private Microsoft.DirectX.DirectSound.Capture mCapDev = null;              // 音频捕捉设备
    private Microsoft.DirectX.DirectSound.CaptureBuffer mRecBuffer = null;     // 缓冲区对象

    private int mNextCaptureOffset = 0;        // 该次录音缓冲区的起始点
    private long mSampleCount = 0L;            // 录制的总样本数目
    private long TotalRecordByteSize = 0L;     // 录制的总字节数
    private double TotalRecordSeconds = 0;     // 录制的总秒数

    private Microsoft.DirectX.DirectSound.Notify mNotify = null; // 消息通知对象
    private const int cNotifyNum = 16;                           // 通知的个数
    private const int mNotifySize = 1600;                        // 每次通知大小 原python以48000采样 每0.05秒触发 但发送的数据每3个求平均 相当于16000Hz采样，0.05秒是800个采样点，发送的是float32
                                                                 // 这里如果用1600Byte 16Bit精度 等同于800个采样 最后需要转换成float32 必须使用生产者-消费者的模式 不然会生产数据过快，WebSocket发送数据过慢
    private int mBufferSize = mNotifySize * cNotifyNum;          // 缓冲队列大小
    private System.Threading.Thread mNotifyThread = null;                 // 处理缓冲区消息的线程
    private System.Threading.AutoResetEvent mNotificationEvent = null;    // 通知事件



    private Microsoft.DirectX.DirectSound.WaveFormat mWaveFormat;  // 音频格式

    private bool IsRecording { get; set; }

    // UI线程的同步上下文用
    private SynchronizationContext synchronizationContext;

    // 定义事件委托 收到录音数据时触发事件
    public delegate void RecordDataAvailableHandler(object sender, RecordDataEventArgs e);
    // 定义事件
    public event RecordDataAvailableHandler RecordDataAvailable;
    // 提供一个保护的虚拟方法来调用事件
    protected virtual void OnRecordDataAvailable(RecordDataEventArgs e)
    {
        synchronizationContext.Post(state =>
        {
            RecordDataAvailable?.Invoke(this, e);
        }, null);
    }

    public class RecordDataEventArgs : EventArgs
    {
        private byte[] buffer;
        private long totalSampleCount;
        private long totalRecordByteSize;
        private double totalRecordSeconds;

        public RecordDataEventArgs(byte[] buffer, long totalSampleCount, long totalRecordByteSize, double totalRecordSeconds)
        {
            this.buffer = buffer;
            this.totalSampleCount = totalSampleCount;
            this.totalRecordByteSize = totalRecordByteSize;
            this.totalRecordSeconds = totalRecordSeconds;
        }
        public byte[] Buffer
        {
            get { return buffer; }
        }
        public long SampleRecorded
        {
            get { return totalSampleCount; }
        }
        public long BytesRecorded
        {
            get { return totalRecordByteSize; }
        }
        public double SecondsRecorded
        {
            get { return totalRecordSeconds; }
        }
    }




    public AudioReceive(SynchronizationContext context)
    {
        // 由UI线程传进来的同步上下文用
        synchronizationContext = context;

        //初始化音频捕捉设备
        InitCaptureDevice(out mCapDev);

        IsRecording = false;
    }



    /// <summary>
    /// 初始化音频捕捉设备,使用默认录音设备
    /// </summary>
    /// <returns>调用成功返回true,否则返回false</returns>
    private bool InitCaptureDevice(out Microsoft.DirectX.DirectSound.Capture capture)
    {
        capture = null;

        //获取默认音频捕捉设备
        var captureDevicesCollection = new Microsoft.DirectX.DirectSound.CaptureDevicesCollection();  // 枚举音频捕捉设备
        Guid deviceGuid = Guid.Empty;
        if (captureDevicesCollection.Count > 0)
        {
            deviceGuid = captureDevicesCollection[0].DriverGuid; // 使用默认音频捕捉设备
        }
        else
        {
            //MessageBox.Show("系统中没有音频捕捉设备");
            return false;
        }

        // 用指定的捕捉设备创建Capture对象
        try
        {
            capture = new Microsoft.DirectX.DirectSound.Capture(deviceGuid);
        }
        catch (Microsoft.DirectX.DirectXException e)
        {
            //MessageBox.Show(e.ToString());
            return false;
        }
        return true;
    }



    /// <summary>
    /// 创建录音格式,默认使用16KHz,16bit,Mono单声道(对于语音这是一个比较适合的取样参数)
    /// <summary>
    /// <returns>返回Microsoft.DirectX.DirectSound.WaveFormat</returns>
    private Microsoft.DirectX.DirectSound.WaveFormat CreateWaveFormat(int SamplesPerSecond = 16000, short BitsPerSample = 16, short Channels = 1)
    {
        var format = new Microsoft.DirectX.DirectSound.WaveFormat()
        {
            FormatTag = WaveFormatTag.Pcm,          // PCM
            SamplesPerSecond = SamplesPerSecond,    // 采样率：16KHz
            BitsPerSample = BitsPerSample,          // 采样位数：16Bit
            Channels = Channels,                    // 声道：Mono
            BlockAlign = (short)(Channels * (BitsPerSample / 8)),  // 数据块对齐单位(每个采样需要的字节数) 单声道是2 立体声是4
            AverageBytesPerSecond = (short)(Channels * (BitsPerSample / 8)) * SamplesPerSecond,
            // 按照以上采样规格，可知采样1秒钟的字节数为 16000Hz*(16Bit/8)*1(Mono)=32000(byte) 约为31K
        };
        return format;
    }




    public bool Start()
    {
        // 创建音频采样格式 对于语音 16kHz 16Bit 单声道 是个比较合理的范围
        mWaveFormat = CreateWaveFormat(SamplesPerSecond, BitsPerSample, Channels);

        try
        {
            // 创建一个录音缓冲区，并开始录音  
            CreateCaptureBuffer(mCapDev, mWaveFormat);
            // 建立通知消息,当缓冲区满的时候处理方法  
            InitNotifications();
            mRecBuffer.Start(true);

            IsRecording = true;

            return true;
        }
        catch
        {
            IsRecording = false;

            return false;
        }
    }


    /// <summary>
    /// 创建录音使用的缓冲区
    /// </summary>
    private void CreateCaptureBuffer(Microsoft.DirectX.DirectSound.Capture CapDev, Microsoft.DirectX.DirectSound.WaveFormat waveFormat)
    {
        // 缓冲区的描述对象
        Microsoft.DirectX.DirectSound.CaptureBufferDescription bufferDescription = new CaptureBufferDescription();
        if (mNotify != null)
        {
            mNotify.Dispose();
            mNotify = null;
        }
        if (mRecBuffer != null)
        {
            mRecBuffer.Dispose();
            mRecBuffer = null;
        }

        // 创建缓冲区描述
        bufferDescription.BufferBytes = mBufferSize;
        // 录音格式
        bufferDescription.Format = waveFormat;
        // 创建缓冲区
        // 注意：在WIN10下 如果麦克风隐私权限中的 "允许桌面应用访问你的麦克风" 未打开 创建CaptureBuffer会报错
        mRecBuffer = new Microsoft.DirectX.DirectSound.CaptureBuffer(bufferDescription, CapDev);
        mNextCaptureOffset = 0;
        mSampleCount = 0;
        TotalRecordByteSize = 0;
        TotalRecordSeconds = 0;
    }



    /// <summary>
    /// 初始化通知事件,将原缓冲区分成16个缓冲队列,在每个缓冲队列的结束点设定通知点
    /// </summary>
    /// <returns>是否成功</returns>
    private bool InitNotifications()
    {
        if (mRecBuffer == null)
        {
            // MessageBox.Show("未创建录音缓冲区");
            return false;
        }
        // 创建一个通知事件,当缓冲队列满了就激发该事件.
        mNotificationEvent = new AutoResetEvent(false);
        // 创建一个线程管理缓冲区事件
        if (mNotifyThread == null)
        {
            mNotifyThread = new Thread(new ThreadStart(WaitThread));
            mNotifyThread.Start();
        }
        // 设定通知的位置 0-15 16设置在0位置 循环使用
        Microsoft.DirectX.DirectSound.BufferPositionNotify[] PositionNotify = new BufferPositionNotify[cNotifyNum + 1];
        for (int i = 0; i < cNotifyNum; i++)
        {
            PositionNotify[i].Offset = (mNotifySize * i) + mNotifySize - 1;
            PositionNotify[i].EventNotifyHandle = mNotificationEvent.SafeWaitHandle.DangerousGetHandle();
        }
        mNotify = new Notify(mRecBuffer);
        mNotify.SetNotificationPositions(PositionNotify, cNotifyNum);
        return true;
    }


    /// <summary>
    /// 接收缓冲区满消息的处理线程
    /// </summary>
    private void WaitThread()
    {
        while (true)
        {
            // 等待缓冲区的通知消息 在缓冲区到达位置前线程会停在此句后
            mNotificationEvent.WaitOne(Timeout.Infinite, true);

            // 读取缓冲区的音频数据,每次的数据量为mNotifySize的大小，CreateCaptureBuffer计算得到 默认音频格式下是4000字节
            byte[] CaptureData = GetCaptureData();

            // 处理收到的数据
            DealCaptureData(CaptureData);
        }
    }


    /// <summary>
    /// 每次通知在这里获取缓冲区内的数据
    /// </summary>
    private byte[] GetCaptureData()
    {
        byte[] CaptureData = null;

        int ReadPos = 0, CapturePos = 0, LockSize = 0;
        mRecBuffer.GetCurrentPosition(out CapturePos, out ReadPos);
        LockSize = ReadPos - mNextCaptureOffset;
        if (LockSize < 0)       // 因为是循环的使用缓冲区，所以有一种情况下为负：当文以载读指针回到第一个通知点，而Ibuffeoffset还在最后一个通知处
            LockSize += mBufferSize;
        LockSize -= (LockSize % mNotifySize);   // 对齐缓冲区边界,实际上由于开始设定完整,这个操作是多余的
        if (LockSize == 0)
            return CaptureData;

        // 读取缓冲区内的数据
        CaptureData = (byte[])mRecBuffer.Read(mNextCaptureOffset, typeof(byte), LockFlag.None, LockSize);

        // 更新已经录制的数据信息
        TotalRecordByteSize += CaptureData.Length; // 总录音数据字节数
        mSampleCount = TotalRecordByteSize * 8 / mWaveFormat.BitsPerSample;  // 总录音的采样数
        TotalRecordSeconds = (double)mSampleCount / mWaveFormat.SamplesPerSecond;    // 总录音的秒数

        // 移动录制数据的起始点,通知消息只负责指示产生消息的位置,并不记录上次录制的位置
        mNextCaptureOffset += CaptureData.Length;
        mNextCaptureOffset %= mBufferSize; // Circular buffer

        return CaptureData;
    }


    private void DealCaptureData(byte[] CaptureData)
    {
        if (CaptureData != null)
        {
            // 触发收到录音数据的事件
            RecordDataEventArgs e = new RecordDataEventArgs(CaptureData, mSampleCount, TotalRecordByteSize, TotalRecordSeconds);
            OnRecordDataAvailable(e);
        }
    }

    public void Stop()
    {
        if (IsRecording)
        {
            mRecBuffer.Stop();      // 调用缓冲区的停止方法，停止采集声音  
            if (null != mNotificationEvent)
                mNotificationEvent.Set();       //关闭通知  
            mNotifyThread.Abort();  //结束线程
            mNotifyThread = null;

            // 读取缓冲区的音频数据,每次的数据量为mNotifySize的大小，CreateCaptureBuffer计算得到 默认音频格式下每个通知是4000Byte
            byte[] CaptureData = GetCaptureData();

            DealCaptureData(CaptureData);
        }
    }
}
