// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.Hosting;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class NorthwindODataQueryTestFixture<T> : NorthwindQuerySqlServerFixture<NoopModelCustomizer>
    {
        private IHost _selfHostServer = null;

        public NorthwindODataQueryTestFixture()
        {
            (BaseAddress, ClientFactory, _selfHostServer) = ODataQueryTestFixtureInitializer.Initialize<T>();
        }

        public string BaseAddress { get; private set; }

        public IHttpClientFactory ClientFactory { get; private set; }

        public override void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_selfHostServer != null)
                {
                    _selfHostServer.StopAsync();
                    _selfHostServer.WaitForShutdownAsync();
                    _selfHostServer = null;
                }
            }
        }
    }
}
