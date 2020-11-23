using System;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Azure;
using System.Text.Json;

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    SharedTokenCacheTenantId = ""
});
var client = new DigitalTwinsClient(new Uri("https://***.api.***.digitaltwins.azure.net"), credential);

var typeList = new List<string>();
typeList.Add(File.ReadAllText(@"D:\Azure Digital Twins\AdtSampleApp\SampleClientApp\Models\SpaceModel.json"));
typeList.Add(File.ReadAllText(@"D:\Azure Digital Twins\AdtSampleApp\SampleClientApp\Models\Room.json"));
typeList.Add(File.ReadAllText(@"D:\Azure Digital Twins\AdtSampleApp\SampleClientApp\Models\ThermostatModel.json"));

Response<DigitalTwinsModelData[]> data = default;

try
{
    data = await client.CreateModelsAsync(typeList);
}
catch (RequestFailedException rex)
{
    Console.WriteLine($"Load model: {rex.Status}:{rex.Message}");
}

// Initialize twin data
BasicDigitalTwin twinData = new BasicDigitalTwin();
twinData.Id = $"room0";
twinData.Metadata.ModelId = "dtmi:com:example:Room;1"; //  data.Value[1].Id;
twinData.Contents["Humidity"] = "50";
await client.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(twinData.Id, twinData);

async static Task CreateRelationship(DigitalTwinsClient client, string srcId, string targetId)
{
    var relationship = new BasicRelationship
    {
        TargetId = targetId,
        Name = "contains"
    };

    try
    {
        string relId = $"{srcId}-contains->{targetId}";
        await client.CreateOrReplaceRelationshipAsync(srcId, relId, relationship);
        Console.WriteLine("Created relationship successfully");
    }
    catch (RequestFailedException rex)
    {
        Console.WriteLine($"Create relationship error: {rex.Status}:{rex.Message}");
    }
}

async static Task ListRelationships(DigitalTwinsClient client, string srcId)
{
    try
    {
        AsyncPageable<BasicRelationship> results = client.GetRelationshipsAsync<BasicRelationship>(srcId);
        Console.WriteLine($"Twin {srcId} is connected to:");
        await foreach (BasicRelationship rel in results)
        {
            Console.WriteLine($" -{rel.Name}->{rel.TargetId}");
        }
    }
    catch (RequestFailedException rex)
    {
        Console.WriteLine($"Relationship retrieval error: {rex.Status}:{rex.Message}");
    }
}
