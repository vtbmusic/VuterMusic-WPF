﻿using LemonLibrary;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static LemonLibrary.InfoHelper;

namespace Lemon_App
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 一些字段
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        MusicLib ml = new MusicLib();
        private System.Windows.Forms.NotifyIcon notifyIcon;
        DataItem MusicData;
        bool isplay = false;
        bool IsRadio = false;
        string RadioID = "";
        int ind = 0;//歌词页面是否打开
        bool xh = false;//false: lb true:dq  循环/单曲 播放控制
        bool issingerloaded = false;
        bool isPos = false;//true:Wy false:QQ 播放控制
        bool mod = true;//true : qq false : wy

        bool isSearch = false;
        #endregion
        #region 等待动画
        public void OpenLoading()
        {
            var s = Resources["OpenLoadingFx"] as Storyboard;
            s.Completed += delegate { (Resources["FxLoading"] as Storyboard).Begin(); };
            s.Begin();
        }
        public void CloseLoading()
        {
            var s = Resources["CloseLoadingFx"] as Storyboard;
            s.Completed += delegate { (Resources["FxLoading"] as Storyboard).Stop(); };
            s.Begin();
        }
        #endregion
        #region 窗口加载辅助
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
        private void window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Settings.USettings.Skin_Path != ""&& System.IO.File.Exists(Settings.USettings.Skin_Path))
            {
                App.BaseApp.Skin();
                Page.Background = new ImageBrush(new BitmapImage(new Uri(Settings.USettings.Skin_Path, UriKind.Absolute)));
                App.BaseApp.SetColor("ThemeColor", Color.FromRgb(byte.Parse(Settings.USettings.Skin_Theme_R),
                    byte.Parse(Settings.USettings.Skin_Theme_G),
                    byte.Parse(Settings.USettings.Skin_Theme_B)));
                Color co;
                if (Settings.USettings.Skin_txt == "Black")
                    co = Color.FromRgb(64, 64, 64);
                else co = Color.FromRgb(255, 255, 255);
                App.BaseApp.SetColor("ResuColorBrush", co);
                App.BaseApp.SetColor("ButtonColorBrush", co);
                App.BaseApp.SetColor("TextX1ColorBrush", co);
            }
            else App.BaseApp.unSkin();

            Settings.SaveWINDOW_HANDLE(new WindowInteropHelper(this).Handle.ToInt32());
            LoadSEND_SHOW();

            var ani = Resources["Loading"] as Storyboard;
            ani.Completed += Ani_Completed;
            ani.Begin();
        }

        private void Ani_Completed(object sender, EventArgs e)
        {
            OpenLoading();
            Updata();
            LoadSettings();
            LoadHotDog();
            /////Timer user
            var ds = new System.Windows.Forms.Timer() { Interval = 2000 };
            ds.Tick += delegate { GC.Collect(); UIHelper.G(Page); };
            ds.Start();
            UserName.Text = Settings.USettings.UserName;
            if (System.IO.File.Exists(Settings.USettings.UserImage))
            {
                var image = new System.Drawing.Bitmap(Settings.USettings.UserImage);
                UserTX.Background = new ImageBrush(image.ToImageSource());
            }
                (Resources["Closing"] as Storyboard).Completed += delegate { ShowInTaskbar = false; };
            ////////////load
            LyricView lv = new LyricView();
            lv.FoucsLrcColor = new SolidColorBrush(Color.FromArgb(255,255,255,255));
            lv.NoramlLrcColor = new SolidColorBrush(Color.FromArgb(100,255,255,255));
            lv.TextAlignment = TextAlignment.Left;
            ly.Child = lv;
            ml = new MusicLib(lv,Settings.USettings.LemonAreeunIts);
            if (Settings.USettings.Playing.MusicName != "")
            {
                PlayMusic(Settings.USettings.Playing.MusicID, Settings.USettings.Playing.ImageUrl, Settings.USettings.Playing.MusicName, Settings.USettings.Playing.Singer, false, false);
                jd.Maximum = Settings.USettings.alljd;
                jd.Value = Settings.USettings.jd;
                ml.m.Position = TimeSpan.FromMilliseconds(Settings.USettings.jd);
                Play_All.Text = TextHelper.TimeSpanToms(TimeSpan.FromMilliseconds(Settings.USettings.alljd));
                Play_Now.Text = TextHelper.TimeSpanToms(TimeSpan.FromMilliseconds(Settings.USettings.jd));
            }
            t.Interval = 500;
            t.Tick += delegate
            {
                try
                {
                    Play_All.Text =TextHelper.TimeSpanToms(ml.m.NaturalDuration.TimeSpan);
                    Play_Now.Text = TextHelper.TimeSpanToms(ml.m.Position);
                    jd.Maximum = ml.m.NaturalDuration.TimeSpan.TotalMilliseconds;
                    jd.Value = ml.m.Position.TotalMilliseconds;
                    if (ind == 1)
                        ml.lv.LrcRoll(ml.m.Position.TotalMilliseconds);
                    Settings.USettings.alljd = jd.Maximum;
                    Settings.USettings.jd = jd.Value;
                }
                catch { }
            };
            ml.m.MediaEnded += delegate
            {
                t.Stop();
                ml.m.Stop();
                jd.Value = 0;
                if (xh)
                    if (IsRadio)
                        PlayMusic(RadioData.MusicID, RadioData.ImageUrl, RadioData.MusicName, RadioData.Singer, true);
                    else PlayMusic(MusicData, null);
                else
                {
                    if (IsRadio)
                        GetRadio(new RadioItem(RadioID), null);
                    else
                        PlayMusic(DataItemsList.Children[DataItemsList.Children.IndexOf(MusicData) + 1] as DataItem, null);
                }
            };
            /////top////
            var de = new Task(new Action(async delegate
            {
                var dt = await ml.GetTopIndexAsync();
                Dispatcher.Invoke(() => { topIndexList.Children.Clear(); });
                foreach (var d in dt)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var top = new TopControl(d.ID, d.Photo, d.Name);
                        top.MouseDown += delegate (object seb, MouseButtonEventArgs ed)
                        {
                            isSearch = false;
                            OpenLoading();
                            var g = seb as TopControl;
                            NSPage(null, Data);
                            string file = Settings.USettings.CachePath + "Image\\Top" + g.topID + ".jpg";
                            if (!System.IO.File.Exists(file))
                            {
                                var s = new WebClient();
                                s.DownloadFileAsync(new Uri(g.pic), file);
                                s.DownloadFileCompleted += delegate {TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource()); };
                            }
                            else TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource());
                            TB.Text = g.name;
                            var ss = new Task(new Action(async delegate
                            {
                                var dta = await ml.GetToplistAsync(int.Parse(g.topID));
                                Dispatcher.Invoke(() => { DataItemsList.Children.Clear(); });
                                foreach (var j in dta)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        var k = new DataItem(j) { Width = DataItemsList.ActualWidth };
                                        k.MouseDown += PlayMusic;
                                        if (k.isPlay(MusicName.Text))
                                        {
                                            k.ShowDx();
                                            MusicData = k;
                                        }
                                        DataItemsList.Children.Add(k);
                                    });
                                    await Task.Delay(1);
                                }
                                isSearch = false;
                                Dispatcher.Invoke(() => { CloseLoading(); });
                            }));
                            ss.Start();
                        };
                        top.Margin = new Thickness(0, 0, 20, 20);
                        topIndexList.Children.Add(top);
                    });
                }
                Dispatcher.Invoke(() => { CloseLoading(); });
            }));
            de.Start();
            ////TB GDlist////
            var gd = new Task(new Action(async delegate {
                await ml.UpdateGdAsync();
            }));
            gd.Start();
            CloseLoading();
        }

        private void exShow() {
                this.WindowState = WindowState.Normal;
                var ani = Resources["Loading"] as Storyboard;
                ani.Completed -= Ani_Completed;
                ani.Begin();
                this.Activate();
        }
        private void MaxBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MaxHeight = SystemParameters.WorkArea.Height + 10;
            if (WindowState == WindowState.Normal)
            {
                c.ResizeBorderThickness = new Thickness(0);
                Page.BeginAnimation(MarginProperty, new ThicknessAnimation(new Thickness(0), TimeSpan.FromSeconds(0)));
                WindowState = WindowState.Maximized;
                Page.Clip = new RectangleGeometry() { RadiusX = 0, RadiusY = 0, Rect = new Rect() { Width = Page.ActualWidth, Height = Page.ActualHeight } };
            }
            else
            {
                c.ResizeBorderThickness = new Thickness(30);
                Page.BeginAnimation(MarginProperty, new ThicknessAnimation(new Thickness(30), TimeSpan.FromSeconds(0)));
                WindowState = WindowState.Normal;
                Page.Clip = new RectangleGeometry() { RadiusX = 5, RadiusY = 5, Rect = new Rect() { Width = Page.ActualWidth, Height = Page.ActualHeight } };
            }
        }
        private void MinBtn_MouseDown(object sender, MouseButtonEventArgs e) { ShowInTaskbar = true; WindowState = WindowState.Minimized; }
        private void window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Page.Clip = new RectangleGeometry() { RadiusX = 5, RadiusY = 5, Rect = new Rect() { Width = Page.ActualWidth, Height = Page.ActualHeight } };
            foreach (DataItem dx in DataItemsList.Children)
                dx.Width = DataItemsList.ActualWidth;
        }
        #endregion
        #region 设置
        public void LoadSettings() {
            CachePathTb.Text = Settings.USettings.CachePath;
            DownloadPathTb.Text = Settings.USettings.DownloadPath;
        }
        private void SettingsBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            LoadSettings();
            NSPage(null, SettingsPage);
        }
        private void SettingsPage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 3)
            {
                if (hhh.Visibility == Visibility.Collapsed)
                    hhh.Visibility = Visibility.Visible;
                else hhh.Visibility = Visibility.Collapsed;
            }
        }
        private void CP_ChooseBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var g = new System.Windows.Forms.FolderBrowserDialog();
            if (g.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadPathTb.Text = g.SelectedPath;
                Settings.USettings.DownloadPath = g.SelectedPath;
            }
        }

        private void DP_ChooseBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var g = new System.Windows.Forms.FolderBrowserDialog();
            if (g.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CachePathTb.Text = g.SelectedPath;
                Settings.USettings.DownloadPath = g.SelectedPath;
            }
        }

        private void CP_OpenBt_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", CachePathTb.Text);
        }

        private void DP_OpenBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", DownloadPathTb.Text);
        }
        #endregion
        #region 主题切换
        private async void SkinBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NSPage(null, SkinPage);
            SkinIndexList.Children.Clear();
            var json = JObject.Parse(await HttpHelper.GetWebAsync("https://gitee.com/TwilightLemon/ux/raw/master/SkinList.json"))["data"];
            int i = 1;
            foreach (var dx in json)
            {
                string name = dx["name"].ToString();
                Color color = Color.FromRgb(byte.Parse(dx["ThemeColor"]["R"].ToString()),
                    byte.Parse(dx["ThemeColor"]["G"].ToString()),
                    byte.Parse(dx["ThemeColor"]["B"].ToString()));
                if(!System.IO.File.Exists(Settings.USettings.CachePath + "Skin\\" + i + ".jpg"))
                     await HttpHelper.HttpDownloadFileAsync($"https://gitee.com/TwilightLemon/ux/raw/master/w{i}.jpg", Settings.USettings.CachePath + "Skin\\" + i + ".jpg");
                SkinControl sc = new SkinControl(i, name, color);
                sc.txtColor = dx["TextColor"].ToString();
                sc.MouseDown += async (s, n) => {
                    if (!System.IO.File.Exists(Settings.USettings.CachePath + "Skin\\" + sc.imgurl + ".png"))
                        await HttpHelper.HttpDownloadFileAsync($"https://gitee.com/TwilightLemon/ux/raw/master/{sc.imgurl}.png", Settings.USettings.CachePath + "Skin\\" + sc.imgurl + ".png");
                    Page.Background = new ImageBrush(new System.Drawing.Bitmap(Settings.USettings.CachePath + "Skin\\" + sc.imgurl + ".png").ToImageSource());
                    App.BaseApp.Skin();
                    App.BaseApp.SetColor("ThemeColor", sc.theme);
                    Color co;
                    if (sc.txtColor == "Black")
                        co = Color.FromRgb(64, 64, 64);
                    else co = Color.FromRgb(255, 255, 255);
                    App.BaseApp.SetColor("ResuColorBrush", co);
                    App.BaseApp.SetColor("ButtonColorBrush", co);
                    App.BaseApp.SetColor("TextX1ColorBrush", co);
                    Settings.USettings.Skin_Path = Settings.USettings.CachePath + "Skin\\" + +sc.imgurl + ".png";
                    Settings.USettings.Skin_txt = sc.txtColor;
                    Settings.USettings.Skin_Theme_R = sc.theme.R.ToString();
                    Settings.USettings.Skin_Theme_G = sc.theme.G.ToString();
                    Settings.USettings.Skin_Theme_B = sc.theme.B.ToString();
                    Settings.SaveSettings();
                };
                sc.Margin = new Thickness(10, 0, 0, 0);
                SkinIndexList.Children.Add(sc);
                i++;
            }
            SkinControl sxc = new SkinControl(-1, "默认主题", Color.FromArgb(0,0,0,0));
            sxc.MouseDown += (s, n) =>{
                Page.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                App.BaseApp.unSkin();
                Settings.USettings.Skin_Path = "";
                Settings.SaveSettings();
            };
            sxc.Margin = new Thickness(10, 0, 0, 0);
            SkinIndexList.Children.Add(sxc);
        }
        #endregion
        #region 功能区
        #region Updata
        private async void Updata() {
            var o = JObject.Parse(await HttpHelper.GetWebAsync("https://gitee.com/TwilightLemon/ux/raw/master/WindowsUpdata.json"));
            string v = o["version"].ToString();
            string dt = o["description"].ToString().Replace("@32","\n");
            if (int.Parse(v) > int.Parse(App.EM)) {
                if (MyMessageBox.Show("小萌有更新啦", dt, "立即更新")) {
                    var xpath = Settings.USettings.CachePath + "win-release.exe";
                    await HttpHelper.HttpDownloadFileAsync("https://coding.net/u/twilightlemon/p/Updata/git/raw/master/win-release.exe", xpath);
                    Process.Start(xpath);
                }
            }
        }
        #endregion
        #region N/S Page
        private Label LastClickLabel = null;
        private Grid LastPage = null;
        public void NSPage(Label ClickLabel,Grid TPage) {
            if (LastClickLabel == null) LastClickLabel = TopBtn;
            LastClickLabel.SetResourceReference(ForegroundProperty, "ResuColorBrush");
            if (ClickLabel!=null)ClickLabel.SetResourceReference(ForegroundProperty, "ThemeColor");
            if (LastPage == null) LastPage = TopIndexPage;
            LastPage.Visibility = Visibility.Collapsed;
            TPage.Visibility = Visibility.Visible;
            TPage.BeginAnimation(OpacityProperty, new DoubleAnimation(0.5,1, TimeSpan.FromSeconds(0.2)));
            if (ClickLabel != null) LastClickLabel = ClickLabel;
            LastPage = TPage;
        }
        private void TopBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NSPage(TopBtn, TopIndexPage);
        }
        #endregion
        #region Singer
        string SingerKey1 = "all_all_";
        string SingerKey2 = "all";
        private void SingerPageChecked(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                OpenLoading();
                SingerKey1 = (sender as RadioButton).Uid;
                string sk = SingerKey1 + SingerKey2;
                if (sk == "all")
                    sk = "all_all_all";
                var s = new Task(new Action(async delegate
                {
                    var sin = await ml.GetSingerAsync(sk);
                    Dispatcher.Invoke(() => { singerItemsList.Children.Clear(); });
                    foreach (var d in sin)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var sinx = new SingerItem(d.Photo, d.Name) { Margin = new Thickness(20, 0, 0, 20) };
                            sinx.MouseDown += GetSinger;
                            singerItemsList.Children.Add(sinx);
                        });
                        await Task.Delay(1);
                    }
                    Dispatcher.Invoke(() => { CloseLoading(); });
                }));
                s.Start();
            }
        }
        public void GetSinger(object sender, MouseEventArgs e)
        {
            SearchMusic((sender as SingerItem).singer);
        }
        private void SIngerPageChecked(object sender, RoutedEventArgs e)
        {
             if (sender != null)
                {
                    OpenLoading();
                    if (SingerKey1 == "")
                        SingerKey1 = "all_all_";
                    SingerKey2 = (sender as RadioButton).Content.ToString().Replace("热门", "all").Replace("#", "9");
                    var s = new Task(new Action(async delegate
                    {
                        var mx = await ml.GetSingerAsync(SingerKey1 + SingerKey2);
                        Dispatcher.Invoke(() => { singerItemsList.Children.Clear(); });
                        foreach (var d in mx)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                var sinx = new SingerItem(d.Photo, d.Name) { Margin = new Thickness(20, 0, 0, 20) };
                                sinx.MouseDown += GetSinger;
                                singerItemsList.Children.Add(sinx);
                            });
                            await Task.Delay(1);
                        }
                        Dispatcher.Invoke(() => { CloseLoading(); });
                    }));
                    s.Start();
                }
        }
        private void SingerBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NSPage(SingerBtn, SingerIndexPage);
            if (!issingerloaded)
            {
                issingerloaded = true;
                foreach (var c in Singerws.Children)
                    (c as RadioButton).Checked += SingerPageChecked;
                foreach (var c in Singersx.Children)
                    (c as RadioButton).Checked += SIngerPageChecked;

                OpenLoading();
                var s = new Task(new Action(async delegate
                {
                    var mx = await ml.GetSingerAsync("all_all_all");
                    Dispatcher.Invoke(() => { singerItemsList.Children.Clear(); });
                    foreach (var d in mx)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var sinx = new SingerItem(d.Photo, d.Name) { Margin = new Thickness(20, 0, 0, 20) };
                            sinx.MouseDown += GetSinger;
                            singerItemsList.Children.Add(sinx);
                        });
                        await Task.Delay(1);
                    }
                    Dispatcher.Invoke(() => { CloseLoading(); });
                }));
                s.Start();
            }
        }
        #endregion
        #region FLGD
        private void ZJBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NSPage(ZJBtn, ZJIndexPage);
            if (FLGDIndexList.Children.Count == 0)
            {
                var sinx = new Task(new Action(async delegate
                {
                    Dispatcher.Invoke(() => { OpenLoading(); });
                    var wk = await ml.GetFLGDIndexAsync();
                    Dispatcher.Invoke(() => { 
                    RadioButton rb = new RadioButton()
                    {
                        Style = RadioMe.Style,
                        Background = RadioMe.Background,
                        Content = wk.Hot[0].name,
                        Uid = wk.Hot[0].id,
                        Margin = new Thickness(0, 0, 30, 10)
                    };
                        rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                        rb.SetResourceReference(BorderBrushProperty, "TextX1ColorBrush");
                        FLGDIndexList.Children.Add(rb); });
                    Dispatcher.Invoke(() => {
                        var rb = new TextBlock() { Text = "语种:" };
                        rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                        FLGDIndexList.Children.Add(rb);
                    });
                    foreach (var d in wk.Lauch)
                        Dispatcher.Invoke(() => {
                            var rb = new RadioButton()
                            {
                                Style = RadioMe.Style,
                                Background = RadioMe.Background,
                                Content = d.name,
                                Uid = d.id,
                                Margin = new Thickness(0, 0, 10, 10)
                            };
                            rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                            rb.SetResourceReference(BorderBrushProperty, "TextX1ColorBrush");
                            FLGDIndexList.Children.Add(rb); });
                    Dispatcher.Invoke(() => {
                        var rb = new TextBlock() { Text = "流派:" };
                        rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                        FLGDIndexList.Children.Add(rb);
                    });
                    foreach (var d in wk.LiuPai)
                        Dispatcher.Invoke(() => {
                            var rb = new RadioButton()
                            {
                                Style = RadioMe.Style,
                                Background = RadioMe.Background,
                                Content = d.name,
                                Uid = d.id,
                                Margin = new Thickness(0, 0, 10, 10)
                            };
                            rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                            rb.SetResourceReference(BorderBrushProperty, "TextX1ColorBrush");
                            FLGDIndexList.Children.Add(rb);
                        });
                    Dispatcher.Invoke(() => {
                        var rb = new TextBlock() { Text = "主题:" };
                        rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                        FLGDIndexList.Children.Add(rb);
                    });
                    foreach (var d in wk.Theme)
                        Dispatcher.Invoke(() => {
                            var rb = new RadioButton()
                            {
                                Style = RadioMe.Style,
                                Background = RadioMe.Background,
                                Content = d.name,
                                Uid = d.id,
                                Margin = new Thickness(0, 0, 10, 10)
                            };
                            rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                            rb.SetResourceReference(BorderBrushProperty, "TextX1ColorBrush");
                            FLGDIndexList.Children.Add(rb);
                        });
                    Dispatcher.Invoke(() => {
                        var rb = new TextBlock() { Text = "心情:" };
                        rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                        FLGDIndexList.Children.Add(rb);
                    });
                    foreach (var d in wk.Heart)
                        Dispatcher.Invoke(() => {
                            var rb = new RadioButton()
                            {
                                Style = RadioMe.Style,
                                Background = RadioMe.Background,
                                Content = d.name,
                                Uid = d.id,
                                Margin = new Thickness(0, 0, 10, 10)
                            };
                            rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                            rb.SetResourceReference(BorderBrushProperty, "TextX1ColorBrush");
                            FLGDIndexList.Children.Add(rb);
                        });
                    Dispatcher.Invoke(() => {
                        var rb = new TextBlock() { Text = "场景:" };
                        rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                        FLGDIndexList.Children.Add(rb);
                    });
                    foreach (var d in wk.Changjing)
                        Dispatcher.Invoke(() => {
                            var rb = new RadioButton()
                            {
                                Style = RadioMe.Style,
                                Background = RadioMe.Background,
                                Content = d.name,
                                Uid = d.id,
                                Margin = new Thickness(0, 0, 10, 10)
                            };
                            rb.SetResourceReference(ForegroundProperty, "TextX1ColorBrush");
                            rb.SetResourceReference(BorderBrushProperty, "TextX1ColorBrush");
                            FLGDIndexList.Children.Add(rb);
                        });
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var d in FLGDIndexList.Children)
                        {
                            if (d is RadioButton)
                                (d as RadioButton).Checked += FLGDPageChecked;
                        }
                    });
                    var dat = await ml.GetFLGDAsync(int.Parse(wk.Hot[0].id));
                    Dispatcher.Invoke(() => { FLGDItemsList.Children.Clear(); });
                    foreach (var d in dat)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var kss = new FLGDIndexItem(d.ID, d.Name, d.Photo) { Margin = new Thickness(20, 0, 0, 20) };
                            kss.MouseDown += GDMouseDown;
                            FLGDItemsList.Children.Add(kss);
                        });
                        await Task.Delay(1);
                    }
                    foreach (var d in (await ml.GetRadioList()).Hot)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var a = new RadioItem(d.ID, d.Name, d.Photo) { Margin = new Thickness(0, 0, 20, 20) };
                            a.MouseDown += GetRadio;
                            RadioItemsList.Children.Add(a);
                        }); await Task.Delay(1);
                    }
                    Dispatcher.Invoke(() => { CloseLoading(); });
                }));
                sinx.Start();
            }
        }
        public void GDMouseDown(object s, MouseButtonEventArgs se)
        {
            GetGD((s as FLGDIndexItem).id);
        }
        private void FLGDPageChecked(object sender, RoutedEventArgs e)
        {
                if (sender != null)
                {
                    OpenLoading();
                    var dt = sender as RadioButton;
                    int xs = int.Parse(dt.Uid);
                    var s = new Task(new Action(async delegate
                    {
                        var data = await ml.GetFLGDAsync(xs);
                        Dispatcher.Invoke(() =>
                        {
                            FLGDItemsList.Children.Clear();
                            foreach (var d in data)
                            {
                                var k = new FLGDIndexItem(d.ID, d.Name, d.Photo) { Margin = new Thickness(20, 0, 0, 20) };
                                k.MouseDown += GDMouseDown;
                                FLGDItemsList.Children.Add(k);
                            }
                            CloseLoading();
                        });
                    }));
                    s.Start();
                }
        }
        public void GetGD(string id)
        {
            isSearch = false;
            OpenLoading();
            var sx = new Task(new Action(async delegate {
                var dt = await ml.GetGDAsync(id);
                string file = Settings.USettings.CachePath + "Image\\GD" + id + ".jpg";
                if (!System.IO.File.Exists(file))
                {
                    var s = new WebClient();
                    s.DownloadFileAsync(new Uri(dt.pic), file);
                    s.DownloadFileCompleted += delegate { Dispatcher.Invoke(() => { TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource()); }); };
                }
                else Dispatcher.Invoke(() => { TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource());});
                Dispatcher.Invoke(() =>
                {
                    TB.Text = dt.name;
                    DataItemsList.Children.Clear();
                    foreach (var j in dt.Data)
                    {
                        var k = new DataItem(j) { Width = DataItemsList.ActualWidth };
                        if (k.isPlay(MusicName.Text))
                        {
                            k.ShowDx();
                            MusicData = k;
                        }
                        k.MouseDown += PlayMusic;
                        DataItemsList.Children.Add(k);
                    }
                    NSPage(null, Data);
                });
                isSearch = false;
                Dispatcher.Invoke(() => { CloseLoading(); });
            }));
            sx.Start();
        }
        #endregion
        #region Radio
        private void RadioBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NSPage(RadioBtn, RadioIndexPage);
            RadioMe.IsChecked = true;
        }

        public void GetRadio(object sender, MouseEventArgs e)
        {
            OpenLoading();
            var x = new Task(new Action(async delegate
            {
                var dt = sender as RadioItem;
                RadioID = dt.id;
                var data = await ml.GetRadioMusicAsync(dt.id);
                RadioData = data;
                Dispatcher.Invoke(() =>
                {
                    ml.mldata.Add(data.MusicID, (data.MusicName + " - " + data.Singer).Replace("\\", "-").Replace("?", "").Replace("/", "").Replace(":", "").Replace("*", "").Replace("\"", "").Replace("<", "").Replace(">", "").Replace("|", ""));
                    PlayMusic(data.MusicID, data.ImageUrl, data.MusicName, data.Singer, true);
                });
                Dispatcher.Invoke(() => { CloseLoading(); });
            }));
            x.Start();
        }
        private void RadioPageChecked(object sender, RoutedEventArgs e)
        {
                if (sender != null)
                {
                    OpenLoading();
                    var dt = sender as RadioButton;
                    var s = new Task(new Action(async delegate
                    {
                        var data = await ml.GetRadioList();
                        Dispatcher.Invoke(() => { RadioItemsList.Children.Clear(); });
                        List<MusicRadioListItem> dat = null;
                        Dispatcher.Invoke(() =>
                        {
                            switch (dt.Uid)
                            {
                                case "0":
                                    dat = data.Hot;
                                    break;
                                case "1":
                                    dat = data.Evening;
                                    break;
                                case "2":
                                    dat = data.Love;
                                    break;
                                case "3":
                                    dat = data.Theme;
                                    break;
                                case "4":
                                    dat = data.Changjing;
                                    break;
                                case "5":
                                    dat = data.Style;
                                    break;
                                case "6":
                                    dat = data.Lauch;
                                    break;
                                case "7":
                                    dat = data.People;
                                    break;
                                case "8":
                                    dat = data.Diqu;
                                    break;
                            }
                        });
                        foreach (var d in dat)
                        {
                            Dispatcher.Invoke(() => { RadioItemsList.Children.Add(new RadioItem(d.ID, d.Name, d.Photo) { Margin = new Thickness(0, 0, 20, 20) }); });
                            await Task.Delay(1);
                        }
                        Dispatcher.Invoke(() => {
                            foreach (var i in RadioItemsList.Children)
                                (i as RadioItem).MouseDown += GetRadio;
                        });
                        Dispatcher.Invoke(() => { CloseLoading(); });
                    }));
                    s.Start();
                }
        }
        Music RadioData;
        #endregion
        #region ILike
        private void LikeBtnUp() {
            likeBtn_path.SetResourceReference(Path.FillProperty, "ResuColorBrush");
        }

        private void LikeBtnDown() {
            likeBtn_path.Fill = new SolidColorBrush(Color.FromRgb(216, 30, 30));
        }
        private void likeBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (MusicName.Text != "MusicName")
            {
                if (IsRadio)
                {
                    if (Settings.USettings.MusicLike.ContainsKey(RadioData.MusicID))
                    {
                        LikeBtnUp();
                        Settings.USettings.MusicLike.Remove(RadioData.MusicID);
                    }
                    else
                    {
                        Settings.USettings.MusicLike.Add(RadioData.MusicID, RadioData);
                        LikeBtnDown();
                    }
                }
                else
                {
                    if (Settings.USettings.MusicLike.ContainsKey(MusicData.ID))
                    {
                        LikeBtnUp();
                        Settings.USettings.MusicLike.Remove(MusicData.ID);
                    }
                    else
                    {
                        Settings.USettings.MusicLike.Add(MusicData.ID, new InfoHelper.Music()
                        {
                            GC = MusicData.ID,
                            Singer = MusicData.Singer,
                            ImageUrl = MusicData.Image,
                            MusicID = MusicData.ID,
                            MusicName = MusicData.SongName
                        });
                        LikeBtnDown();
                    }
                }
                Settings.SaveSettings();
            }
        }
        private void LikeBtn_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            NSPage(LikeBtn, Data);
            TB.Text = "我喜欢";
            TXx.Background = Resources["LoveIcon"] as VisualBrush;
            DataItemsList.Children.Clear();
            foreach (var dt in Settings.USettings.MusicLike.Values)
            {
                var jm = new DataItem(dt) { Width = DataItemsList.ActualWidth };
                if (jm.isPlay(MusicName.Text))
                {
                    jm.ShowDx();
                    MusicData = jm;
                }
                jm.MouseDown += PlayMusic;
                DataItemsList.Children.Add(jm);
            }
            isSearch = false;
        }
        #endregion
        #region DataPageBtn
        private void DataPlayBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PlayMusic(DataItemsList.Children[0] as DataItem, null);
        }

        private void DataDownloadBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            border5.Visibility = Visibility.Collapsed;
            DataDownloadPage.Visibility = Visibility.Visible;
            Download_Path.Text = Settings.USettings.DownloadPath;
            DownloadQx.IsChecked = true;
            DownloadQx.Content = "全不选";
            foreach (DataItem x in DataItemsList.Children) {
                x.MouseDown -= PlayMusic;
                x.NSDownload(true);
                x.Check();
            }
        }

        public void CloseDownloadPage() {
            border5.Visibility = Visibility.Visible;
            DataDownloadPage.Visibility = Visibility.Collapsed;
            foreach (DataItem x in DataItemsList.Children)
            {
                x.MouseDown += PlayMusic;
                x.NSDownload(false);
                x.Check();
            }
        }

        private void DataDownloadBtn_Copy_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseDownloadPage();
        }
        #endregion
        #region SearchMusic
        private int ixPlay = 1;
        private void Datasv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (Datasv.IsVerticalScrollBarAtButtom()&&isSearch) {
                ixPlay++;
                SearchMusic(SearchKey,ixPlay);
            }
        }
        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchBox.Text.Trim() != string.Empty)
            {
                if (Search_SmartBox.Opacity != 100)
                    Search_SmartBox.Opacity = 100;
                var data = await ml.Search_SmartBoxAsync(SearchBox.Text);
                Search_SmartBoxList.Items.Clear();
                if (data.Count == 0)
                    Search_SmartBox.Opacity = 0;
                else foreach (var dt in data)
                    Search_SmartBoxList.Items.Add(dt);
            }
            else Search_SmartBox.Opacity = 0;
        }
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter&&SearchBox.Text.Trim() != string.Empty)
            { SearchMusic(SearchBox.Text); ixPlay = 1; }
        }
        private void Search_SmartBoxList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Search_SmartBoxList.SelectedIndex != -1)
            {
                SearchBox.Text = Search_SmartBoxList.SelectedItem.ToString().Replace("歌曲:", "").Replace("歌手:", "").Replace("专辑:", "");
                Search_SmartBox.Opacity = 0;
                SearchMusic(SearchBox.Text); ixPlay = 1;
            }
        }
        private string SearchKey = "";
        public void SearchMusic(string key,int osx=0)
        {
            isSearch = true;
            SearchKey = key;
            OpenLoading();
            var xs = new Task(new Action(async delegate
            {
                List<Music> dt = null;
                if (osx == 0) Dispatcher.Invoke(() => { NSPage(null, Data); });
                if (osx==0)dt = await ml.SearchMusicAsync(key);
                else dt = await ml.SearchMusicAsync(key,osx);
                var file = Settings.USettings.CachePath + "Image\\Search" + key + ".jpg";
                if (!System.IO.File.Exists(file))
                {
                    var s = new WebClient();
                    s.DownloadFileAsync(new Uri(dt.First().ImageUrl), file);
                    s.DownloadFileCompleted += delegate { Dispatcher.Invoke(() => { TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource()); }); };
                }
                else Dispatcher.Invoke(() => { TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource()); });
                if(osx==0)
                   Dispatcher.Invoke(() => {
                    TB.Text = key;
                    DataItemsList.Children.Clear();
                });
            Dispatcher.Invoke(() => {
                foreach (var j in dt) {
                    var k = new DataItem(j) { Width = DataItemsList.ActualWidth };
                    if (k.isPlay(MusicName.Text))
                    {
                        k.ShowDx();
                        MusicData = k;
                    }
                    k.MouseDown += PlayMusic;
                    DataItemsList.Children.Add(k);
                }
            });
                Dispatcher.Invoke(() => {CloseLoading(); });
            }));
            xs.Start();
        }
        #endregion
        #region PlayMusic

        public void PlayMusic(object sender, MouseEventArgs e)
        {
            var dt = sender as DataItem;
            dt.ShowDx();
            MusicData = dt;
            PlayMusic(dt.ID, dt.Image, dt.SongName, dt.Singer);
        }
        public void PlayMusic(string id, string x, string name, string singer, bool isRadio = false, bool doesplay = true)
        {
            MusicName.Text = "";
            IsRadio = isRadio;
            Settings.USettings.Playing.GC = id;
            Settings.USettings.Playing.ImageUrl = x;
            Settings.USettings.Playing.MusicID = id;
            Settings.USettings.Playing.MusicName = name;
            Settings.USettings.Playing.Singer = singer;
            Settings.SaveSettings();
            if (Settings.USettings.MusicLike.ContainsKey(id))
                LikeBtnDown();
            else LikeBtnUp();
            if (!ml.mldata.ContainsKey(id))
                ml.mldata.Add(id, (name + " - " + singer).Replace("\\", "-").Replace("?", "").Replace("/", "").Replace(":", "").Replace("*", "").Replace("\"", "").Replace("<", "").Replace(">", "").Replace("|", ""));
            ml.GetAndPlayMusicUrlAsync(id, true, MusicName, this, isPos, doesplay);
            MusicImage.Background = new ImageBrush(new BitmapImage(new Uri(x)));
            Singer.Text = singer;
            if (doesplay)
            {
                (PlayBtn.Child as Path).Data = Geometry.Parse(Properties.Resources.Pause);
                t.Start();
            }
        }
        #endregion
        #region PlayControl
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsRadio)
                PlayMusic(DataItemsList.Children[DataItemsList.Children.IndexOf(MusicData) - 1] as DataItem, null);
        }
        private void Border_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            if (!IsRadio)
                PlayMusic(DataItemsList.Children[DataItemsList.Children.IndexOf(MusicData) + 1] as DataItem, null);
            else GetRadio(new RadioItem(RadioID), null);
        }
        private void PlayBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isplay)
            {
                isplay = false;
                ml.m.Pause();
                t.Stop();
                (PlayBtn.Child as Path).Data = Geometry.Parse(Properties.Resources.Play);
            }
            else
            {
                isplay = true;
                ml.m.Play();
                t.Start();
                (PlayBtn.Child as Path).Data = Geometry.Parse(Properties.Resources.Pause);
            }
        }
        private void jd_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ml.m.Position = TimeSpan.FromMilliseconds(jd.Value);
        }
        private void MusicImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ind == 0)
            {
                ind = 1;
                ControlDownPage.BorderThickness = new Thickness(0);
                (Resources["OpenLyricPage"] as Storyboard).Begin();
            }
            else
            {
                ind = 0;
                ControlDownPage.BorderThickness = new Thickness(0,1,0,0);
                (Resources["CloseLyricPage"] as Storyboard).Begin();
            }
        }
        private void XHBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (xh)
            {
                xh = false;
                (XHBtn.Child as Path).Data = Geometry.Parse(Properties.Resources.Lbxh);
            }
            else
            {
                xh = true;
                (XHBtn.Child as Path).Data = Geometry.Parse(Properties.Resources.Dqxh);
            }
        }
        #endregion
        #region Lyric
        private async void Border_MouseDown_3(object sender, MouseButtonEventArgs e)
        {
            if (m_Name.Visibility == Visibility.Visible)
            {
                m_Singer.Visibility = Visibility.Collapsed;
                m_Name.Visibility = Visibility.Collapsed;
                ly.Visibility = Visibility.Collapsed;
                pl.Visibility = Visibility.Visible;
                List<MusicPL> data;
                if (!isPos)
                    data = await ml.GetPLByQQAsync(Settings.USettings.Playing.MusicID);
                else
                    data = await ml.GetPLAsync(m_Name.Text + "-" + m_Singer.Text);
                pldata.Children.Clear();
                foreach (var dt in data)
                {
                    pldata.Children.Add(new PlControl(dt.img, dt.name, dt.text) { Width = pldata.ActualWidth - 100 });
                }
            }
            else
            {
                m_Singer.Visibility = Visibility.Visible;
                m_Name.Visibility = Visibility.Visible;
                ly.Visibility = Visibility.Visible;
                pl.Visibility = Visibility.Collapsed;
            }
        }
        private void ly_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ml.lv != null)
                ml.lv.RestWidth(e.NewSize.Width);
        }
        private void qq_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isPos = false;
            ml.GetAndPlayMusicUrlAsync(Settings.USettings.Playing.MusicID, true, MusicName, this, isPos);
        }

        private void wy_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isPos = true;
            ml.GetAndPlayMusicUrlAsync(Settings.USettings.Playing.MusicID, true, MusicName, this, isPos);
        }
        #endregion
        #region AddGD
        private void AddGDPage_qqmod_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!mod)
            {
                AddGDPage_qqmod.Effect = new DropShadowEffect() { BlurRadius = 10, Opacity = 0.4, ShadowDepth = 0 };
                AddGDPage_wymod.Effect = null;
                mod = true;
            }
        }

        private void AddGDPage_wymod_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (mod)
            {
                AddGDPage_qqmod.Effect = null;
                AddGDPage_wymod.Effect = new DropShadowEffect() { BlurRadius = 10, Opacity = 0.4, ShadowDepth = 0 };
                mod = false;
            }
        }

        private async void AddGDPage_DrBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (mod)
            {
                if (!Settings.USettings.MusicGD.ContainsKey(AddGDPage_id.Text))
                    Settings.USettings.MusicGD.Add(AddGDPage_id.Text, await ml.GetGDAsync(AddGDPage_id.Text));
            }
            else
            {
                if (!Settings.USettings.MusicGD.ContainsKey(AddGDPage_id.Text))
                    Settings.USettings.MusicGD.Add(AddGDPage_id.Text, await ml.GetGDbyWYAsync(AddGDPage_id.Text, this, AddGDPage_ps_name, AddGDPage_ps_jd));
            }
             (Resources["CloseAddGDPage"] as Storyboard).Begin();
            GDBtn_MouseDown(null, null);
        }
        #endregion
        #region Download

        private void ckFile_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var g = new System.Windows.Forms.FolderBrowserDialog();
            if (g.ShowDialog() == System.Windows.Forms.DialogResult.OK){
                Download_Path.Text = g.SelectedPath;
                Settings.USettings.DownloadPath= g.SelectedPath;
            }
            
        }

        private void cb_color_Click(object sender, RoutedEventArgs e)
        {
            var d = sender as CheckBox;
            if (d.IsChecked == true)
            {
                d.Content = "全不选";
                foreach (DataItem x in DataItemsList.Children)
                    x.Check();
            }
            else
            {
                d.Content = "全选";
                foreach (DataItem x in DataItemsList.Children)
                    x.Check();
            }
        }

        private async void DownloadBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var data = new List<DataItem>();
            foreach (var x in DataItemsList.Children)
            {
                var f = x as DataItem;
                if (f.isChecked == true)
                    data.Add(f);
            }
            CloseDownloadPage();
            Msg msg = new Msg("正在下载全部歌曲(" + data.Count + ")");
            msg.Show();
            var DTimer = new System.Windows.Forms.Timer();
            await DownloadTaskAsync(msg, data,DTimer);
            DTimer.Interval = 2000;
            DTimer.Tick +=async delegate {
                if (DownloadIndex != data.Count){
                    string name = data[DownloadIndex].SongName + " - " + data[DownloadIndex].Singer;
                    string file = Download_Path.Text + $"\\{name}.mp3";
                    System.IO.File.Delete(file);
                    await DownloadTaskAsync(msg, data, DTimer);
                }
            };
        }
        private int DownloadIndex=0;
        public async Task DownloadTaskAsync(Msg msg, List<DataItem> data, System.Windows.Forms.Timer dt) {
            dt.Start();
            string name = data[DownloadIndex].SongName + " - " + data[DownloadIndex].Singer;
            string file = Download_Path.Text + $"\\{name}.mp3";
            if (!System.IO.File.Exists(file))
            {
                var cl = new WebClient();
                string mid = data[DownloadIndex].ID;
                string url = await ml.GetUrlAsync(mid);
                msg.tb.Text = "正在下载全部歌曲(" + data.Count + ")\n正在下载:" + (DownloadIndex + 1) + "  " + name;
                cl.DownloadFileAsync(new Uri(url), file);
                cl.DownloadProgressChanged += (s, e) =>
                {
                    dt.Stop();
                    msg.tb.Text = "正在下载全部歌曲(" + data.Count + ")\n正在下载：(" + e.ProgressPercentage + "%)" + (DownloadIndex + 1) + "  " + name;
                };
                cl.DownloadFileCompleted += async delegate
                {
                    if (!msg.IsClose)
                    {
                        if (DownloadIndex != data.Count)
                        {
                            DownloadIndex++;
                            await DownloadTaskAsync(msg, data,dt);
                        }
                        if (DownloadIndex + 1 == data.Count) {
                            if (!msg.IsClose)
                            {
                                msg.tb.Text = "已完成.";
                                await Task.Delay(5000);
                                msg.tbclose();
                            }
                            else
                            {
                                await Task.Delay(2000);
                                Msg msxg = new Msg("已取消下载");
                                msxg.Show();
                                await Task.Delay(5000);
                                msxg.tbclose();
                            }
                        }
                    }
                    cl.Dispose();
                };
            }
            else {
                DownloadIndex++;
                await DownloadTaskAsync(msg, data,dt);
            }
        }

        #endregion
        #region User
        #region Login
        private void LoginPage_Close_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var da = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3));
            da.Completed += delegate { LoginPage.Visibility = Visibility.Collapsed; };
            LoginPage.BeginAnimation(OpacityProperty, da);
        }
        private async void UserTX_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var data = System.IO.Directory.GetDirectories(Environment.ExpandEnvironmentVariables(@"%AppData%\Tencent\Users"));
            Login_wp.Children.Clear();
            foreach (var dt in data)
            {
                string qqx = new System.IO.DirectoryInfo(dt).Name;
                string dl = await HttpHelper.GetWebAsync($"https://c.y.qq.com/rsc/fcgi-bin/fcg_get_profile_homepage.fcg?loginUin={qqx}&hostUin=0&format=json&inCharset=utf8&outCharset=utf-8&notice=0&platform=yqq&needNewCode=0&cid=205360838&ct=20&userid={qqx}&reqfrom=1&reqtype=0", Encoding.UTF8);
                string name = JObject.Parse(dl)["data"]["creator"]["nick"].ToString();
                await HttpHelper.HttpDownloadFileAsync($"http://q2.qlogo.cn/headimg_dl?bs=qq&dst_uin={qqx}&spec=100", Settings.USettings.CachePath + qqx + ".jpg");
                var image = new System.Drawing.Bitmap(Settings.USettings.CachePath + qqx + ".jpg");
                UserTxControl utc = new UserTxControl(new ImageBrush(image.ToImageSource()), name, qqx);
                utc.MouseDown += (s, ex) => {
                    Settings.SaveSettings();
                    Settings.USettings.MusicGD.Clear();
                    string qq = utc.qq;
                    Settings.LoadUSettings(qq);
                    Settings.USettings.UserName = utc.UserName.Text;
                    Settings.USettings.UserImage = Settings.USettings.CachePath + qq + ".jpg";
                    Settings.USettings.LemonAreeunIts = qq;
                    Settings.SaveSettings();
                    Settings.LSettings.qq = qq;
                    Settings.SaveLocaSettings();
                    ml.m.Stop();
                    ml = null;
                    var a = new MainWindow();
                    a.Show();
                    this.Close();
                };
                Login_wp.Children.Add(utc);
            }
            LoginPage.Visibility = Visibility.Visible;
            LoginPage.BeginAnimation(OpacityProperty, new DoubleAnimation(0.5, 1, TimeSpan.FromSeconds(0.3)));
        }
        #endregion
        #region MyGD
        private void GDBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NSPage(GDBtn, MyGDIndexPage);
            GDItemsList.Children.Clear();
            foreach (var jm in Settings.USettings.MusicGD)
            {
                var ks = new FLGDIndexItem(jm.Key, jm.Value.name, jm.Value.pic) { Margin = new Thickness(20, 0, 0, 20) };
                ks.MouseDown += FxGDMouseDown;
                GDItemsList.Children.Add(ks);
            }
            UIHelper.G(Page);
        }

        private void FxGDMouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenLoading();
            var sx = new Task(new Action(async delegate
            {
                var dt = sender as FLGDIndexItem;
                Dispatcher.Invoke(() =>
                {
                    NSPage(null, Data);
                    OpenLoading();
                    TB.Text = dt.name.Text;
                    DataItemsList.Children.Clear();
                });
                await Task.Delay(500);
                var file = Settings.USettings.CachePath + "Image\\GD" + dt.id + ".jpg";
                if (!System.IO.File.Exists(file))
                {
                    var s = new WebClient();
                    s.DownloadFileAsync(new Uri(dt.img), file);
                    s.DownloadFileCompleted += delegate { Dispatcher.Invoke(() => { TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource()); }); };
                }
                else Dispatcher.Invoke(() => { TXx.Background = new ImageBrush(new System.Drawing.Bitmap(file).ToImageSource()); });
                Dispatcher.Invoke(() =>
                {
                    TB.Text = dt.name.Text;
                    DataItemsList.Children.Clear();
                });
                foreach (var j in Settings.USettings.MusicGD[dt.id].Data)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var k = new DataItem(j) { Width = DataItemsList.ActualWidth };
                        k.MouseDown += PlayMusic;
                        if (k.isPlay(MusicName.Text)) {
                            k.ShowDx();
                            MusicData = k;
                        }
                        DataItemsList.Children.Add(k);
                    });
                    await Task.Delay(1);
                }
                isSearch = false;
                Dispatcher.Invoke(() => { CloseLoading(); });
            }));
            sx.Start();
        }
        #endregion
        #endregion

        #endregion
        #region 快捷键
        private void LoadHotDog() {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            RegisterHotKey(handle, 124, 1, (uint)System.Windows.Forms.Keys.L);
            RegisterHotKey(handle, 125, 1, (uint)System.Windows.Forms.Keys.S);
            InstallHotKeyHook(this);
            Closed += (s, e) => {
                IntPtr hd = new WindowInteropHelper(this).Handle;
                UnregisterHotKey(hd, 124);
                UnregisterHotKey(hd, 125);
            };

            //notifyIcon
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Text = "小萌";
            notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            notifyIcon.Visible = true;
            //打开菜单项
            System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("打开");
            open.Click += delegate { exShow(); };
            //退出菜单项
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("关闭");
            exit.Click += delegate {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                var dt = Resources["Closing"] as Storyboard;
                dt.Completed += delegate { Settings.SaveSettings(); Environment.Exit(0); };
                dt.Begin();
            };
            //关联托盘控件
            System.Windows.Forms.MenuItem[] childen = new System.Windows.Forms.MenuItem[] { open, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);

            notifyIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler((o, m) =>
            {
                if (m.Button == System.Windows.Forms.MouseButtons.Left) exShow();
            });
        }

        [DllImport("user32")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint controlKey, uint virtualKey);

        [DllImport("user32")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        public bool InstallHotKeyHook(Window window)
        {
            if (window == null)
                return false;
            WindowInteropHelper helper = new WindowInteropHelper(window);
            if (IntPtr.Zero == helper.Handle)
                return false;
            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            if (source == null)
                return false;
            source.AddHook(HotKeyHook);
            return true;
        }
        private IntPtr HotKeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == 124)
                    exShow();
                else if (wParam.ToInt32() == 125)
                    new SearchWindow().Show();
            }
            return IntPtr.Zero;
        }
        private const int WM_HOTKEY = 0x0312;
        #endregion
        #region 进程通信
        private void LoadSEND_SHOW() {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hwnd);
            if (source != null) source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg ==MsgHelper.WM_COPYDATA)
            {
                MsgHelper.COPYDATASTRUCT cdata = new MsgHelper.COPYDATASTRUCT();
                Type mytype = cdata.GetType();
                cdata = (MsgHelper.COPYDATASTRUCT)Marshal.PtrToStructure(lParam, mytype);
                if (cdata.lpData == MsgHelper.SEND_SHOW)
                    exShow();
            }
            return IntPtr.Zero;
        }
        #endregion
    }
}