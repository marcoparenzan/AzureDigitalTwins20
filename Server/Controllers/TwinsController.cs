using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.DigitalTwins.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TwinApp.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class TwinsController : ControllerBase
    {
        private readonly ILogger<TwinsController> logger;
        private readonly DigitalTwinsClient client;

        public TwinsController(ILogger<TwinsController> logger, DigitalTwinsClient client)
        {
            this.logger = logger;
            this.client = client;
        }

        public record Item(string Id, string ModelId);

        [HttpGet]
        public async Task<IEnumerable<Item>> All()
        {
            var result = client.QueryAsync<BasicDigitalTwin>("SELECT * FROM digitaltwins");

            var items = new List<Item>();
            await foreach (BasicDigitalTwin twin in result)
            {
                items.Add(new Item(twin.Id, twin.Metadata.ModelId));
            }
            return items;
        }
    }
}
