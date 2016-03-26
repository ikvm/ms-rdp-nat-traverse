﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MSRDPNatTraverseClient.Config;
using MSRDPNatTraverseClient.Utility;
using MSRDPNatTraverseClient.SSHReverseTunnel;
using Newtonsoft.Json;
using System.Diagnostics;
using MSRDPNatTraverseClient.Computer;
using MSRDPNatTraverseClient.MainFom;

namespace MSRDPNatTraverseClient
{
    public partial class MainForm : Form
    {
        #region 一些使用到的全局变量
        /// <summary>
        /// 本地计算机的对象实例
        /// </summary>
        private Computer.Computer localComputer = null;

        /// <summary>
        /// 配置对象实例
        /// </summary>
        private Config.Config programConfig = null;

        /// <summary>
        /// 代理服务器对象实例
        /// </summary>
        private ProxyServer.ProxyServer proxyServer = null;

        /// <summary>
        /// 两个特殊线程
        /// </summary>
        private Thread keepAliveThread = null;
        private Thread queryStatusThread = null;

        /// <summary>
        /// 取消线程的执行
        /// </summary>
        CancellationTokenSource cts = null;

        /// <summary>
        /// 在线计算机列表
        /// </summary>
        private List<int> onlineComputerList = new List<int>();

        // tunnel列表，可以存放备用隧道。防止连接失败。
        List<SSHReverseTunnel.SSHReverseTunnel> tunnelList = new List<SSHReverseTunnel.SSHReverseTunnel>();
        #endregion
        public MainForm()
        {
            InitializeComponent();

            // 显示这些信息
            LoadConfig();
        }

        #region 菜单项及按钮等事件处理函数集合
        /// <summary>
        /// 编辑菜单点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditServer();
        }

