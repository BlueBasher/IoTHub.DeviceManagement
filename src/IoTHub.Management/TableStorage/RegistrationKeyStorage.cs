namespace IoTHub.Management.TableStorage
{
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class RegistrationKeyStorage
    {
        private readonly CloudTable _registrationKeysTable;

        public RegistrationKeyStorage(string connectionString)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var tableClient = account.CreateCloudTableClient();
            _registrationKeysTable = tableClient.GetTableReference("RegistrationKeys");
            _registrationKeysTable.CreateIfNotExists();
        }

        public async Task<RegistrationKeyEntity> CreateRegistrationKey()
        {
            var registrationKeyEntity = new RegistrationKeyEntity();
            registrationKeyEntity.RegistrationKey = Guid.NewGuid().ToString();
            registrationKeyEntity.ValidUntil = DateTime.UtcNow.AddHours(2);

            var operation = TableOperation.InsertOrReplace(registrationKeyEntity);
            await _registrationKeysTable.ExecuteAsync(operation);
            return registrationKeyEntity;
        }

        public RegistrationKeyEntity GetRegistrationKey(string registrationKey)
        {
            var query = new TableQuery<RegistrationKeyEntity>()
                .Where(
                TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "RegistrationKey"),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, registrationKey)));

            return _registrationKeysTable.ExecuteQuery(query).SingleOrDefault();
        }

        public async Task UpdateRegistrationKey(RegistrationKeyEntity registrationKey)
        {
            var operation = TableOperation.InsertOrReplace(registrationKey);
            await _registrationKeysTable.ExecuteAsync(operation);
        }
    }
}