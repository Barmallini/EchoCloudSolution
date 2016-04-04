using System;

using System.Collections.Generic;

using System.Linq;

using System.Text;

using System.Threading.Tasks;

using Microsoft.WindowsAzure.Storage;

using Microsoft.WindowsAzure.Storage.Auth;

using Microsoft.WindowsAzure.Storage.Table;
using System.Globalization;

 

namespace wadlogs

{

    class Program

    {

        static void Main(string[] args)
        {
            CultureInfo cult = new CultureInfo(0x0419);
            cult.NumberFormat.NumberDecimalSeparator = ".";

            string latitude = "5139.1545";
            string latitude_direction = "N"; // N or S
            int point1 = latitude.IndexOf('.');
            string minute1 = latitude.Substring(point1 - 2);
            string degree1 = latitude.Substring(0, point1 - 2);
            double latitude_double = Convert.ToInt32(degree1) + (Convert.ToDouble(minute1, cult) / 60);



            string received_text = "12345;";
            string cmd1 = "";

            if (received_text.IndexOf(";") > 0) cmd1 = received_text.Substring(0, received_text.IndexOf(";"));

            DateTime utcStartTime = DateTime.UtcNow;

            Console.WriteLine("Enter your storage account connection string, or press enter for ‘UseDevelopmentStorage=true;':");

            var connStr = Console.ReadLine();

            connStr = connStr.Trim(new[] { ' ', '\"', '\'', });

            if (string.IsNullOrEmpty(connStr))

            {

                connStr = "UseDevelopmentStorage=true;";

            }

 

            var storageAccount = CloudStorageAccount.Parse(connStr);

            var tableClient = storageAccount.CreateCloudTableClient();

            var table = tableClient.GetTableReference("WADLogsTable");

            var query = table.CreateQuery<GenericTableEntity>()

                .Where(e => e.Timestamp > utcStartTime.AddMinutes(-120));

            var results = query.ToList();

            results = results.OrderBy(x => x.Timestamp).ToList();

            foreach (var row in results)

            {

                Console.WriteLine("{0}: {1}", row.Timestamp, row.Properties["Message"].StringValue);

            }

        }

    }

 

    public class GenericTableEntity : ITableEntity

    {

        public string ETag { get; set; }

 

        public string PartitionKey { get; set; }

 

        public string RowKey { get; set; }

 

        public DateTimeOffset Timestamp { get; set; }

 

        public IDictionary<string, EntityProperty> Properties { get; set; }

 

        public void ReadEntity(IDictionary<string, EntityProperty> properties, Microsoft.WindowsAzure.Storage.OperationContext operationContext)

        {

            this.Properties = properties;

        }

 

        public IDictionary<string, EntityProperty> WriteEntity(Microsoft.WindowsAzure.Storage.OperationContext operationContext)

        {

            return this.Properties;

        }

    }

}