        /// <summary>
        /// 更换服务器菜单点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeServer();
        }

        /// <summary>
        /// 关于菜单项点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AboutProgramToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAboutDialog();
        }

        /// <summary>
        /// 编辑本地计算机菜单点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditLocalMachineToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            EditComputer();
        }

        /// <summary>
        /// 保存本地计算机菜单项点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveLocalMachineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveComputer();
        }

        /// <summary>
        /// 开机启动选择事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void autoStartupCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb != null)
            {
                SetAutoStartup(cb.Checked);
            }
        }

        /// <summary>
        /// 关闭窗口在后台运行点击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeWithoutQuitCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb != null)
            {
                SetCloseWithoutQuit(cb.Checked);
            }
        }

        /// <summary>
        /// 启动按钮单击
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void startButton_Click(object sender, EventArgs e)
        {
            Start();
        }

        /// <summary>
        /// 停止按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void stopButton_Click(object sender, EventArgs e)
        {
            Stop();
        }

        /// <summary>
        /// 退出按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void quitButton_Click(object sender, EventArgs e)
        {
            Quit();
        }

        /// <summary>
        /// 更新计算机列表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void upddateRemteMachineListButton_Click(object sender, EventArgs e)
        {
            UpdateRemoteMachineList();
        }

        /// <summary>
        /// 控制按钮单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void controlButton_Click(object sender, EventArgs e)
        {
            var bt = sender as Button;
            if (bt != null)
            {
                if (bt.Text == "连接")
                {
                    if (remoteComputerListBox.SelectedIndex == -1)
                    {
                        return;
                    }

                    var remoteId = onlineComputerList[remoteComputerListBox.SelectedIndex];
                    localComputer.PeeredId = remoteId;

                    // 准备在另个线程启动进度条
                    var dic = new Dictionary<string, string>();
                    dic["title"] = "正在请求远程控制";
                    dic["content"] = string.Format("正在向远程计算机({0})请求控制，请稍等...", remoteId);
                    threadProgress = new Thread(ShowProgressFormThread);
                    threadProgress.Start(dic);

                    if (await PrepareToControlRemoteComputerAsync(remoteId))
                    {
                        // 获取隧道端口
                        int tunnelPort = await Client.GetTunnelPortAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, remoteId, false);
                        if (tunnelPort != -1)
                        {
                            Debug.WriteLine("获取到远程计算机的隧道：" + tunnelPort.ToString());

                            bt.Text = "断开";
                            remoteComputerListBox.Enabled = false;
                            threadProgress.Abort();
                            MessageBox.Show("打开远程控制程序，输入：" + string.Format("{0}:{1}", proxyServer.Hostname, tunnelPort));
                        }
                        
                    }
                    threadProgress.Abort();
                }
                else
                {
                    if (await DisconnectToRemoteComputer(localComputer.PeeredId))
                    {
                        Debug.WriteLine("断开连接");
                        bt.Text = "连接";
                        remoteComputerListBox.Enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// 计算机信息属性变化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void machineInfoTextBox_ReadOnlyChanged(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            if (tb != null)
            {
                if (tb.ReadOnly)
                {
                    tb.ForeColor = Color.Black;
                }
                else
                {
                    tb.ForeColor = Color.Blue;
                }
            }
        }

        /// <summary>
        /// 主窗口关闭时的处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 保存当前的配置信息
            programConfig.Computer = localComputer;
            FileOperation.SaveConfig(programConfig);
        }

        #endregion

        #region 核心函数
        /// <summary>
        /// 编辑当前计算机信息
        /// </summary>
        private void EditComputer()
        {
            // 将编辑窗口改为可写状态
            machineNameTextBox.ReadOnly = false;
            machineDescriptionTextBox.ReadOnly = false;
            RDPPortTextBox.ReadOnly = false;
        }

        /// <summary>
        /// 保存当前计算机信息
        /// </summary>
        private void SaveComputer()
        {
            // 重置为只读状态
            machineNameTextBox.ReadOnly = true;
            machineDescriptionTextBox.ReadOnly = true;
            RDPPortTextBox.ReadOnly = true;

            // 保存所有信息
            localComputer.Name = machineNameTextBox.Text;
            localComputer.Description = machineDescriptionTextBox.Text;
            localComputer.RDPPort = int.Parse(RDPPortTextBox.Text);

            // 告诉配置信息类
            programConfig.Computer = localComputer;
        }

        /// <summary>
        /// 编辑代理服务器
        /// </summary>
        private void EditServer()
        {
            EditServerForm dialogForm = new EditServerForm();
            dialogForm.ShowDialog();

            // 关闭服务器编辑窗口后，需要从新从磁盘加载最新服务器配置，防止因为没有及时更新导致错误。
            var list = FileOperation.ReadServerList();

            if (list.Count > 0)
            {
                // 加载同名服务器最新信息
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Name == proxyServer.Name)
                    {
                        proxyServer = list[i];
                        ShowProxyServerInfo(proxyServer);

                        // 同时更新配置
                        programConfig.SelectedServerIndex = i;
                        return;
                    }
                }

                // 不存在的话，那么就尝试使用第一个
                proxyServer = list[0];
                ShowProxyServerInfo(proxyServer);
                // 同时更新配置
                programConfig.SelectedServerIndex = 0;
            }
            else
            {
                proxyServer = new ProxyServer.ProxyServer();
                ShowProxyServerInfo(proxyServer);
                programConfig.SelectedServerIndex = -1;
            }
        }

        /// <summary>
        /// 更换代理服务器
        /// </summary>
        private void ChangeServer()
        {
            ChangeServerForm form = new ChangeServerForm();
            if (form.ShowDialog() == DialogResult.Yes)
            {
                var obj = form.SelectedServer;
                if (obj != null)
                {
                    proxyServer = obj;
                    ShowProxyServerInfo(proxyServer);
                    // 更新配置信息
                    programConfig.SelectedServerIndex = form.SelectedServerIndex;
                }
            }
        }

        /// <summary>
        /// 显示代理服务器信息
        /// </summary>
        /// <param name="server"></param>
        private void ShowProxyServerInfo(ProxyServer.ProxyServer server)
        {
            // 显示代理服务器信息
            serverNameTextBox.Text = server.Name;
            serverIPTextBox.Text = string.Format("{0}:{1}", server.Hostname, server.LoginPort);
        }

        /// <summary>
        /// 显示计算机信息
        /// </summary>
        /// <param name="machine"></param>
        private void ShowComputerInfo(Computer.Computer machine)
        {
            machineNameTextBox.Text = machine.Name;
            machineIDTextBox.Text = machine.ID.ToString();
            machineDescriptionTextBox.Text = machine.Description;
            RDPPortTextBox.Text = machine.RDPPort.ToString();
        }

        /// <summary>
        /// 显示关于窗口
        /// </summary>
        private void ShowAboutDialog()
        {
            (new AboutForm()).ShowDialog();
        }

        /// <summary>
        /// 自启动设置
        /// </summary>
        /// <param name="enable"></param>
        private void SetAutoStartup(bool enable)
        {
            programConfig.AutoStartup = enable;
        }

        /// <summary>
        /// 关闭后依然在后台运行
        /// </summary>
        /// <param name="enable"></param>
        private void SetCloseWithoutQuit(bool enable)
        {
            programConfig.EnableBackgroundMode = enable;
        }

        /// <summary>
        /// 设置服务器连接状态
        /// </summary>
        /// <param name="status"></param>
        private void SetServerConnectionStatus(bool status)
        {
            if (status)
            {
                serverStatusTextBox.Text = "连接正常";
            }
            else
            {
                serverStatusTextBox.Text = "连接断开";
            }
        }

        /// <summary>
        /// 客户端启动相关服务
        /// </summary>
        private async void Start()
        {
            // 根据协议要求，首先获取一个id，标记在线注册
            localComputer.ID = await Client.GetComputerIdAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort);

            if (localComputer.ID != -1)
            {
                Debug.WriteLine("成功获取到id: " + localComputer.ID.ToString());

                SetServerConnectionStatus(true);
                // 更新显示
                ShowComputerInfo(localComputer);

                // 上传本机信息
                if (await Client.PostComputerInformationAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer))
                {
                    Debug.WriteLine("成功上传本机信息到代理服务器中");

                    // 创建两个线程分别负责检查在线状态和检查请求状态信息
                    keepAliveThread = new Thread(KeepAliveThread);
                    queryStatusThread = new Thread(QueryStatusThread);

                    cts = new CancellationTokenSource();

                    // 启动线程
                    Debug.WriteLine("启动保持在线状态的线程");
                    keepAliveThread.Start();

                    Debug.WriteLine("启动查询本机状态请求的线程");
                    queryStatusThread.Start();

                    // 更新按钮的状态
                    startButton.Enabled = false;
                    stopButton.Enabled = true;
                    updateOnlineListButton.Enabled = true;
                    controlButton.Enabled = true;
                }
                else
                {
                    Debug.WriteLine("没能上传本机信息到代理服务器中");
                }
            }
            else
            {
                Debug.WriteLine("获取id失败");
                SetServerConnectionStatus(false);
            }
        }

        /// <summary>
        /// 客户端停止相关服务
        /// </summary>
        private void Stop()
        {
            // 停止两个特殊线程
            if (cts != null)
            {
                cts.Cancel();
            }

            CloseAllSSHReverseTunnels();

            startButton.Enabled = true;
            stopButton.Enabled = false;
            updateOnlineListButton.Enabled = false;
            controlButton.Enabled = false;
        }

        /// <summary>
        /// 客户端退出，并停止所有服务
        /// </summary>
        private void Quit()
        {
            Stop();
            this.Close();
        }

        /// <summary>
        /// 更新在线用户列表
        /// </summary>
        private async void UpdateRemoteMachineList()
        {
            // 向服务器请求获取列表
            var dict = await Client.GetOnlineComputerListAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID);

            if (dict != null)
            {
                remoteComputerListBox.Items.Clear();
                onlineComputerList.Clear();
                for (int i = 0; i < dict.Count; i++)
                {
                    remoteComputerListBox.Items.Add(string.Format("{0}. {1}    {2}", 
                        i + 1, dict.ElementAt(i).Key, dict.ElementAt(i).Value));
                    onlineComputerList.Add(dict.ElementAt(i).Key);
                }
            }
        }

        /// <summary>
        /// 关闭所有正在启动的plink进程，停用隧道。
        /// </summary>
        private void CloseAllSSHReverseTunnels()
        {
            //foreach (var tunnel in tunnelList)
            //{
            //    tunnel.Stop();
            //}

            Debug.WriteLine("关闭所有隧道连接");
            foreach (var item in Process.GetProcessesByName("plink"))
            {
                item.Kill();
            }
        }

        #endregion

        #region 处理远程控制连接和断开等相关函数
        private SSHReverseTunnel.SSHReverseTunnel tunnel = null;
        private Thread threadProgress;

        /// <summary>
        /// 被控端会要求自己准备建立连接被控制
        /// </summary>
        /// <returns></returns>
        private async Task<bool> PrepareToBeUnderControlAsync()
        {
            // 存在控制请求后，会选择开始进行隧道建立
            // 首先，获得一个准许的隧道端口号
            var tunnelPort = await Client.GetTunnelPortAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID, true);

            // 确保端口号ok
            if (tunnelPort != -1)
            {
                // 启动本地隧道进程，尝试和远程代理服务器建立隧道
                tunnel = new SSHReverseTunnel.SSHReverseTunnel(localComputer, proxyServer, tunnelPort);
                if (tunnel.Start())
                {
                    // 实验表明，此处必须要延时等待一下，否则可能建立隧道会失败
                    Thread.Sleep(1000);

                    // 最多尝试查询次数
                    int tryCount = 5;
                    bool status = false;
                    while (tryCount != 0)
                    {
                        // 隧道进程成功启动后，我们需要向远程服务器查询有没有成功建立隧道端口
                        status = await Client.GetTunnelStatusAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID);

                        if (status)
                        {
                            Debug.WriteLine("隧道成功建立，端口号为：" + tunnelPort.ToString());
                        }
                        Thread.Sleep(500);
                        tryCount--;
                    }

                    if (status)
                    {
                        // 隧道成功建立，此时应当清除掉控制请求
                        if (await Client.PostControlRequestAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID, false))
                        {
                            Debug.WriteLine("远程控制请求已经得到处理");
                            if (await Client.PostIsUnderControlAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID, true))
                            {
                                tunnelList.Add(tunnel);
                                Debug.WriteLine("已经标记为正在被控制状态");
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 请求准备与远程计算机建立连接
        /// </summary>
        /// <param name="remoteId"></param>
        /// <returns></returns>
        private async Task<bool> PrepareToControlRemoteComputerAsync(int remoteId)
        {
            // 首先，要检查远程计算机是否正在被控制中
            if (await Client.GetIsUnderControlAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, remoteId))
            {
                if (threadProgress != null)
                {
                    threadProgress.Abort();
                }
                MessageBox.Show(string.Format("计算机(id: {0})正在被其他计算机远程控制中，拒绝请求！", remoteId));
                return false;
            }
            else
            {
                // 把需要控制的计算机的控制请求设置为true
                if (await Client.PostControlRequestAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, remoteId, true))
                {
                    // 等待对方响应请求
                    int tryCount = 100;
                    while (true)
                    {
                        if (await Client.GetIsUnderControlAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, remoteId))
                        {
                            Debug.WriteLine("远程计算机的隧道已经成功建立，可以被远程访问。");
                            return true;
                        }
                        tryCount--;
                        if (tryCount == 0)
                        {
                            return false;
                        }
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    // 可能是网络断开等原因，没有成功邀请
                    MessageBox.Show(string.Format("远程计算机(id: {0})没有成功收到邀请，请确保双方网络正常！", remoteId));
                    return false;
                }
            }
        }

        /// <summary>
        /// 关闭与远程计算机的配对
        /// </summary>
        /// <param name="remoteId"></param>
        /// <returns></returns>
        private async Task<bool> DisconnectToRemoteComputer(int remoteId)
        {
            if (remoteId != -1)
            {
                // 直接向对方发送释放控制请求即可
                if (await Client.PostIsUnderControlAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, remoteId, false))
                {
                    Debug.WriteLine("已经发送了断开请求，等待对方断开连接。");
                    MessageBox.Show(string.Format("已经断开与远程计算机({0})的连接!", localComputer.PeeredId));
                    localComputer.PeeredId = -1;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        #endregion

        #region 后台轮询和保持在线状态线程
        /// <summary>
        /// 该后台线程处理函数会定时查询计算机有没有控制请求等状态，供客户端使用
        /// </summary>
        /// <param name=""></param>
        private async void QueryStatusThread()
        {
            while (true)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    Debug.WriteLine("关闭线程：QueryStatusThread " + queryStatusThread.ManagedThreadId);
                    break;
                }

                // 查询有没有正在被控制
                // 如果计算机一直被控制中，将会被锁定，而无法被其他计算机连接
                if (await Client.GetIsUnderControlAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID))
                {
                    // 查询隧道状态
                    if (await Client.GetTunnelStatusAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID))
                    {
                        Debug.WriteLine("隧道状态正常");
                        Debug.WriteLine("本机正在被控制中");
                    }
                    else
                    {
                        // 重新启动隧道
                        CloseAllSSHReverseTunnels();
                        tunnel.Start();
                    }
                }
                else
                {
                    bool ck = true;
                    // 查询有没有控制请求
                    if (await Client.GetControlRequestAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID))
                    {
                        if (await PrepareToBeUnderControlAsync())
                        {
                            ck = false;
                            Debug.WriteLine("准备建立连接工作已经完毕，等待对方远程登录。");
                        }
                        else
                        {
                            Debug.WriteLine("准备建立连接工作失败！");
                        }
                    }

                    if (ck)
                    {
                        if (await Client.GetTunnelStatusAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID))
                        {
                            CloseAllSSHReverseTunnels();
                        }
                    }
                }
                //
                // 每隔2s查询一次状态
                //
                Thread.Sleep(2 * 1000);
            }
        }

        /// <summary>
        /// 该线程会定时更新服务器上的keep_alive_count值，防止因为被服务器递减为零后，认为超时并下线本机
        /// </summary>
        private async void KeepAliveThread()
        {
            while (true)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    Debug.WriteLine("关闭线程：KeepAliveThread " + keepAliveThread.ManagedThreadId);
                    break;
                }

                if (localComputer.ID != -1)
                {
                    if (await Client.PostKeepAliveCountAsync(proxyServer.Hostname, programConfig.ProxyServerListenPort, localComputer.ID, 10))
                    {
                        Debug.WriteLine("我还在线！");
                    }
                }
                // 每隔10s更新一次
                // 服务器会在30s收不到更新，自动判断为下线
                Thread.Sleep(10 * 1000);
            }
        }

        /// <summary>
        /// 专门用来显示进度的线程
        /// </summary>
        private void ShowProgressFormThread(object obj)
        {
            Dictionary<string, string> dict = (Dictionary<string, string>)obj;

            if (dict != null)
            {
                var form = new ProgressForm(dict["title"], dict["content"]);
                form.ShowDialog();
            }
        }
        #endregion

        #region 其他函数
        private void LoadConfig()
        {
            // 读取默认的配置信息
            programConfig = FileOperation.ReadConfig();

            if (programConfig != null)
            {
                if (programConfig.Computer != null)
                {
                    localComputer = programConfig.Computer;
                }
                else
                {
                    localComputer = new Computer.Computer();
                    programConfig.Computer = localComputer;
                }
            }
            else
            {
                // 做一些初始化的工作
                localComputer = new Computer.Computer();
                proxyServer = new ProxyServer.ProxyServer();
                programConfig = new Config.Config(autoStartupCheckBox.Checked,
                    closeWithoutQuitCheckBox.Checked, localComputer, -1, 9000);
            }
            // 显示本机的有关信息
            ShowComputerInfo(programConfig.Computer);

            // checkbox状态
            autoStartupCheckBox.Checked = programConfig.AutoStartup;
            closeWithoutQuitCheckBox.Checked = programConfig.EnableBackgroundMode;

            // 显示代理服务器信息
            if (programConfig.SelectedServerIndex != -1)
            {
                var serverList = FileOperation.ReadServerList();
                if (serverList.Count > 0)
                {
                    if (programConfig.SelectedServerIndex < serverList.Count)
                    {
                        proxyServer = serverList[programConfig.SelectedServerIndex];
                    }
                    else
                    {
                        proxyServer = serverList[0];
                    }
                    ShowProxyServerInfo(proxyServer);
                }
            }
            else
            {
                // 打开编辑窗口，提示用户添加服务器
                MessageBox.Show("没有可以选择的代理服务器，请自行添加代理服务器后更换。");
                EditServer();
                ChangeServer();
            }
        }
        #endregion


    }
}
