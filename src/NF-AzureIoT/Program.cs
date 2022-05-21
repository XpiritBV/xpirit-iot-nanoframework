using nanoFramework.Azure.Devices.Client;
using nanoFramework.Azure.Devices.Provisioning.Client;
using nanoFramework.Azure.Devices.Shared;
using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NF_AzureIoTDeviceTwin
{
    public class Program
    {
        // Fill in your own wifi network ssid  + password
        private static readonly string _ssid = "SSID";
        private static readonly string _wifiPassword = "WIFIPASSWORD";

        // Fill in the idscope, name of the individual enrolment device, primary key (symmetric key)
        private static readonly string _deviceProvisioningEndpoint = "global.azure-devices-provisioning.net";
        private static readonly string _idScope = "0ne...";
        private static readonly string _deviceRegistrationId = "aca-esp-sk01";
        private static readonly string _deviceKey = "DEVICE KEY";

        const string RootPath = "I:\\";
        static DeviceClient device;
        static int version = -1;
        static string[] files;
        static HttpClient httpClient = new HttpClient();

        public static void Main()
        {
            Debug.WriteLine("Setting up WIFI networking!");

            if (WiFiNetworkHelper.ConnectDhcp(_ssid, _wifiPassword))
            {
                DownloadFirmware("https://xpiritiotnanoframeworksa.blob.core.windows.net/firmware/NF_AzureIoT.pe", "NF_AzureIoT.pe");

                Debug.WriteLine("Connect to Azure IoT hub using DPS.");

                X509Certificate azureCert = new X509Certificate(AzureRootCA);
                ProvisioningDeviceClient provisioningClient = ProvisioningDeviceClient.Create(_deviceProvisioningEndpoint, _idScope,
                                                                                              _deviceRegistrationId, _deviceKey, azureCert);

                DeviceRegistrationResult myDevice = provisioningClient.Register(CancellationToken.None);
                if (myDevice.Status == ProvisioningRegistrationStatusType.Assigned)
                {

                    // Connect to IoT hub and start using it
                    device = new DeviceClient(myDevice.AssignedHub, myDevice.DeviceId, _deviceKey,
                                                  nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtMostOnce, azureCert);

                    if (device.Open())
                    {
                        Debug.WriteLine("Register Device Twin events");
                        device.TwinUpated += Device_TwinUpated;
                        device.StatusUpdated += Device_StatusUpdated;

                        Twin deviceTwin = device.GetTwin(new CancellationTokenSource(10000).Token);
                        Debug.WriteLine($"Get Device Twin (json){deviceTwin.ToJson()}");

                        Debug.WriteLine("Update Reported Device Twin property");
                        UpdateProperty(deviceTwin.Properties.Reported, "someProperty", "new value from device");
                        bool result = device.UpdateReportedProperties(deviceTwin.Properties.Reported);
                    }
                }
                else
                {
                    Debug.WriteLine($"Device could not be registered. Statuscode: {myDevice.Status}.{myDevice.Substatus} - {myDevice.ErrorMessage}");
                }
            }
            Thread.Sleep(Timeout.Infinite);

        }

        private static void Device_StatusUpdated(object sender, StatusUpdatedEventArgs e)
        {
            Debug.WriteLine($"Hub status updated {e.IoTHubStatus.Status}-{e.IoTHubStatus.Message}");
        }

        private static void UpdateProperty(TwinCollection collection, string name, object value)
        {
            if (collection == null) return;
            if (collection.Contains(name))
                collection[name] = value;
            else
                collection.Add(name, value);
        }

        private static void Device_TwinUpated(object sender, TwinUpdateEventArgs e)
        {
            ProcessTwinAndDownloadFiles(e.Twin);
            Debug.WriteLine($"Changed Device Twin From Event (json):\r\n{e.Twin.ToJson()}");
        }


        static void ProcessTwinAndDownloadFiles(TwinCollection desired)
        {
            int codeVersion = 0;
            codeVersion = (int)desired["CodeVersion"];
            string[] files;
            TwinCollection reported = new TwinCollection();

            // If the version is the same as the stored one, no changes, we can load the code
            // Otherwise we have to download a new version
            if (codeVersion != version)
            {
                // And update the reported twin
                reported.Add("Message", "Updating...");
                device.UpdateReportedProperties(reported);

                string firmwareLocation = (string)desired["Firmware"];
                Debug.WriteLine(firmwareLocation);

                string fileName = firmwareLocation.Substring(firmwareLocation.LastIndexOf('/') + 1);

                DownloadFirmware(firmwareLocation, fileName);

                // RebootDevice();
            }
        }

        private static void DownloadFirmware(string firmwareLocation, string fileName)
        {
            // Let's first clean all the pe files
            // We keep any other file
            files = Directory.GetFiles(RootPath);
            foreach (var file in files)
            {
                if (file.EndsWith(".pe"))
                {
                    File.Delete(file);
                }
            }


            //// If we are connected to Azure, we will disconnect as small devices only have limited memory
            if (device != null && device.IsConnected)
            {
                device.Close();
            }

            //httpClient.DefaultRequestHeaders.Add("x-ms-blob-type", "BlockBlob");
            // this example uses Tls 1.2 with Azure
            httpClient.SslProtocols = System.Net.Security.SslProtocols.Tls12;
            // use the pem certificate we created earlier
            httpClient.HttpsAuthentCert = new X509Certificate(azurePEMCertBaltimore);

            HttpResponseMessage response = httpClient.Get(firmwareLocation);
            response.EnsureSuccessStatusCode();

            using FileStream fs = new FileStream($"{RootPath}{fileName}", FileMode.Create, FileAccess.Write);
            response.Content.ReadAsStream().CopyTo(fs);
            fs.Flush();
            fs.Close();
            response.Dispose();
        }

        // Azure Root CA certificate. This certificate might expire in JUNE 2022 
        private const string AzureRootCA = @"-----BEGIN CERTIFICATE-----
MIIDdzCCAl+gAwIBAgIEAgAAuTANBgkqhkiG9w0BAQUFADBaMQswCQYDVQQGEwJJ
RTESMBAGA1UEChMJQmFsdGltb3JlMRMwEQYDVQQLEwpDeWJlclRydXN0MSIwIAYD
VQQDExlCYWx0aW1vcmUgQ3liZXJUcnVzdCBSb290MB4XDTAwMDUxMjE4NDYwMFoX
DTI1MDUxMjIzNTkwMFowWjELMAkGA1UEBhMCSUUxEjAQBgNVBAoTCUJhbHRpbW9y
ZTETMBEGA1UECxMKQ3liZXJUcnVzdDEiMCAGA1UEAxMZQmFsdGltb3JlIEN5YmVy
VHJ1c3QgUm9vdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKMEuyKr
mD1X6CZymrV51Cni4eiVgLGw41uOKymaZN+hXe2wCQVt2yguzmKiYv60iNoS6zjr
IZ3AQSsBUnuId9Mcj8e6uYi1agnnc+gRQKfRzMpijS3ljwumUNKoUMMo6vWrJYeK
mpYcqWe4PwzV9/lSEy/CG9VwcPCPwBLKBsua4dnKM3p31vjsufFoREJIE9LAwqSu
XmD+tqYF/LTdB1kC1FkYmGP1pWPgkAx9XbIGevOF6uvUA65ehD5f/xXtabz5OTZy
dc93Uk3zyZAsuT3lySNTPx8kmCFcB5kpvcY67Oduhjprl3RjM71oGDHweI12v/ye
jl0qhqdNkNwnGjkCAwEAAaNFMEMwHQYDVR0OBBYEFOWdWTCCR1jMrPoIVDaGezq1
BE3wMBIGA1UdEwEB/wQIMAYBAf8CAQMwDgYDVR0PAQH/BAQDAgEGMA0GCSqGSIb3
DQEBBQUAA4IBAQCFDF2O5G9RaEIFoN27TyclhAO992T9Ldcw46QQF+vaKSm2eT92
9hkTI7gQCvlYpNRhcL0EYWoSihfVCr3FvDB81ukMJY2GQE/szKN+OMY3EU/t3Wgx
jkzSswF07r51XgdIGn9w/xZchMB5hbgF/X++ZRGjD8ACtPhSNzkE1akxehi/oCr0
Epn3o0WC4zxe9Z2etciefC7IpJ5OCBRLbf1wbWsaY71k5h+3zvDyny67G7fyUIhz
ksLi4xaNmjICq44Y3ekQEe5+NauQrz4wlHrQMz2nZQ/1/I6eYs9HRCwBXbsdtTLS
R9I4LtD+gdwyah617jzV/OeBHRnDJELqYzmp
-----END CERTIFICATE-----
";

        private const string azurePEMCertBaltimore = @"-----BEGIN CERTIFICATE-----
MIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7KGSxHQn65TANBgkqhkiG9w0BAQsFADBh
MQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3
d3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBH
MjAeFw0xMzA4MDExMjAwMDBaFw0zODAxMTUxMjAwMDBaMGExCzAJBgNVBAYTAlVT
MRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5j
b20xIDAeBgNVBAMTF0RpZ2lDZXJ0IEdsb2JhbCBSb290IEcyMIIBIjANBgkqhkiG
9w0BAQEFAAOCAQ8AMIIBCgKCAQEAuzfNNNx7a8myaJCtSnX/RrohCgiN9RlUyfuI
2/Ou8jqJkTx65qsGGmvPrC3oXgkkRLpimn7Wo6h+4FR1IAWsULecYxpsMNzaHxmx
1x7e/dfgy5SDN67sH0NO3Xss0r0upS/kqbitOtSZpLYl6ZtrAGCSYP9PIUkY92eQ
q2EGnI/yuum06ZIya7XzV+hdG82MHauVBJVJ8zUtluNJbd134/tJS7SsVQepj5Wz
tCO7TG1F8PapspUwtP1MVYwnSlcUfIKdzXOS0xZKBgyMUNGPHgm+F6HmIcr9g+UQ
vIOlCsRnKPZzFBQ9RnbDhxSJITRNrw9FDKZJobq7nMWxM4MphQIDAQABo0IwQDAP
BgNVHRMBAf8EBTADAQH/MA4GA1UdDwEB/wQEAwIBhjAdBgNVHQ4EFgQUTiJUIBiV
5uNu5g/6+rkS7QYXjzkwDQYJKoZIhvcNAQELBQADggEBAGBnKJRvDkhj6zHd6mcY
1Yl9PMWLSn/pvtsrF9+wX3N3KjITOYFnQoQj8kVnNeyIv/iPsGEMNKSuIEyExtv4
NeF22d+mQrvHRAiGfzZ0JFrabA0UWTW98kndth/Jsw1HKj2ZL7tcu7XUIOGZX1NG
Fdtom/DzMNU+MeKNhJ7jitralj41E6Vf8PlwUHBHQRFXGU7Aj64GxJUTFy8bJZ91
8rGOmaFvE7FBcf6IKshPECBV1/MUReXgRPTqh5Uykw7+U0b6LJ3/iyK5S9kJRaTe
pLiaWN0bfVKfjllDiIGknibVb63dDcY3fe0Dkhvld1927jyNxF1WW6LZZm6zNTfl
MrY=
-----END CERTIFICATE-----";
    }
}
