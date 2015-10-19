namespace IoTHub.Management.TableStorage
{
    using Microsoft.WindowsAzure.Storage.Table;
    using System;

    public class RegistrationKeyEntity : TableEntity
    {
        public string RegistrationKey
        {
            get
            {
                return RowKey;
            }
            set
            {
                RowKey = value;
            }
        }

        public DateTime ValidUntil { get; set; }

        public DateTime? UsedOn { get; set; }

        public RegistrationKeyEntity()
        {
            PartitionKey = "RegistrationKey";
        }
    }
}