using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;

using System.Net.WebSockets;

using Newtonsoft.Json;

namespace CapsWriterClient
{
    public partial class frmCapsWriterClient : Form
    {
        // 用于检测CapsLock键按住的状态
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private CWClient cwClient;

        // 录音广播类
        private AudioReceive myAudioReceive;
        // 同步上下文
        private SynchronizationContext context;


        public frmCapsWriterClient()
        {
            InitializeComponent();
        }

        private void frmCapsWriterClient_Load(object sender, EventArgs e)
        {
            // 同步上下文
            context = SynchronizationContext.Current;

            myAudioReceive = new AudioReceive(context)
            {
                SamplesPerSecond = 16000,
                BitsPerSample = 16,
                Channels = 1,
            };

            myAudioReceive.RecordDataAvailable += MyAudioReceive_RecordDataAvailable;
            if (myAudioReceive.Start())
            { }
            else
            {
                MessageBox.Show("找不到音频采集设备!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void frmCapsWriterClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (myAudioReceive != null)
                myAudioReceive.Stop();
        }
        

        private CWM_Audio startAudioMsg;
        private void MyAudioReceive_RecordDataAvailable(object sender, AudioReceive.RecordDataEventArgs e)
        {
            // 更新音量 只要有声音数据就在界面上更新音量
            UpdateUI(200, e.Buffer);

            // 未连接
            if (cwClient == null)
                return;

            if (cwClient.IsConnected)
            {
                // 已连接
                if (GetKeyState(0x14) < 0)  //CapsLock按住状态
                {
                    if (startAudioMsg == null)  // 第一个Message
                    {
                        startAudioMsg = cwClient.CreateStartAudioMessage();
                        cwClient.addMsg(startAudioMsg);
                    }
                    else  // 其他Message
                    {
                        CWM_Audio processAudioMsg = cwClient.CreateProcessAudioMessage(startAudioMsg, e.Buffer);
                        cwClient.addMsg(processAudioMsg);
                    }

                    lblStatus.Text = $"识别状态：{DateTime.Now.ToString("HH:mm:ss.fff")} 传送语音中...";
                }
                else  //CapsLock未按住
                {
                    if (startAudioMsg != null)  // 最后一个Message
                    {
                        // 最后一个有音频数据的Msg
                        CWM_Audio processAudioMsg = cwClient.CreateProcessAudioMessage(startAudioMsg, e.Buffer);
                        cwClient.addMsg(processAudioMsg);
                        //is_final = true的Msg
                        CWM_Audio finalAudioMsg = cwClient.CreateFinalAudioMessage(startAudioMsg);
                        cwClient.addMsg(finalAudioMsg);

                        startAudioMsg = null;

                        lblStatus.Text = $"识别状态：";
                    }
                }
            }
        }


        #region 界面刷新间隔 小于该值不刷新
        private DateTime timeLastUpdateUI = DateTime.Now;
        private void UpdateUI(int UpdateUIInterval, byte[] buffer)
        {
            TimeSpan ts = DateTime.Now - timeLastUpdateUI;
            if (ts.TotalMilliseconds < UpdateUIInterval)
            {
                return;
            }

            // 更新界面开始
            double dB = RMSToDBSPL(CalculateRMS(buffer));
            lblMicVolumeLevel.Text = $"当前话筒音量：{dB:0}db";
            // 更新界面结束

            timeLastUpdateUI = DateTime.Now;
        }
        #endregion

        // 计算RMS值
        private static double CalculateRMS(byte[] buffer)
        {
            int bytesRecorded = buffer.Length;

            // (对于16K 16BIT采样 每2个字节表示一次声音取样值)
            int samplesRecorded = bytesRecorded / 2;

            short[] samples = new short[samplesRecorded];
            for (int index = 0; index < samplesRecorded; index++)
            {
                samples[index] = (short)((buffer[index * 2 + 1] << 8) | buffer[index * 2]);  //2字节的数据 高位在后 低位在前 移位操作
            }

            // 将16位样本值转换为浮点数
            double[] floatSamples = samples.Select(s => (double)s).ToArray();

            // 计算均值
            double meanSquare = floatSamples.Average(s => s * s);  //s => s * s 均方根值
            return Math.Sqrt(meanSquare);
        }

        // 将RMS值转换为dB值
        private static double RMSToDBSPL(double rmsValue)
        {
            const double referenceValue = 32768.0; // 16位音频的最大值
            double dBSPLOffset = 94; // 将0 dBFS校准为94 dBSPL（这是一个常见的校准级别）
            return 20 * Math.Log10(rmsValue / referenceValue + 1e-10) + dBSPLOffset;  // 前面是dBFS（分贝相对于全刻度）
        }


        private void btnConnectToServer_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn.Text.Equals("连接"))
            {
                btnConnectToServer.Enabled = false;
                btnConnectToServer.Text = "连接中...";

                string ServerAddress = txtServerAddress.Text;
                ushort ServerPort = Convert.ToUInt16(txtServerPort.Text);

                cwClient = new CWClient(ServerAddress, ServerPort)
                { };
                cwClient.OnConnected += cwClient_OnConnected;
                cwClient.OnDisconnected += cwClient_OnDisconnected;
                cwClient.OnReceiveResult += cwClient_OnReceiveResult;

                cwClient.Connect();
            }
            else
            {
                if (btn.Text.Equals("断开"))
                {
                    cwClient.Disconnect();
                    cwClient = null;
                }
            }
        }

        private void cwClient_OnConnected()
        {
            btnConnectToServer.Enabled = true;
            btnConnectToServer.Text = "断开";
        }

        private void cwClient_OnDisconnected()
        {
            btnConnectToServer.Enabled = true;
            btnConnectToServer.Text = "连接";
        }

        private void cwClient_OnReceiveResult(object sender, CWClient.ResultEventArgs e)
        {
            Console.WriteLine(e.ResultJSON);

            CWM_Result resMsg = JsonConvert.DeserializeObject<CWM_Result>(e.ResultJSON);

            txtResult.Invoke(new EventHandler(delegate
            {
                txtResult.Text += resMsg.text;
            }));
        }


    }
}
