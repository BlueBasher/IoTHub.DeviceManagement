namespace IoTHub.Device
{
    using Microsoft.Azure.Devices.Client;
    using Common.Models;
    using Newtonsoft.Json;
    using System.IO;
    using System.Net;
    using System.Text;
    using Windows.Storage;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using System;
    using System.Threading.Tasks;
    using IotHub.Common.Models;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DeviceClient _deviceClient;

        public MainPage()
        {
            this.InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            var connectionString = GetConnectionString();
            DeviceClientPanel.Visibility = connectionString == null ? Visibility.Collapsed : Visibility.Visible;
            RegistrationPanel.Visibility = connectionString == null ? Visibility.Visible : Visibility.Collapsed;

            if (connectionString != null)
            {
                InitializeDeviceClient(connectionString);
            }
        }

        private async Task InitializeDeviceClient(string connectionString)
        {
            _deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Http1);
            var iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
            DeviceClientPanel.Children.Add(new TextBlock() { Text = "Host: " + iotHubConnectionStringBuilder.HostName });
            DeviceClientPanel.Children.Add(new TextBlock() { Text = "Device: " + iotHubConnectionStringBuilder.DeviceId });

            Message receivedMessage;
            string messageData;

            while (true)
            {
                receivedMessage = await _deviceClient.ReceiveAsync();

                if (receivedMessage != null)
                {
                    messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                    var updateAccessKeyRequest = JsonConvert.DeserializeObject<UpdateAccessKeyRequest>(messageData);
                    if (updateAccessKeyRequest != null)
                    {
                        DeviceClientPanel.Children.Add(new TextBlock() { Text = "Update Access Key received" });

                        UpdateConnectionString(updateAccessKeyRequest.AccessKey);
                        await _deviceClient.CloseAsync();
                        _deviceClient = DeviceClient.CreateFromConnectionString(GetConnectionString(), TransportType.Http1);
                        await _deviceClient.CompleteAsync(receivedMessage);
                    }
                }
            }

        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var deviceId = DeviceIdTextBox.Text;
            var dataToSend = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                new DeviceRegistrationRequest
                {
                    RegistrationKey = RegistrationKeyTextBox.Text,
                    DeviceId = deviceId
                }));

            var webRequest = (HttpWebRequest)WebRequest.Create(RegistrationUrlTextBox.Text);
            webRequest.ContentType = "application/json";
            webRequest.Method = "POST";
            var requestStream = await webRequest.GetRequestStreamAsync();
            requestStream.Write(dataToSend, 0, dataToSend.Length);

            try
            {
                var response = await webRequest.GetResponseAsync() as HttpWebResponse;
                if (response != null
                    && response.StatusCode == HttpStatusCode.OK)
                {
                    using (var streamReader = new StreamReader(response.GetResponseStream(), true))
                    {
                        var responseText = streamReader.ReadToEnd();
                        var deviceRegistrationResponse = JsonConvert.DeserializeObject<DeviceRegistrationResponse>(responseText);

                        SaveConnectionString(deviceRegistrationResponse.HostName, deviceId, deviceRegistrationResponse.AccessKey);

                        InitializeApplication();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static string GetConnectionString()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var connectionString = settings.Values["IoTHubConnectionString"];
            return connectionString?.ToString();
        }

        private static void SaveConnectionString(string hostName, string deviceId, string accessKey)
        {
            var iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(
                hostName,
                AuthenticationMethodFactory.CreateAuthenticationWithRegistrySymmetricKey(
                    deviceId,
                    accessKey));

            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["IoTHubConnectionString"] = iotHubConnectionStringBuilder.ToString();
        }

        private static void UpdateConnectionString(string accessKey)
        {
            var iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(GetConnectionString());

            SaveConnectionString(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, accessKey);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["IoTHubConnectionString"] = null;

            InitializeApplication();
        }
    }
}
