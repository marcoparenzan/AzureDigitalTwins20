using System;
using System.Text.Json;
using Azure.DigitalTwins.Core;
using Azure.Identity;

using static System.Console;

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    SharedTokenCacheTenantId = ""
});
var client = new DigitalTwinsClient(new Uri("https://***.api.***.digitaltwins.azure.net"), credential);

var twins = client.QueryAsync<JsonElement>("select $dtId from digitaltwins where IS_PRIMITIVE($dtId)").AsPages();
await foreach (var xx in twins)
{
    foreach (var yy in xx.Values)
    {
        var twinId = yy.GetProperty("$dtId").GetString();
        var rels = client.GetRelationshipsAsync<BasicRelationship>(twinId).AsPages();

        await foreach (var zz in rels)
        {
            foreach (var ww in zz.Values)
            {
                WriteLine($"Deleting rel {twinId}/{ww.Id}");
                await client.DeleteRelationshipAsync(twinId, ww.Id);
            }
            if (string.IsNullOrWhiteSpace(zz.ContinuationToken)) break;
        }

        var rels1 = client.GetIncomingRelationshipsAsync(twinId).AsPages();
        await foreach (var zz in rels1)
        {
            foreach (var ww in zz.Values)
            {
                WriteLine($"Deleting incoming rel {ww.SourceId}/{ww.RelationshipId}");
                await client.DeleteRelationshipAsync(ww.SourceId, ww.RelationshipId);
            }
            if (string.IsNullOrWhiteSpace(zz.ContinuationToken)) break;
        }

        WriteLine($"Deleting twin {twinId}");
        await client.DeleteDigitalTwinAsync(twinId);
    }
    if (string.IsNullOrWhiteSpace(xx.ContinuationToken)) break;
}

var models = client.GetModelsAsync().AsPages();

var count = 0;
while (true)
{
    var exceptions = false;
    WriteLine($"Cleaning models loop {++count}");
    await foreach (var xx in models)
    {
        foreach (var zz in xx.Values)
        {
            try
            {
                await client.DeleteModelAsync(zz.Id);
                WriteLine($"Deleted model {zz.Id}");
            }
            catch (Exception ex)
            {
                WriteLine($"Exception deleting model {zz.Id}: {ex.Message}");
                exceptions = true;
            }
        }

        if (string.IsNullOrWhiteSpace(xx.ContinuationToken)) break;
    }
    if (!exceptions) break;
}
