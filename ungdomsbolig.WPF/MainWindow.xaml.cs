using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
using ungdomsbolig;
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
            //lblStatus
            _client = SearchHelper.GetLoggedInClient(creds);

            lblName.Content = "Name";
            var waitingList = await GetAndSaveWaitingListAsync();
            lblWaitingList.Content = waitingList.Count();

            LoginPanel.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Visible;
        }

        private async Task<IEnumerable<ILivable>> GetAndSaveWaitingListAsync()
        {
            var waitingList = await SearchHelper.GetAllPagesAndParseAsync(_client, "/user/apartments");
            SearchHelper.SaveJson(waitingList, "waitingList.json");
            return waitingList;
        }

        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchResults = await GetAndSaveSearchResultsAsync();
            lvHouses.ItemsSource = searchResults;
        }

        private async Task<IEnumerable<ILivable>> GetAndSaveSearchResultsAsync()
        {
            var filename = "searchResults.json";

            var force = cbForce.IsChecked ?? false;
            if (!force && File.Exists(filename)){
                var json = File.ReadAllText(filename);
                return JsonSerializer.Deserialize<List<House>>(json).Select(h => (ILivable)h);
            }

            //https://www.wpf-tutorial.com/listview-control/listview-filtering/
            var waitingList = await SearchHelper.GetAllPagesAndParseAsync(_client, "/search");
            SearchHelper.SaveJson(waitingList, filename);
            return waitingList;
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
    }
}
