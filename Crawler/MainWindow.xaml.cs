using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.IconPacks;
using static Crawler.csHelperMethods;
using System.Data.Entity;
using System.Runtime.Remoting.Contexts;
using System.Web.UI.WebControls;
using System.Web.Configuration;
using System.Windows.Threading;

namespace Crawler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {


        private void LaunchGitHubSite(object sender, RoutedEventArgs e)
        {
            var destinationurl = "https://github.com/BilginBurak";
            var sInfo = new System.Diagnostics.ProcessStartInfo(destinationurl)
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }








        private static int _irNumberOfTotalConcurrentCrawling = 20;
        private static int _irMaximumTryCount = 3;


        private ObservableCollection<string> _Results = new ObservableCollection<string>();
        private ObservableCollection<string> _Summary = new ObservableCollection<string>();

        public ObservableCollection<string> UserLogs
        {
            get { return _Results; }
            set
            {
                _Results = value;
            }
        }
        public ObservableCollection<string> LogSummary
        {
            get { return _Summary; }
            set
            {
                _Summary = value;
            }
        }

 

        public MainWindow()
        {
            InitializeComponent();
            //threadpools allows you to start as many as threads you want immediately
            ThreadPool.SetMaxThreads(100000, 100000);
            ThreadPool.SetMinThreads(100000, 100000);
            ServicePointManager.DefaultConnectionLimit = 1000;//this increases your number of connections to per host at the same time
            listBoxResults.ItemsSource = UserLogs;
            lstUserLogs.ItemsSource = LogSummary;
            fillFromDbDropdown();
        }




