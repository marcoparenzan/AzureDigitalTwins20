using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;

using static System.Console;

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    SharedTokenCacheTenantId = ""
});
var client = new DigitalTwinsClient(new Uri("https://***.api.****.digitaltwins.azure.net"), credential);

var backgroundColor = BackgroundColor;
var foregroundColor = ForegroundColor;

while (true)
{
    var query = prompt();
    if (query is "exit") break;

    try
    {
        var pages = client.QueryAsync<JsonElement>(query).AsPages(pageSizeHint: 10);
        title($"Searching...");

        var count = 0;
        await foreach (var page in pages)
        {
            QueryChargeHelper.TryGetQueryCharge(page, out var charge);
            if (count == 0)
            {
                Clear();
                title($"Found some pages...");
            }
            title($"Charge: {charge}");
            foreach (var row in page.Values)
            {
                WriteLine(row.ToString());
            }

            if (string.IsNullOrWhiteSpace(page.ContinuationToken)) break;
            if (!canContinue($"Page {++count}")) break;
        }
    }
    catch (RequestFailedException ex)
    {
        requestFailedException(ex);
    }
    catch (Exception ex)
    {
        genericException(ex);
    }
}

string prompt()
{
    BackgroundColor = ConsoleColor.Cyan;
    ForegroundColor = ConsoleColor.Black;

    Write("now what>");

    BackgroundColor = backgroundColor;
    ForegroundColor = foregroundColor;

    var prompt = ReadLine().Trim();

    return prompt;
}

void title(string text)
{
    BackgroundColor = ConsoleColor.DarkGreen;
    ForegroundColor = ConsoleColor.Black;

    WriteLine(text);

    BackgroundColor = backgroundColor;
    ForegroundColor = foregroundColor;
}

bool canContinue(string prompt)
{
    BackgroundColor = ConsoleColor.Green;
    ForegroundColor = ConsoleColor.Black;

    Write($"{prompt}[continue YES|no] ");

    BackgroundColor = backgroundColor;
    ForegroundColor = foregroundColor;

    var continueAnswer = ReadLine().Trim();
    if (string.IsNullOrWhiteSpace(continueAnswer)) return true;
    if (string.Compare(continueAnswer, "yes", true) == 0) return true;
    return false;
}

void genericException(Exception ex)
{
    BackgroundColor = ConsoleColor.Red;
    ForegroundColor = ConsoleColor.Black;

    if (ex.InnerException != null)
        WriteLine(ex.InnerException.Message);
    else
        WriteLine(ex.Message);

    BackgroundColor = backgroundColor;
    ForegroundColor = foregroundColor;
}

void requestFailedException(RequestFailedException ex)
{
    BackgroundColor = ConsoleColor.Red;
    ForegroundColor = ConsoleColor.Black;

    if (ex.InnerException != null)
        WriteLine(ex.InnerException.Message);
    else
        WriteLine(ex.Message);

    BackgroundColor = backgroundColor;
    ForegroundColor = foregroundColor;
}