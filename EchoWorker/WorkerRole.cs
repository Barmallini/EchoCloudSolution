using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
//using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Net.Sockets;
using System.IO;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Globalization;
using Microsoft.Azure;



namespace EchoWorker
{
    public class CustomerEntity : TableEntity
    {
        public CustomerEntity(string partname, string guid)
        {
            this.PartitionKey = partname;
            this.RowKey = guid;
        }

        public CustomerEntity() { }

        public DateTime dt { get; set; }
        public Double latitude { get; set; }
        public Double longtitude { get; set; }

    }

    public class WorkerRole : RoleEntryPoint
    {
        private AutoResetEvent connectionWaitHandle = new AutoResetEvent(false);
        //StreamWriter writeFileStream = null;
        const string imei = "354779036439983";
        CultureInfo cult = new CultureInfo(0x0419);

        private void hh()
        {
            //Parse the connection string for the storage account.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                Microsoft.Azure.CloudConfigurationManager.GetSetting("StorageConnectionString"));

            //Create service client for credentialed access to the Blob service.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            //Get a reference to a container.
            CloudBlobContainer container = blobClient.GetContainerReference("my-append-blobs");

            //Create the container if it does not already exist.
            container.CreateIfNotExists();

            //Get a reference to an append blob.
            CloudAppendBlob appendBlob = container.GetAppendBlobReference("append-blob.log");

            //Create the append blob. Note that if the blob already exists, the CreateOrReplace() method will overwrite it.
            //You can check whether the blob exists to avoid overwriting it by using CloudAppendBlob.Exists().
            appendBlob.CreateOrReplace();

            int numBlocks = 10;

            //Generate an array of random bytes.
            Random rnd = new Random();
            byte[] bytes = new byte[numBlocks];
            rnd.NextBytes(bytes);

            //Simulate a logging operation by writing text data and byte data to the end of the append blob.
            for (int i = 0; i < numBlocks; i++)
            {
                appendBlob.AppendText(String.Format("Timestamp: {0:u} \tLog Entry: {1}{2}",
                    DateTime.UtcNow, bytes[i], Environment.NewLine));
            }

            //Read the append blob to the console window.
            Console.WriteLine(appendBlob.DownloadText());

        }

        public override void Run()
        {
            Trace.WriteLine("Starting echo server...", "Information");

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(
                    RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["EchoEndpoint"].IPEndpoint);
                listener.ExclusiveAddressUse = false;
                listener.Start();

                Trace.WriteLine("Started echo server.", "Information");
            }
            catch (SocketException se)
            {
                Trace.Write("Echo server could not start.", "Error");
                return;
            }

            while (true)
            {
                IAsyncResult result = listener.BeginAcceptTcpClient(HandleAsyncConnection, listener);
                connectionWaitHandle.WaitOne();
            }
        }

