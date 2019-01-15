﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Update.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Update.Internal;
using Newtonsoft.Json.Linq;

namespace Microsoft.EntityFrameworkCore.Cosmos.Storage.Internal
{
    public class CosmosDatabaseWrapper : Database
    {
        private readonly Dictionary<IEntityType, DocumentSource> _documentCollections
            = new Dictionary<IEntityType, DocumentSource>();

        private readonly CosmosClientWrapper _cosmosClient;
        private readonly bool _sensitiveLoggingEnabled;

        public CosmosDatabaseWrapper(
            DatabaseDependencies dependencies,
            CosmosClientWrapper cosmosClient,
            ILoggingOptions loggingOptions)
            : base(dependencies)
        {
            _cosmosClient = cosmosClient;

            if (loggingOptions.IsSensitiveDataLoggingEnabled)
            {
                _sensitiveLoggingEnabled = true;
            }
        }

        public override int SaveChanges(IReadOnlyList<IUpdateEntry> entries)
        {
            var rowsAffected = 0;
            var entriesSaved = new HashSet<IUpdateEntry>();
            var rootEntriesToSave = new HashSet<IUpdateEntry>();

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var entityType = entry.EntityType;

                Debug.Assert(!entityType.IsAbstract());

                if (!entityType.IsDocumentRoot())
                {
                    var root = GetRootDocument((InternalEntityEntry)entry);
                    if (!entriesSaved.Contains(root)
                        && rootEntriesToSave.Add(root)
                        && root.EntityState == EntityState.Unchanged)
                    {
                        ((InternalEntityEntry)root).SetEntityState(EntityState.Modified);
                    }

                    continue;
                }

                entriesSaved.Add(entry);
                if (Save(entry))
                {
                    rowsAffected++;
                }
            }

            foreach (var rootEntry in rootEntriesToSave)
            {
                if (!entriesSaved.Contains(rootEntry)
                    && Save(rootEntry))
                {
                    rowsAffected++;
                }
            }

            return rowsAffected;
        }

        public override async Task<int> SaveChangesAsync(
            IReadOnlyList<IUpdateEntry> entries, CancellationToken cancellationToken = default)
        {
            var rowsAffected = 0;
            var entriesSaved = new HashSet<IUpdateEntry>();
            var rootEntriesToSave = new HashSet<IUpdateEntry>();

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var entityType = entry.EntityType;

                Debug.Assert(!entityType.IsAbstract());
                if (!entityType.IsDocumentRoot())
                {
                    var root = GetRootDocument((InternalEntityEntry)entry);
                    if (!entriesSaved.Contains(root)
                        && rootEntriesToSave.Add(root)
                        && root.EntityState == EntityState.Unchanged)
                    {
                        ((InternalEntityEntry)root).SetEntityState(EntityState.Modified);
                    }

                    continue;
                }

                entriesSaved.Add(entry);
                if (await SaveAsync(entry, cancellationToken))
                {
                    rowsAffected++;
                }
            }

            foreach (var rootEntry in rootEntriesToSave)
            {
                if (!entriesSaved.Contains(rootEntry)
                    && await SaveAsync(rootEntry, cancellationToken))
                {
                    rowsAffected++;
                }
            }

