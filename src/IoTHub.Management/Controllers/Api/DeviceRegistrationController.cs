namespace IoTHub.Management.Controllers.Api
{
    using TableStorage;
    using System.Web.Http;
    using System.Configuration;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Common.Models;

    public class DeviceRegistrationController : ApiController
    {
        // POST api/values
        public async Task<IHttpActionResult> Post(DeviceRegistrationRequest request)
        {
            var registrationKeyStorage = new RegistrationKeyStorage(ConfigurationManager.AppSettings["StorageConnectionString"]);
            var registrationKey = registrationKeyStorage.GetRegistrationKey(request.RegistrationKey);
            if (registrationKey != null
                && registrationKey.ValidUntil > DateTime.UtcNow
                && !registrationKey.UsedOn.HasValue)
            {
                registrationKey.UsedOn = DateTime.UtcNow;
                await registrationKeyStorage.UpdateRegistrationKey(registrationKey);

                var registryManager = RegistryManager.CreateFromConnectionString(ConfigurationManager.AppSettings["IoTHubConnectionString"]);
                try
                {
                    var device = await registryManager.AddDeviceAsync(new Device(request.DeviceId));

                    var iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(ConfigurationManager.AppSettings["IoTHubConnectionString"]);
                    return Ok(new DeviceRegistrationResponse
                    {
                        HostName = iotHubConnectionStringBuilder.HostName,
                        AccessKey = device.Authentication.SymmetricKey.PrimaryKey
                    });
                }
                catch (DeviceAlreadyExistsException)
                {
                }
            }
            return BadRequest();
        }
    }
}