        private void HandleAsyncConnection(IAsyncResult result)
        {
            // Accept connection
            TcpListener listener = (TcpListener)result.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(result);
            //client.ReceiveTimeout = 2000;
            connectionWaitHandle.Set();

            // Accepted connection
            Guid clientId = Guid.NewGuid();
            Trace.WriteLine("Accepted connection with ID " + clientId.ToString(), "Information");

            // Setup reader/writer
            NetworkStream netStream = client.GetStream();
            BinaryWriter bw = new BinaryWriter(netStream);
            StreamReader reader = new StreamReader(netStream);
            StreamWriter writer = new StreamWriter(netStream);
            writer.AutoFlush = true;
            char[] buff = new char[1024];
            byte[] load = new byte[] { 0x4C, 0x4F, 0x41, 0x44 };
            byte[] on = new byte[] { 0x4F, 0x4E };
            int readed = 0;
            StringBuilder b = new StringBuilder();
            DateTime last_time = DateTime.Now;
            string[] cmd;
            string cmd1 = "";
            bool isConnected = false;

            // Show application
            //string input = string.Empty;
            while (true)
            {
                try
                {
                    readed = reader.Read(buff, 0, buff.Length);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                    break;
                }



                if (readed != 0)
                {
                    b.Append(buff, 0, readed);
                    string received_text = b.ToString();
                    Trace.WriteLine(received_text);
                    cmd1 = string.Empty;
                    if (received_text.IndexOf(";") > 0) cmd1 = received_text.Substring(0, received_text.IndexOf(";"));
                    cmd = cmd1.Split(',');
                    if (cmd.Length > 0)
                    {
                        switch (cmd[0])
                        {
                            case "##":          // connect
                                bw.Write(load, 0, 4);
                                isConnected = true;
                                break;
                            case imei:          // keepalive
                                bw.Write(on, 0, 2);
                                isConnected = true;
                                break;
                            case "imei:" + imei:  // gps info
                                try
                                {
                                    string tracker_status = cmd[1]; //tracker or low battery
                                    string admin = cmd[3];
                                    string gps_available = cmd[4];
                                    if (gps_available == "F")    // F or L
                                    {
                                        // gps signal received
                                        DateTime dt = DateTime.ParseExact(cmd[2], "yyMMddHHmm", CultureInfo.InvariantCulture);
                                        string timefix = cmd[5];



                                        string latitude = cmd[7];
                                        string latitude_direction = cmd[8]; // N or S
                                        int point1 = latitude.IndexOf('.');
                                        string minute1 = latitude.Substring(point1 - 2);
                                        string degree1 = latitude.Substring(0, point1 - 2);
                                        double latitude_double = Convert.ToInt32(degree1) + (Convert.ToDouble(minute1, cult) / 60);
                                        if (latitude_direction.ToUpper() == "S") latitude_double *= -1;

                                        string longtitude = cmd[9];
                                        string longtitude_direction = cmd[10]; // W or E
                                        int point2 = longtitude.IndexOf('.');
                                        string minute2 = longtitude.Substring(point2 - 2);
                                        string degree2 = longtitude.Substring(0, point2 - 2);
                                        double longtitude_double = Convert.ToInt32(degree2) + (Convert.ToDouble(minute2, cult) / 60);
                                        if (longtitude_direction.ToUpper() == "W") longtitude_double *= -1;


                                        string hz = cmd[11];

                                        if (table != null)
                                        {
                                            // Create a new customer entity.
                                            CustomerEntity rec1 = new CustomerEntity(imei, Guid.NewGuid().ToString());
                                            rec1.dt = dt;
                                            rec1.latitude = latitude_double;
                                            rec1.longtitude = longtitude_double;

                                            // Create the TableOperation object that inserts the customer entity.
                                            TableOperation insertOperation = TableOperation.Insert(rec1);

                                            // Execute the insert operation.
                                            table.Execute(insertOperation);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Trace.WriteLine(ex.Message);
                                }
                                break;
                            default:

                                break;
                        }

                        if (last_time.AddSeconds(30) < DateTime.Now && isConnected)
                        {
                            string dados = String.Format("**,imei:{0},B;", imei);
                            byte[] bytes = Encoding.ASCII.GetBytes(dados);
                            bw.Write(bytes, 0, bytes.Length);
                            last_time = DateTime.Now;
                        }
                    }


                    //354779036439983
                    //EventName="DirectWrite" Message="imei:354779036439983,tracker,0000000000,+79102436541,L,;"    // no signal
                    //                                                              datetime YYMMDDHHMM
                    //EventName="DirectWrite" Message="imei:354779036439983,low battery,1604012259,+79102436541,F,235952.000,A,5139.1545,N,03916.9044,E,0.89,;"
                    //                                                                                         gps  vysota     latitud dms, longtit dms,speed  
                    //It is counted like this: the last 6 digitals divided by 60,then plus the first two digitals.
                    //5320.6735,N-------20.6735/60+53=0.3445583+53=53.344558N   // если N оставляем положительным, если S то делаем отрицательным
                    //00129.0141,W------29.014/60+001=0.4835683+001=001.4835683W    // если E то оставляем положительным, если W то делаем отрицательным

                    b.Clear();
                }

                if (isConnected)
                {
                    Thread.Sleep(5000);
                }
                else
                {
                    Thread.Sleep(2000);
                }

            }


            // Done!
            client.Close();
            Trace.WriteLine("Closed connection with ID " + clientId.ToString(), "Information");
        }

        CloudStorageAccount storageAccount = null;
        CloudTableClient tableClient = null;
        CloudTable table = null;

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            cult.NumberFormat.NumberDecimalSeparator = ".";

            // Retrieve the storage account from the connection string.
            string ss = CloudConfigurationManager.GetSetting("StorageConnectionString");
            storageAccount = CloudStorageAccount.Parse(ss);
            //Trace.TraceInformation(String.Format("StorageConnectionString {0}", ss));

            if (storageAccount != null)
            {
                // Create the table client.
                tableClient = storageAccount.CreateCloudTableClient();
                if (tableClient != null)
                {
                    // Create the table if it doesn't exist.
                    table = tableClient.GetTableReference("gpstrackerlog");
                    table.CreateIfNotExists();
                }
                else
                {
                    Trace.TraceInformation(String.Format("tableClient {0}", "null"));
                }
            }
            else
            {
                Trace.TraceInformation(String.Format("storageAccount {0}", "null"));
            }

            //DiagnosticMonitor.Start("DiagnosticsConnectionString");

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += RoleEnvironmentChanging;
            Trace.TraceInformation("Service OnStart");
            return base.OnStart();
        }

        public override void OnStop()
        {
            Trace.TraceInformation("Service OnStop");
            //writeFileStream.Flush();
            //writeFileStream.Close();
            //writeFileStream = null;
            base.OnStop(); ;
        }

        private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If a configuration setting is changing
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                // Set e.Cancel to true to restart this role instance
                Trace.TraceInformation("Service OnRoleEnvironmentChanging");
                e.Cancel = true;
            }
        }
    }




    /* kml_export
     * 
    <?xml version='1.0' encoding='UTF-8'?>
<kml xmlns='http://www.opengis.net/kml/2.2'>
  <Document>
    <name>marshrut_zzz</name>
    <Placemark>
      <name>marshrut_zzz2</name>
      <LineString>
        <tessellate>1</tessellate>
        <coordinates>39.20858,51.67551,0.0 39.20858,51.67547,0.0 39.20859,51.67545,0.0 39.20859,51.67543,0.0 39.20861,51.67541,0.0</coordinates>
      </LineString>
    </Placemark>
  </Document>
</kml>
    */



    /*
     * 
     * that is really interesting! As i said, mine is different from most TK's (mine is also a clone)

I think you will find that the difference in SMS Vs GPRS coords is that one is in decimal and the other in 'minutes' respectively. You have to convert.

The protocol i have is as follows:
After initiating GPRS mode (adminip) the tracker will attempt to connect with '##,imei:<imei>,A;'
The server then replies 'LOAD'

After this, the tracker sends a 'keepalive' every minuteto the server of JUST it's imei. The server must reply with 'ON' or the tracker will terminate it's GPRS session and try again from the ## command.

Once a session is established, the following commands can be sent, all commands start with **,imei:<imei>,
**,imei:<imei>,B - Single track (like calling the tracker)
**,imei:<imei>,C,20s - 20s polling
**,imei:<imei>,C,01m - 1m polling
**,imei:<imei>,D - Disable multi tracking
**,imei:<imei>,E - Stop alarm (stops move, stockade alarms)
**,imei:<imei>,G - Set move alarm
**,imei:<imei>,H,060 - 60 k/mph overspeed (mine seems to work in MILES??)
**,imei:<imei>,I,+9 - Set timezone to +9h
**,imei:<imei>,N - Return to SMS mode ('noadminip')
Using GPRS commands, mine will track @ 5s intervals.


After issuing the command, you should get a response from the tracker much like the SMS confirm such as:
imei:<imei>,ht,..... the HT denotes 'Speed alarm OK'

In an alarm case, it will replace the 'HT' with 'LOW BATTERY', 'SPEED', 'MOVE' etc. 

I am interested to see how many of these commands work on a 'generic' TK or even one from xexun. I have a feeling mine is more like a TK-102-2

     * 
     * 
     * 
     * public void StartListen()
{
while (true)
{
string dados; byte[] bytes;
Console.WriteLine("Aguardando por conexào...");
TcpClient tcpClient = tcpListener.AcceptTcpClient();

tcpClient.ReceiveTimeout = timeout;
Console.WriteLine("Conexào realizada...");
NetworkStream networkStream = tcpClient.GetStream();

bytes = new byte[tcpClient.ReceiveBufferSize];
networkStream.Read(bytes, 0, (int)tcpClient.ReceiveBufferSize);
dados = Encoding.ASCII.GetString(bytes);
if (dados.IndexOf(";") > 0) dados = dados.Substring(0, dados.IndexOf(";") + 1);


Console.WriteLine("Dados Recebidos: " + dados);
Log(dados);


if (dados == "##,imei:123456789132456,A;")
{
dados = "LOAD";
}
else if (dados == "123456789132456;")
{
dados = "ON";
dados = "**,imei:123456789132456,B;";
}
else
{
dados = "**,imei:123456789132456,N;";
}


bytes = Encoding.ASCII.GetBytes(dados);
networkStream.Write(bytes, 0, bytes.Length);
Console.WriteLine(("Enviados /> : " + dados));


networkStream.Close();
networkStream = null;
tcpClient.Close();
tcpClient = null;

}

}
     * 
     * 
     * 
     * 
     * 
     * 
     * To configure your Xexun TK102-2 correctly, overview of all known commands, including examples


Initialisierung/Zurücksetzen
Kommando: begin+password
Antwort: begin ok

Passwort ändern
password+OldPassword+space+NewPassword
password ok

Rufnummer autorisieren
admin+password+space+PhoneNumber
admin ok

Autorisierte Nummer löschen
noadmin+password+space+PhoneNumber to be deleted
noadmin ok


Sendeintervall festlegen
Auto track - 5 times with 30 seconds interval
t030s005n+password
Auto track - 26 times with 5 minutes interval
t005m026n+password
Auto track - 10 times with 1 hours interval
t001h010n+password
Auto track - heaps of times with 30 seconds interval
t030s***n+password

Cancel Auto Track
notn+password
notn ok
Switch to Voice Monitor mode
monitor+password
monitor ok

Switch to Tracker mode
tracker+password
tracker ok
Geo Fencing
stockade+password+space+longitudeE/W,latitudeN/S; longitudeE/W,latitudeN/S
stockade ok

Cancel Geo Fencing
nostockade+password
nostockage ok

Movement alert
move+password
move ok

Cancel Movement alert
nomove+password
nomove ok

Overspeed alert
speed+password+space+080
speed ok

Cancel Overspeed alert
nospeed+password
nospeed ok

IMEI checking
imei+password

SMS Center
adminsms+password+space+cell phone number

Cancel SMS Center
noadminsms+password

Low battery alert
low battery+Geo-info

SOS button
Press the SOS for 3 seconds
help me !+ Geo-info

Time zone setting
time+space+zone+password+space+TimeZone

home page setting
home+password+space+the website you want to set

Switch to the link format
smslink+password

switch back to the text format
smstext+password

Extra functions for TK102-2NEW

To get one time positioning
smsone123456

Tlimit function
tlimit123456 100

Cancel Tlimit function
tlimit123456 0

Motion sensor
shake123456 0-10 (0 means off,1 means the lowest sensitive, 10 means the highest sensitive)

Activate SD card function
sdlog123456 1

Deactivate SD card function
sdlog123456 0

send data in the SD card to the GPRS server
readsd+password+space+1

stop sending data in the SD card to the server
readsd+password+space+0

Tracker reply one sms with GOOGLE MAP link
smslinkone+password link to google map

set the GPS re-fresh time interval
gpsautosearch123456 120
120 means 120 seconds, and 120 is the minimum

Restore factory firmware default
format

check firmware version
version+password
TK102 V1.0-110707 ok!

GPRS Function
Setup APN
apn+password+space+the customer’s APN CONTENT

Setup APN username
apnuser+123456+space+The APN'S USER NAME

Setup APN password
apnpasswd+123456 +space+THE APN'S PASSWORD

Setup IP address & Port
adminip+password+space+ip address+space+port

Cancel IP address & Port
noadminip+password

Set GPRS MODE-UDP
gprsmode+password+1

Set GPRS MODE-TCP(default)
gprsmode+password+0 

     */

}
