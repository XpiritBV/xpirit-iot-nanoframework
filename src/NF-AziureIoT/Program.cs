using nanoFramework.Azure.Devices.Client;
using nanoFramework.Azure.Devices.Provisioning.Client;
using nanoFramework.Azure.Devices.Shared;
using nanoFramework.Networking;
using System.Diagnostics;
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


        public static void Main()
        {
            Debug.WriteLine("Setting up WIFI networking!");

            if (WiFiNetworkHelper.ConnectDhcp(_ssid, _wifiPassword))
            {
                Debug.WriteLine("Connect to Azure IoT hub using DPS.");

                X509Certificate azureCert = new X509Certificate(AzureRootCA);
                ProvisioningDeviceClient provisioningClient = ProvisioningDeviceClient.Create(_deviceProvisioningEndpoint, _idScope,
                                                                                              _deviceRegistrationId, _deviceKey, azureCert);

                DeviceRegistrationResult myDevice = provisioningClient.Register(CancellationToken.None);
                if (myDevice.Status == ProvisioningRegistrationStatusType.Assigned)
                {

                    // Connect to IoT hub and start using it
                    var device = new DeviceClient(myDevice.AssignedHub, myDevice.DeviceId, _deviceKey,
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
                    Debug.WriteLine($"Device could not be registered. Statuscode: {myDevice.Status}.{myDevice.Substatus}");
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
            Debug.WriteLine($"Changed Device Twin From Event (json):\r\n{e.Twin.ToJson()}");
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


    }
}
