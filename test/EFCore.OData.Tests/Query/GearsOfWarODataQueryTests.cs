// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNet.OData.Batch;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNet.OData.Routing.Conventions;
using Microsoft.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class GearsOfWarODataQueryTests : IClassFixture<GearsOfWarODataQueryTestFixture<GearsOfWarODataQueryTests>>
    {
        public static IEdmModel EdmModel;

        public GearsOfWarODataQueryTests(GearsOfWarODataQueryTestFixture<GearsOfWarODataQueryTests> fixture)
        {
            BaseAddress = fixture.BaseAddress;
            Client = fixture.ClientFactory.CreateClient();
        }

        public string BaseAddress { get; }

        public HttpClient Client { get; }

        protected static void UpdateConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<GearsOfWarODataContext>(b =>
                b.UseSqlServer(
                    SqlServerTestStore.CreateConnectionString("GearsOfWarQueryTest")));
        }

        protected static void UpdateConfigure(EndpointRouteConfiguration configuration)
        {
            var controllers = new Type[]
            {
                typeof(GearsController),
                typeof(SquadsController),
                typeof(TagsController),
                typeof(WeaponsController),
                typeof(CitiesController),
                typeof(MissionsController),
                typeof(SquadMissionsController),
                typeof(FactionsController),
                typeof(LocustLeadersController),
                typeof(LocustHighCommandsController),
            };

            configuration.AddControllers(controllers);
            configuration.MaxTop(2).Expand().Select().OrderBy().Filter();

            // TODO: fix/hack conventions
            var routingConventions = ODataRoutingConventions.CreateDefault();

            configuration.MapODataRoute("odata", "odata",
                GetGearsOfWarEdmModel(),
                new DefaultODataPathHandler(),
                routingConventions,
                new DefaultODataBatchHandler());
        }

        protected static IEdmModel GetGearsOfWarEdmModel()
        {
            var modelBuilder = new ODataConventionModelBuilder();
            modelBuilder.EntitySet<Gear>("Gears");
            modelBuilder.EntityType<Gear>().HasKey(e => new { e.Nickname, e.SquadId });
            modelBuilder.EntitySet<Squad>("Squads");
            modelBuilder.EntitySet<CogTag>("Tags");
            modelBuilder.EntitySet<Weapon>("Weapons");
            modelBuilder.EntitySet<City>("Cities");
            modelBuilder.EntityType<City>().HasKey(c => c.Name);
            modelBuilder.EntitySet<Mission>("Missions");
            modelBuilder.EntitySet<SquadMission>("SquadMissions");
            modelBuilder.EntityType<SquadMission>().HasKey(e => new { e.SquadId, e.MissionId });
            modelBuilder.EntitySet<Faction>("Factions");
            modelBuilder.EntitySet<LocustLeader>("LocustLeaders");
            modelBuilder.EntityType<LocustLeader>().HasKey(c => c.Name);
            modelBuilder.EntitySet<LocustHighCommand>("LocustHighCommands");

            return modelBuilder.GetEdmModel();
        }

        [ConditionalFact]
        public async Task Basic_query_gears()
        {
            var requestUri = string.Format("{0}/odata/Gears", BaseAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsObject<JObject>();

            Assert.Contains("$metadata#Gears", result["@odata.context"].ToString());
            var gears = result["value"] as JArray;

            Assert.Equal(5, gears.Count);
        }

        [ConditionalFact]
        public async Task Basic_query_inheritance()
        {
            var requestUri = string.Format("{0}/odata/Gears/Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel.Officer", BaseAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsObject<JObject>();

            Assert.Contains("$metadata#Gears/Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel.Officer", result["@odata.context"].ToString());
            var gears = result["value"] as JArray;

            Assert.Equal(2, gears.Count);
        }

        [ConditionalFact]
        public async Task Basic_query_single_element_from_set_composite_key()
        {
            var requestUri = string.Format("{0}/odata/Gears(Nickname='Marcus', SquadId=1)", BaseAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadAsObject<JObject>();

            Assert.Contains("$metadata#Gears/Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel.Officer/$entity", result["@odata.context"].ToString());
            Assert.Equal("Marcus", result["Nickname"].ToString());
        }

        [ConditionalFact]
        public async Task Complex_query_with_any_on_collection_navigation()
        {
            var requestUri = string.Format(@"{0}/odata/Gears?$filter=Weapons/any(w: w/Id gt 4)", BaseAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsObject<JObject>();

            Assert.Contains("$metadata#Gears", result["@odata.context"].ToString());
            var officers = result["value"] as JArray;

            Assert.Equal(3, officers.Count);
        }
    }
}