        private void fillFromDbDropdown()
        {
            dropFavSelector.Items.Clear();
            using (DBCrawling db = new DBCrawling())
            {
                try
                {
                    var favUrlList = db.tblFavUrls.ToList();
                    foreach (var fav in favUrlList)
                        dropFavSelector.Items.Add(fav.favUrls);
                    txtInputUrl.Text = string.Empty;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }






        DateTime dtStartDate;


        private static bool isCrawlPaused = false;
        private System.Windows.Threading.DispatcherTimer dispatcherTimer;

        private void checkingTimer()
        {
            if (isCrawlPaused)
            {
                // Crawl işlemi duraklatıyor
                isCrawlPaused = false;
                btnStartInitial.Content = "Continue";
                btnStartInitial.Background = new SolidColorBrush(Color.FromArgb(10, 255, 0, 0));  // butonun rengini değiştirebilmek için
                dispatcherTimer.Stop(); // Timer'ı duraklat

            }
            else
            {
                
                //burası crawl işlemini başlatıyor

                isCrawlPaused = true;
                btnStartInitial.Content = "Pause";
                btnStartInitial.Background = new SolidColorBrush(Color.FromArgb(10, 0,255, 0));  // butonun rengini değiştirebilmek için

                dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                dispatcherTimer.Tick += new EventHandler(startPollingAwaitingURLs);
                dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
                dispatcherTimer.Start(); // Timer'ı başlat


            }
        }

        private void clearDBandStart(object sender, RoutedEventArgs e)
        {

            if (txtInputUrl.Text.IsValidUrl() == false)
            {
                MessageBox.Show("please enter a valid url");
                return;
            }
            else
            {
                dtStartDate = DateTime.Now;
                crawlPage(txtInputUrl.Text.normalizeUrl(), 0, txtInputUrl.Text.normalizeUrl(), DateTime.Now);
                checkingTimer();

            }

        }











        private static object _lock_CrawlingSync = new object();
        private static bool blBeingProcessed = false;
        private static List<Task> lstCrawlingTasks = new List<Task>();
        private static List<string> lstCurrentlyCrawlingUrls = new List<string>();

        private void startPollingAwaitingURLs(object sender, EventArgs e)
        {
            lock (LogSummary)
            {
                string srPerMinCrawlingspeed = (irCrawledUrlCount.ToDouble() / (DateTime.Now - dtStartDate).TotalMinutes).ToString("N2");

                string srPerMinDiscoveredLinkSpeed = (irDiscoveredUrlCount.ToDouble() / (DateTime.Now - dtStartDate).TotalMinutes).ToString("N2");

                string srPassedTime = (DateTime.Now - dtStartDate).TotalMinutes.ToString("N2");
                LogSummary.Clear();
                LogSummary.Insert(0, $"Total Time: {srPassedTime} Minutes \t Total Crawled Links Count: {irCrawledUrlCount.ToString("N0")}");
                LogSummary.Insert(0, $"{DateTime.Now} polling awaiting urls \t processing: {blBeingProcessed} \t Number of crawling tasks: {lstCrawlingTasks.Count}");
                LogSummary.Insert(0, $"Crawling Speed Per Minute: {srPerMinCrawlingspeed} \t Total Discovered Links : {irDiscoveredUrlCount.ToString("N0")} \t Discovered Url Speed: {srPerMinDiscoveredLinkSpeed} ");
            }

            logMesssage($"polling awaiting urls \t processing: {blBeingProcessed} \t number of crawling tasks: {lstCrawlingTasks.Count}");

            if (blBeingProcessed)
                return;

            lock (_lock_CrawlingSync)
            {
                blBeingProcessed = true;

                lstCrawlingTasks = lstCrawlingTasks.Where(pr => pr.Status != TaskStatus.RanToCompletion && pr.Status != TaskStatus.Faulted).ToList();

                int irTasksCountToStart = _irNumberOfTotalConcurrentCrawling - lstCrawlingTasks.Count;

                if (irTasksCountToStart > 0)
                    using (DBCrawling db = new DBCrawling())
                    {
                        var vrReturnedList = db.tblMainUrls.Where(x => x.IsCrawled == false && x.CrawlTryCounter < _irMaximumTryCount)
                                  .OrderBy(pr => pr.DiscoverDate)
                            .Select(x => new
                            {
                                x.Url,
                                x.LinkDepthLevel
                            }).Take(irTasksCountToStart * 2).ToList();

                        logMesssage(string.Join(" , ", vrReturnedList.Select(pr => pr.Url)));

                        foreach (var vrPerReturned in vrReturnedList)
                        {
                            var vrUrlToCrawl = vrPerReturned.Url;
                            int irDepth = vrPerReturned.LinkDepthLevel;
                            lock (lstCurrentlyCrawlingUrls)
                            {
                                if (lstCurrentlyCrawlingUrls.Contains(vrUrlToCrawl))
                                {
                                    logMesssage($"bypass url since already crawling: \t {vrUrlToCrawl}");
                                    continue;
                                }
                                lstCurrentlyCrawlingUrls.Add(vrUrlToCrawl);
                            }

                            logMesssage($"starting crawling url: \t {vrUrlToCrawl}");

                            lock (UserLogs)
                            {
                                UserLogs.Insert(0, $"{DateTime.Now} starting crawling url: \t {vrUrlToCrawl}");
                            }

                            var vrStartedTask = Task.Factory.StartNew(() => { crawlPage(vrUrlToCrawl, irDepth, null, DateTime.MinValue); }).ContinueWith((pr) =>
                            {

                                lock (lstCurrentlyCrawlingUrls)
                                {
                                    lstCurrentlyCrawlingUrls.Remove(vrUrlToCrawl);
                                    logMesssage($"removing url from list since task completed: \t {vrUrlToCrawl}");
                                }

                            });
                            lstCrawlingTasks.Add(vrStartedTask);

                            if (lstCrawlingTasks.Count > _irNumberOfTotalConcurrentCrawling)
                                break;
                        }
                    }

                blBeingProcessed = false;
            }
        }





        private void btnCleanTxt_Click(object sender, RoutedEventArgs e)
        {
            fillFromDbDropdown();
            txtInputUrl.Focus();
        }



        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            using (DBCrawling db = new DBCrawling())
            {
                try
                {
                    csHelperMethods.clearDatabase();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }



        private void btnTest_Click(object sender, RoutedEventArgs e)
        {


            using (DBCrawling db = new DBCrawling())
            {
                try
                {
                    db.tblMainUrls.Add(new tblMainUrl { Url = "www.toros.edu.tr", ParentUrlHash = "www.toros.edu.tr", SourceCode = "gg", UrlHash = "ww", DiscoverDate = DateTime.Today, LinkDepthLevel = 0, LastCrawlingDate = DateTime.Now, FetchTimeMS = 1, CompressionPercent = 1, IsCrawled = true, HostUrl = "ww", CrawlTryCounter = 2 });
                    db.SaveChanges();
                    MessageBox.Show("Database Connection Succesfully");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }



        }



        private void btnAddFav_Click(object sender, RoutedEventArgs e)
        {
            if (txtInputUrl.Text.IsValidUrl() == false)
            {
                MessageBox.Show("please enter a valid url");
                return;
            }
            else
                using (DBCrawling db = new DBCrawling())
                {
                    try
                    {
                        db.tblFavUrls.Add(new tblFavUrls { favUrls = txtInputUrl.Text });
                        db.SaveChanges();
                        fillFromDbDropdown();

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }






        }











        private void btnClearFav_Click(object sender, RoutedEventArgs e)
        {

            using (DBCrawling db = new DBCrawling())
            {
                try
                {
                    string selectedUrl = dropFavSelector.SelectedItem.ToString();

                    tblFavUrls urlToRemove = db.tblFavUrls.FirstOrDefault(url => url.favUrls == selectedUrl);
                    if (urlToRemove != null)
                    {
                        db.tblFavUrls.Remove(urlToRemove);
                        db.SaveChanges();
                        fillFromDbDropdown();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }


                /*

                try
                {


                    db.tblFavUrls.Remove(dropFavSelector.SelectedValue as tblFavUrls);

                    db.SaveChanges();
                    fillFromDbDropdown();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                */
            }

        }







        private void dropFavSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dropFavSelector.SelectedItem != null)
                try
                {

                    string selectedValue = dropFavSelector.SelectedValue.ToString();
                    txtInputUrl.Text = selectedValue;

                }
                catch (Exception ex)
                {
                    txtInputUrl.Text = (ex.Message);
                }
        }
    }
}
