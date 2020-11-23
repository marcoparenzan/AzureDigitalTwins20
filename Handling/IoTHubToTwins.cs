
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using IoTHubTriggerAttribute = Microsoft.Azure.WebJobs.Extensions.EventGrid.EventGridTriggerAttribute;

namespace Handling
{
    public class IoTHubToTwins
    {
        private static readonly string adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient httpClient = new HttpClient();

        private static readonly string tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
        private static readonly string clientId = Environment.GetEnvironmentVariable("CLIENT_ID");

        private readonly IConfiguration configuration;
        private readonly ILogger<IoTHubToTwins> log;

        private readonly EventGridSubscriber eventGridSubscriber;

        private readonly TokenCredential cred;
        private readonly DigitalTwinsClient client;

        public IoTHubToTwins(IConfiguration configuration, ILogger<IoTHubToTwins> log)
        {
            this.configuration = configuration;
            this.log = log;

            eventGridSubscriber = new EventGridSubscriber();

            this.cred = new DefaultAzureCredential(new DefaultAzureCredentialOptions { 
                SharedTokenCacheTenantId = tenantId,
                ManagedIdentityClientId = clientId
            });
            this.client = new DigitalTwinsClient(
                new Uri(adtServiceUrl), this.cred, new DigitalTwinsClientOptions
                { Transport = new HttpClientTransport(httpClient) });
        }

        [FunctionName("IoTHubToTwinsEG")]
        public async Task<IActionResult> Handler([HttpTrigger] HttpRequestMessage request)
        {
            var content = await request.Content.ReadAsStringAsync();
            var eventGridEvents = eventGridSubscriber.DeserializeEventGridEvents(content);

            foreach (var ev in eventGridEvents)
            {
                if (ev.EventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
                {
                    var data = (SubscriptionValidationEventData)ev.Data;
                    var responseData = new SubscriptionValidationResponse()
                    {
                        ValidationResponse = data.ValidationCode
                    };
                    return new OkObjectResult(responseData);
                }
                else if (ev.EventType == "Microsoft.Devices.DeviceTelemetry")
                {
                    log.LogInformation(ev.Data.ToString());

                    var data = (IotHubDeviceTelemetryEventData)ev.Data;
                    var digitalTwinId = data.SystemProperties["iothub-connection-device-id"];
                    JObject jsonBody = default;
                    if (data.Body is string)
                    {
                        var base64 = (string)data.Body;
                        var bytes = Convert.FromBase64String(base64);
                        var json = Encoding.UTF8.GetString(bytes);
                        jsonBody = JsonConvert.DeserializeObject<JObject>(json);
                    }
                    else
                    {
                        jsonBody = (JObject)data.Body;
                    }
                    var humidity = jsonBody.Value<string>("Humidity");

                    log.LogInformation($"Device:{digitalTwinId} Humidity is:{humidity}");

                    //Update twin using device temperature
                    var response = await client.PublishTelemetryAsync(digitalTwinId, null, JsonConvert.SerializeObject(new
                    {
                        Humidity = humidity
                    }));
                    log.LogInformation($"{digitalTwinId} humidity patched to {humidity}");
                  
                    // send telemetry
                    var responseT = await client.PublishTelemetryAsync(digitalTwinId, null, JsonConvert.SerializeObject(new
                    {
                        Humidity = humidity
                    }));
                    log.LogInformation($"{digitalTwinId} humidity sent {humidity}");
                }
            }
            return new OkResult();
        }

        [FunctionName("IoTHubToTwins")]
        public async Task Run([IoTHubTrigger] EventGridEvent ev, ILogger log)
        {
            if (ev == null || ev.Data == null) return;

            try
            {
                log.LogInformation(ev.Data.ToString());

                var (digitalTwinId, temperature) = InfoFrom(ev);
                log.LogInformation($"Device:{digitalTwinId} Temperature is:{temperature}");

                //Update twin using device temperature
                await PatchTemperature(client, digitalTwinId, temperature);
            }
            catch (Exception e)
            {
                log.LogError($"Error in ingest function: {e.Message}");
            }
        }

        private (string digitalTwinId, double temperature) InfoFrom(EventGridEvent eventGridEvent)
        {
            var deviceMessage = JsonConvert.DeserializeObject<JObject>(eventGridEvent.Data.ToString());
            var digitalTwinId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
            var temperature = deviceMessage["body"]["Temperature"].Value<double>();
            return (digitalTwinId, temperature);
        }

        private async Task TelemetryTemperature(DigitalTwinsClient client, string digitalTwinId, double temperature)
        {
            var response = await client.PublishTelemetryAsync(digitalTwinId, null, JsonConvert.SerializeObject(new {
                Temperature = temperature
            }));
            log.LogInformation($"{digitalTwinId} temperature patched to {temperature}");
        }

        private async Task PatchTemperature(DigitalTwinsClient client, string digitalTwinId, double temperature)
        {
            var updateTwinData = new JsonPatchDocument();
            updateTwinData.AppendAdd("/Humidity", temperature);
            //updateTwinData.AppendReplace("/Temperature", temperature);
            var response = await client.UpdateDigitalTwinAsync(digitalTwinId, updateTwinData);
            log.LogInformation($"{digitalTwinId} temperature patched to {temperature}");
        }
    }
}