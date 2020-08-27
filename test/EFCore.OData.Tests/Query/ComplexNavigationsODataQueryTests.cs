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
using Microsoft.EntityFrameworkCore.TestModels.ComplexNavigationsModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ComplexNavigationsODataQueryTests : IClassFixture<ComplexNavigationsODataQueryTestFixture<ComplexNavigationsODataQueryTests>>
    {
        public ComplexNavigationsODataQueryTests(ComplexNavigationsODataQueryTestFixture<ComplexNavigationsODataQueryTests> fixture)
        {
            BaseAddress = fixture.BaseAddress;
            Client = fixture.ClientFactory.CreateClient();
        }

        public string BaseAddress { get; }

        public HttpClient Client { get; }

        protected static void UpdateConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ComplexNavigationsODataContext>(b =>
                b.UseSqlServer(
                    SqlServerTestStore.CreateConnectionString("ComplexNavigations")));
        }

        protected static void UpdateConfigure(EndpointRouteConfiguration configuration)
        {
            var controllers = new Type[]
            {
                typeof(LevelOneController),
                typeof(LevelTwoController),
                typeof(LevelThreeController),
                typeof(LevelFourController),
            };

            configuration.AddControllers(controllers);
            configuration.MaxTop(2).Expand().Select().OrderBy().Filter();

            configuration.MapODataRoute("odata", "odata",
                GetGearsOfWarEdmModel(),
                new DefaultODataPathHandler(),
                ODataRoutingConventions.CreateDefault(),
                new DefaultODataBatchHandler());
        }

        protected static IEdmModel GetGearsOfWarEdmModel()
        {
            var modelBuilder = new ODataConventionModelBuilder();
            modelBuilder.EntitySet<Level1>("LevelOne");
            modelBuilder.EntitySet<Level2>("LevelTwo");
            modelBuilder.EntitySet<Level3>("LevelThree");
            modelBuilder.EntitySet<Level4>("LevelFour");

            return modelBuilder.GetEdmModel();
        }

        [ConditionalFact]
        public async Task Query_level_ones()
        {
            var requestUri = string.Format("{0}/odata/LevelOne", BaseAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsObject<JObject>();

            Assert.Contains("$metadata#LevelOne", result["@odata.context"].ToString());
            var levelOnes = result["value"] as JArray;

            Assert.Equal(13, levelOnes.Count);
        }

        [ConditionalFact]
        public async Task Query_level_threes()
        {
            var requestUri = string.Format("{0}/odata/LevelThree", BaseAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadAsObject<JObject>();

            Assert.Contains("$metadata#LevelThree", result["@odata.context"].ToString());
            var levelThrees = result["value"] as JArray;

            Assert.Equal(10, levelThrees.Count);
        }
    }
}
