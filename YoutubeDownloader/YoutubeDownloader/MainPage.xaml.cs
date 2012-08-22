using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using System.Xml.Linq;
using System.ComponentModel;
using System.Windows.Navigation;
using Microsoft.Phone.Tasks;
using System.IO.IsolatedStorage;
using System.IO;

namespace YoutubeDownloader
{
    public class partial_download
    {
        public string id { get; set; }
        public string name { get; set; }
        public string status { get; set; }
        public double recieved { get; set; }
        public string url { get; set; }
    }
    public class YouTubeVideo
    {
        public string Title { get; set; }
        public string VideoImageUrl { get; set; }
        public string VideoId { get; set; }
        public string id { get; set; }
        
    }
    public class tmp_files {
        public string t1 { get; set; }
        public string t2 { get; set; }
    }
    public partial class MainPage : PhoneApplicationPage
    {
        public List<partial_download> downloads;
        public MainPage()
        {

            downloads = new List<partial_download>();

            InitializeComponent();
            fillDownloads();
        }

        public void fillDownloads() {
            var isolatedStorageFile = IsolatedStorageFile.GetUserStoreForApplication();
            var files = isolatedStorageFile.GetFileNames("*.mp4");
            
            Downloaded_List.ItemsSource = null;
            Downloaded_List.ItemsSource = files;
        }
        private bool oldState  = false;
        protected override void OnBackKeyPress(CancelEventArgs e)
        {
            if (Microsoft.Phone.Shell.SystemTray.ProgressIndicator != null)
            {
                Microsoft.Phone.Shell.SystemTray.ProgressIndicator.IsVisible = false;
            }
            if (oldState)
            {
                e.Cancel = true;
            }
            oldState = false;
            base.OnBackKeyPress(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (Microsoft.Phone.Shell.SystemTray.ProgressIndicator != null)
            {
                Microsoft.Phone.Shell.SystemTray.ProgressIndicator.IsVisible = false;
            }
            oldState = false;
        }
        private void btn_stream(object sender, RoutedEventArgs k) {
            var but = sender as Button;
            var id = but.Tag.ToString();
            var url = "http://aen1.herokuapp.com/stream/http://www.youtube.com/watch?v=" + id;
            var launcher = new MediaPlayerLauncher
            {
                Controls = MediaPlaybackControls.All,
                Media = new Uri(url, UriKind.Absolute)
            };
            launcher.Show();
            oldState = true;

        }
        private void btn_download(object sender, RoutedEventArgs e) {
            var but = sender as Button;
            var vid = but.Tag as YouTubeVideo;
            partial_download a = new partial_download();
            a.name = vid.Title + ".mp4";
            a.status = " Downloading";
            a.recieved = 0;
            a.id = vid.id;
            a.url = "http://aen1.herokuapp.com/stream/http://www.youtube.com/watch?v=" + a.id;
            downloads.Add(a);
            WebClient webClient = new WebClient();
            
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler((sen, k) => {
                
                double downloaded = Math.Round(((double)k.BytesReceived) / 1000000, 2);
                bool found = false;
                for (int i = 0; i < downloads.Count; i++)
                {
                    if (downloads[i].url == a.url) {
                        downloads[i].recieved = downloaded;
                        found = true;
                        break;
                    }
                }
                if (found == false) webClient.CancelAsync();
                else
                {
                    TransferListBox.ItemsSource = null;
                    TransferListBox.ItemsSource = downloads;
                }
            });
            webClient.OpenReadCompleted += new OpenReadCompletedEventHandler((sen, k) => {
                int i = 0;
                fillDownloads();
                if (k.Error != null)
                {
                    MessageBox.Show("Download was interrupted as internet connection was lost, please try again with internet connectivity");
                    for (i = 0; i < downloads.Count; i++)
                    {
                        if (downloads[i].url == a.url)
                        {
                            downloads.RemoveAt(i);
                            break;
                        };
                    }
                }
                else
                {

                    partial_download tmp = null;

                    for (i = 0; i < downloads.Count; i++)
                    {
                        if (downloads[i].url == a.url)
                        {
                            tmp = new partial_download();
                            downloads[i].status = "Download Finished";
                            tmp = downloads[i];
                            break;
                        };
                    }
                    if (tmp != null)
                    {
                        try
                        {
                            if (k.Result != null)
                            {
                                var isolatedStorageFile = IsolatedStorageFile.GetUserStoreForApplication();

                                bool checkQuotaIncrease = IncreaseIsolatedStorageSpace(k.Result.Length);

                                string VideoFile = tmp.name;
                                var isolatedStorageFileStream = new IsolatedStorageFileStream(VideoFile, FileMode.Create, isolatedStorageFile);
                                long VideoFileLength = (long)k.Result.Length;
                                byte[] byteImage = new byte[VideoFileLength];
                                k.Result.Read(byteImage, 0, byteImage.Length);
                                isolatedStorageFileStream.Write(byteImage, 0, byteImage.Length);

                                isolatedStorageFileStream.Close();

                            }
                            TransferListBox.ItemsSource = null;
                            TransferListBox.ItemsSource = downloads;
                            mainPivot.SelectedItem = download;
                        }
                        catch (Exception ex)
                        {
                            var message = ex.Message;
                            MessageBox.Show(message);

                        }
                        fillDownloads();
                    }
                }
            });
            webClient.OpenReadAsync(new Uri(a.url));
            TransferListBox.ItemsSource = null;
            TransferListBox.ItemsSource = downloads;
            mainPivot.SelectedItem = progress;
        }
        protected bool IncreaseIsolatedStorageSpace(long quotaSizeDemand)
        {
            bool CanSizeIncrease = false;
            IsolatedStorageFile isolatedStorageFile = IsolatedStorageFile.GetUserStoreForApplication();
            //Get the Available space
            long maxAvailableSpace = isolatedStorageFile.AvailableFreeSpace;
            if (quotaSizeDemand > maxAvailableSpace)
            {
                if (!isolatedStorageFile.IncreaseQuotaTo(isolatedStorageFile.Quota + quotaSizeDemand))
                {
                    CanSizeIncrease = false;
                    return CanSizeIncrease;
                }
                CanSizeIncrease = true;
                return CanSizeIncrease;
            }
            return CanSizeIncrease;
        }
        private void remove_file(object sender, RoutedEventArgs e) {
            var but = sender as Button;
            var url = but.Tag.ToString();
            var isolatedStorageFile = IsolatedStorageFile.GetUserStoreForApplication();
            isolatedStorageFile.DeleteFile(url);
            fillDownloads();
        }
        private void remove_download(object sender, RoutedEventArgs e) {
            var but = sender as Button;
            var url = but.Tag.ToString();
            for (int i = 0; i < downloads.Count; i++)
            {
                if (downloads[i].url == url)
                {
                    downloads.RemoveAt(i);
                    break;
                }
            }
            TransferListBox.ItemsSource = null;
            TransferListBox.ItemsSource = downloads;
            fillDownloads();

        }
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wc = new WebClient();
                wc.DownloadStringCompleted += DownloadStringCompleted;
                

                var searchUri = string.Format(
                  "http://gdata.youtube.com/feeds/api/videos?q={0}&format=6",
                  HttpUtility.UrlEncode(SearchText.Text));
                wc.DownloadStringAsync(new Uri(searchUri));
            }
            catch (Exception k) {
                MessageBox.Show("Error occurred, check weather you are connected to internet or not");
            }
        }
        private void PlayVid_bt(object sender, RoutedEvent e) {
            var but = sender as Button;
            var id = but.Tag.ToString();
            var launcher = new MediaPlayerLauncher
            {
                Controls = MediaPlaybackControls.All,
                Media = new Uri(id, UriKind.Relative)
            };
            launcher.Show();
            oldState = true;
        }
        void DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show("Please connect to internet and try again");
            }
            else
            {
                var atomns = XNamespace.Get("http://www.w3.org/2005/Atom");
                var medians = XNamespace.Get("http://search.yahoo.com/mrss/");

                var xml = XElement.Parse(e.Result);
                var videos = (
                  from entry in xml.Descendants(atomns.GetName("entry"))
                  select new YouTubeVideo
                  {
                      VideoId = entry.Element(atomns.GetName("id")).Value,
                      id = entry.Element(atomns.GetName("id")).Value.Split('/').Last(),
                      VideoImageUrl = (
                        from thumbnail in entry.Descendants(medians.GetName("group")).Elements(medians.GetName("thumbnail"))
                        select thumbnail.Attribute("url").Value).FirstOrDefault(),
                      Title = entry.Element(atomns.GetName("title")).Value
                  }).ToArray();
                for (var i = 0; i < videos.Length; i++)
                {
                    if (videos[i].Title.Length > 25) videos[i].Title = videos[i].Title.Substring(0, 25) + " ...";
                }
                ResultsList.ItemsSource = videos;
            }
        }
        private void DownloadSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Downloaded_List.SelectedIndex != -1)
            {
                var str = Downloaded_List.SelectedItem as string;
                if (str != null)
                {
                    try
                    {
                        var launcher = new MediaPlayerLauncher
                        {
                            Controls = MediaPlaybackControls.All,
                            Media = new Uri(str, UriKind.Relative)
                        };
                        launcher.Show();
                        oldState = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }

                }
                else MessageBox.Show("No video selected");
                Downloaded_List.SelectedIndex = -1;
            }
        }
        private void VideoListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TransferListBox.SelectedIndex != -1)
            {
                var video = TransferListBox.SelectedItem as partial_download;
                if (video != null)
                {
                    try
                    {
                        var launcher = new MediaPlayerLauncher
                        {
                            Controls = MediaPlaybackControls.All,
                            Media = new Uri(video.name, UriKind.Relative)
                        };
                        launcher.Show();
                        oldState = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("File currupted, deleting it now, do not do anything while downloading, not even watching another video as this causes connection reset");
                        for (var i = 0; i < downloads.Count; i++) {
                            if (downloads[i].id == video.id) {
                                downloads.RemoveAt(i);

                            }
                        }
                        TransferListBox.ItemsSource = null;
                        TransferListBox.ItemsSource = downloads;
                    }


                }
                else MessageBox.Show("video does not exist");
                TransferListBox.SelectedIndex = -1;
            }
        }
    }
}