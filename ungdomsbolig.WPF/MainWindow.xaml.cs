using CsvHelper;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using ungdomsbolig.Shared;

namespace ungdomsbolig.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private RestClient _client;
        private string _credentialsFileName = "credentials.json";
        private string _searchResultsFileName = "searchResults.json";
        private string _csvFileName = "googlemymaps.csv";
        private List<ILivable> _waitingList;
        public MainWindow()
        {
            InitializeComponent();            
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_credentialsFileName))
            {
                var json = File.ReadAllText(_credentialsFileName);
                var creds = JsonSerializer.Deserialize<Credentials>(json);
                await LoginWithCredentials(creds);
            }
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {         
            var name = tbLoginName.Text;
            var password = tbLoginPassword.Password;
            var creds = new Credentials() { Name = name, Password = password };
            var remember = cbRemember.IsChecked ?? false;
            if(remember) SearchHelper.SaveJson(creds, _credentialsFileName);

            await LoginWithCredentials(creds);            
        }

        private async Task LoginWithCredentials(Credentials creds)
        {
            lblStatus.Content = "Logging in..";
            var (client, name) = SearchHelper.GetLoggedInClientAndName(creds);
            _client = client;

            lblName.Content = name;
            await UpdateWaitingList();
            lblWaitingList.Content = _waitingList.Count();

            LoginPanel.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Visible;
            lblStatus.Content = "Logged in";
        }

        private async Task UpdateWaitingList()
        {
            _waitingList = await SearchHelper.GetAllPagesAndParseAsync(_client, "/user/apartments");
        }

        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Content = "Retrieving houses..";
            var searchResults = await GetAndSaveSearchResultsAsync();
            lvHouses.ItemsSource = searchResults;
            lblStatus.Content = "Houses retrieved";
        }

        private async Task<IEnumerable<ILivable>> GetAndSaveSearchResultsAsync()
        {
            var waitingList = cbWaitingList.IsChecked ?? false;
            var force = cbForce.IsChecked ?? false;

            if (waitingList)
            {
                if(force) await UpdateWaitingList();
                return _waitingList;
            }
            
            if (!force && File.Exists(_searchResultsFileName)){
                var json = File.ReadAllText(_searchResultsFileName);
                return JsonSerializer.Deserialize<List<House>>(json).Select(h => (ILivable)h);
            }                       

            var searchResults = await SearchHelper.GetAllPagesAndParseAsync(_client, "/search");
            SearchHelper.SaveJson(searchResults, _searchResultsFileName);
            return searchResults;
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var url = ((Hyperlink)sender).NavigateUri.AbsoluteUri;
            var ps = new ProcessStartInfo(url)
            {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
            e.Handled = true;
        }

        private void FilterSearch(object sender, RoutedEventArgs e) => UpdateFiltering();

        private void UpdateFiltering()
        {
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lvHouses.ItemsSource);
            if (view == null) return;
            view.Filter = s => (FilterRent(s) && FilterSize(s) && FilterExcludeWaitingList(s));
        }

        private bool FilterRent(object item)
        {
            int rent;
            if (string.IsNullOrEmpty(tbSearchRent.Text) || !int.TryParse(tbSearchRent.Text, out rent))
                return true;
            return (item as House).Rent < rent;
        }

        private bool FilterSize(object item)
        {
            int size;
            if (string.IsNullOrEmpty(tbSearchSize.Text) || !int.TryParse(tbSearchSize.Text, out size))
                return true;
            return (item as House).Size > size;
        }

        private bool FilterExcludeWaitingList(object item)
        {
            var exclude = cbSearchExcludeWaiting.IsChecked ?? false;
            if (!exclude)
                return true;
            return !_waitingList.Contains((item as House));
        }

        private void GenerateCSV(object sender, RoutedEventArgs e)
        {
            var openFolder = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            lblStatus.Content = "Generating CSV..";
            var test = lvHouses.Items.Cast<House>();//.Select(h => { h.Rent = Convert.ToInt32(h.Rent); return h; });
            var culture = CultureInfo.CreateSpecificCulture(CultureInfo.InvariantCulture.Name);
            culture.NumberFormat = NumberFormatInfo.CurrentInfo;
            using (var writer = new StreamWriter(_csvFileName))
            using (var csv = new CsvWriter(writer, culture))
            {
                csv.WriteRecords(test);
            }            
            lblStatus.Content = "CSV generated";

            if (!openFolder) return;
            var folder = Path.GetFullPath(_csvFileName);
            Process.Start("explorer.exe", "/select, " + folder);
        }
    }
}
