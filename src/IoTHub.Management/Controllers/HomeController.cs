namespace IoTHub.Management.Controllers
{
    using TableStorage;
    using Microsoft.Azure.Devices;
    using System.Configuration;
    using System.Threading.Tasks;
    using System.Web.Mvc;
    using System;
    using IotHub.Common.Models;
    using System.Text;
    using Newtonsoft.Json;
    using System.Linq;
    using System.Threading;

    public class HomeController : Controller
    {
        public async Task<ActionResult> Index()
        {
            ViewBag.Title = "Home Page";

            var registryManager = RegistryManager.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHubConnectionString"]);
            var devices = await registryManager.GetDevicesAsync(-1);

            return View(devices);
        }

        public async Task<ActionResult> DeleteDevice(string id)
        {
            var registryManager = RegistryManager.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHubConnectionString"]);
            await registryManager.RemoveDeviceAsync(id);

            return RedirectToAction("Index");
        }

        public async Task<ActionResult> RenewKeys(string id)
        {
            var registryManager = RegistryManager.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHubConnectionString"]);
            var device = await registryManager.GetDeviceAsync(id);
            var serviceClient = ServiceClient.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHubConnectionString"]);

            // First tell the device to use the secondary key
            var updateAccessKey = new UpdateAccessKeyRequest
            {
                AccessKey = device.Authentication.SymmetricKey.SecondaryKey
            };
            var accepted = await SendMessageAndWaitForResponse(serviceClient, id, updateAccessKey);

            if (accepted)
            {
                // Since the device is know using the secondary key,
                // we can update the primary key without disruption for the device
                updateAccessKey = new UpdateAccessKeyRequest
                {
                    AccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()))
                };
                var originialKey = device.Authentication.SymmetricKey.PrimaryKey;
                device.Authentication.SymmetricKey.PrimaryKey = updateAccessKey.AccessKey;
                var updatedDevice = await registryManager.UpdateDeviceAsync(device, true);
                accepted = await SendMessageAndWaitForResponse(serviceClient, id, updateAccessKey);

                // If the device could not change the AccessKey, make sure we keep using the original one
                if (!accepted)
                {
                    device.Authentication.SymmetricKey.PrimaryKey = originialKey;
                    updatedDevice = await registryManager.UpdateDeviceAsync(device, true);
                }
            }

            return View(accepted);
        }

        private async Task<bool> SendMessageAndWaitForResponse(ServiceClient serviceClient, string deviceId, UpdateAccessKeyRequest updateAccessKey)
        {
            var serviceMessage = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(updateAccessKey)));
            serviceMessage.Ack = DeliveryAcknowledgement.Full;
            serviceMessage.MessageId = Guid.NewGuid().ToString();

            var feedbackReceiver = serviceClient.GetFeedbackReceiver();
            var feedbackReceived = false;
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;
            var feedbackTask = Task.Run(async () =>
            {
                while (!feedbackReceived && !cancellationToken.IsCancellationRequested)
                {
                    var feedbackBatch = await feedbackReceiver.ReceiveAsync(TimeSpan.FromSeconds(0.5));
                    if (feedbackBatch != null)
                    {
                        feedbackReceived = feedbackBatch.Records.Any(fm =>
                            fm.DeviceId == deviceId
                            && fm.OriginalMessageId == serviceMessage.MessageId);
                        if (feedbackReceived)
                        {
                            await feedbackReceiver.CompleteAsync(feedbackBatch);
                        }
                    }
                }
            }, cancellationToken);

            await Task.WhenAll(
                feedbackTask,
                serviceClient.SendAsync(deviceId, serviceMessage));

            return feedbackReceived;
        }

        public async Task<ActionResult> CreateRegistrationKey()
        {
            var registrationKeyStorage = new RegistrationKeyStorage(ConfigurationManager.AppSettings["StorageConnectionString"]);
            var registrationKey = await registrationKeyStorage.CreateRegistrationKey();
            return View(registrationKey);
        }
    }
}
