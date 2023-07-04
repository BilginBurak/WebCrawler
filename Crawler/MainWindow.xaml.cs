﻿using MahApps.Metro.Controls;
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
        public ObservableCollection<string> UserLogs
        {
            get { return _Results; }
            set
            {
                _Results = value;
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
        }

        DateTime dtStartDate;

      

        private void clearDBandStart(object sender, RoutedEventArgs e)
        {
            dtStartDate = DateTime.Now;
            // clearDatabase();
            crawlPage(txtInputUrl.Text.normalizeUrl(), 0, txtInputUrl.Text.normalizeUrl(), DateTime.Now);
            checkingTimer();
        }

        private void checkingTimer()
        {
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(startPollingAwaitingURLs);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            dispatcherTimer.Start();
        }

        private static object _lock_CrawlingSync = new object();
        private static bool blBeingProcessed = false;
        private static List<Task> lstCrawlingTasks = new List<Task>();
        private static List<string> lstCurrentlyCrawlingUrls = new List<string>();

        private void startPollingAwaitingURLs(object sender, EventArgs e)
        {
            lock (UserLogs)
            {
                string srPerMinCrawlingspeed = (irCrawledUrlCount.ToDouble() / (DateTime.Now - dtStartDate).TotalMinutes).ToString("N2");

                string srPerMinDiscoveredLinkSpeed = (irDiscoveredUrlCount.ToDouble() / (DateTime.Now - dtStartDate).TotalMinutes).ToString("N2");

                string srPassedTime = (DateTime.Now - dtStartDate).TotalMinutes.ToString("N2");

                UserLogs.Insert(0, $"{DateTime.Now} polling awaiting urls \t processing: {blBeingProcessed} \t number of crawling tasks: {lstCrawlingTasks.Count}");

                UserLogs.Insert(0, $"Total Time: {srPassedTime} Minutes \t Total Crawled Links Count: {irCrawledUrlCount.ToString("N0")} \t Crawling Speed Per Minute: {srPerMinCrawlingspeed} \t Total Discovered Links : {irDiscoveredUrlCount.ToString("N0")} \t Discovered Url Speed: {srPerMinDiscoveredLinkSpeed} ");
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

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {

        }



        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            using (DBCrawling db = new DBCrawling())
            {
                try
                {
                    csHelperMethods.clearDatabase();

                }
                catch(Exception ex) {
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
                    csHelperMethods.clearDatabase();
                    db.tblMainUrls.Add(new tblMainUrl { Url = "www.toros.edu.tr", ParentUrlHash = "www.toros.edu.tr", SourceCode = "gg", UrlHash = "ww", DiscoverDate = DateTime.Today, LinkDepthLevel = 0, LastCrawlingDate = DateTime.Now, FetchTimeMS= 1, CompressionPercent = 1, IsCrawled = true, HostUrl = "ww", CrawlTryCounter = 2 });
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

        }




        private void btnClearFav_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