            return rowsAffected;
        }

        private bool Save(IUpdateEntry entry)
        {
            var entityType = entry.EntityType;
            var documentSource = GetDocumentSource(entityType);
            var collectionId = documentSource.GetCollectionId();
            var state = entry.EntityState;

            if (entry.SharedIdentityEntry != null)
            {
                if (entry.EntityState == EntityState.Deleted)
                {
                    return false;
                }

                if (state == EntityState.Added)
                {
                    state = EntityState.Modified;
                }
            }

            switch (state)
            {
                case EntityState.Added:
                    var newDocument = documentSource.CreateDocument(entry);
                    newDocument["__partitionKey"] = "0";
                    return _cosmosClient.CreateItem(collectionId, newDocument);
                case EntityState.Modified:
                    var jObjectProperty = entityType.FindProperty(StoreKeyConvention.JObjectPropertyName);
                    var document = jObjectProperty != null
                        ? (JObject)(entry.SharedIdentityEntry ?? entry).GetCurrentValue(jObjectProperty)
                        : null;
                    if (document != null)
                    {
                        if (documentSource.UpdateDocument(document, entry) == null)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        document = documentSource.CreateDocument(entry);
                        document["__partitionKey"] = "0";

                        document[entityType.Cosmos().DiscriminatorProperty.Cosmos().PropertyName] =
                            JToken.FromObject(entityType.Cosmos().DiscriminatorValue);
                    }

                    return _cosmosClient.ReplaceItem(
                        collectionId, documentSource.GetId(entry.SharedIdentityEntry ?? entry), document);
                case EntityState.Deleted:
                    return _cosmosClient.DeleteItem(collectionId, documentSource.GetId(entry));
                default:
                    return false;
            }
        }

        private Task<bool> SaveAsync(IUpdateEntry entry, CancellationToken cancellationToken)
        {
            var entityType = entry.EntityType;
            var documentSource = GetDocumentSource(entityType);
            var collectionId = documentSource.GetCollectionId();
            var state = entry.EntityState;

            if (entry.SharedIdentityEntry != null)
            {
                if (entry.EntityState == EntityState.Deleted)
                {
                    return Task.FromResult(false);
                }

                if (state == EntityState.Added)
                {
                    state = EntityState.Modified;
                }
            }

            switch (state)
            {
                case EntityState.Added:
                    var newDocument = documentSource.CreateDocument(entry);
                    newDocument["__partitionKey"] = "0";
                    return _cosmosClient.CreateItemAsync(collectionId, newDocument, cancellationToken);
                case EntityState.Modified:
                    var jObjectProperty = entityType.FindProperty(StoreKeyConvention.JObjectPropertyName);
                    var document = jObjectProperty != null
                        ? (JObject)(entry.SharedIdentityEntry ?? entry).GetCurrentValue(jObjectProperty)
                        : null;
                    if (document != null)
                    {
                        if (documentSource.UpdateDocument(document, entry) == null)
                        {
                            return Task.FromResult(false);
                        }
                    }
                    else
                    {
                        document = documentSource.CreateDocument(entry);
                        document["__partitionKey"] = "0";

                        document[entityType.Cosmos().DiscriminatorProperty.Cosmos().PropertyName] =
                            JToken.FromObject(entityType.Cosmos().DiscriminatorValue);
                    }

                    return _cosmosClient.ReplaceItemAsync(
                        collectionId, documentSource.GetId(entry.SharedIdentityEntry ?? entry), document, cancellationToken);
                case EntityState.Deleted:
                    return _cosmosClient.DeleteItemAsync(collectionId, documentSource.GetId(entry), cancellationToken);
                default:
                    return Task.FromResult(false);
            }
        }

        public DocumentSource GetDocumentSource(IEntityType entityType)
        {
            if (!_documentCollections.TryGetValue(entityType, out var documentSource))
            {
                _documentCollections.Add(
                    entityType, documentSource = new DocumentSource(entityType, this));
            }

            return documentSource;
        }

        private IUpdateEntry GetRootDocument(InternalEntityEntry entry)
        {
            var stateManager = entry.StateManager;
            var ownership = entry.EntityType.FindOwnership();
            var principal = stateManager.GetPrincipal(entry, ownership);
            if (principal == null)
            {
                if (_sensitiveLoggingEnabled)
                {
                    throw new InvalidOperationException(
                        CosmosStrings.OrphanedNestedDocumentSensitive(
                            entry.EntityType.DisplayName(),
                            ownership.PrincipalEntityType.DisplayName(),
                            entry.BuildCurrentValuesString(entry.EntityType.FindPrimaryKey().Properties)));
                }

                throw new InvalidOperationException(
                    CosmosStrings.OrphanedNestedDocument(
                        entry.EntityType.DisplayName(),
                        ownership.PrincipalEntityType.DisplayName()));
            }

            return principal.EntityType.IsDocumentRoot() ? principal : GetRootDocument(principal);
        }
    }
}
