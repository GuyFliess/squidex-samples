﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Squidex.ClientLibrary.Utils;

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression

namespace Squidex.ClientLibrary
{
    public sealed class ContentsClient<TEntity, TData> : SquidexClientBase, IContentsClient<TEntity, TData> where TEntity : Content<TData> where TData : class, new()
    {
        public string SchemaName { get; }

        public ContentsClient(SquidexOptions options, string schemaName, HttpClient httpClient)
            : base(options, httpClient)
        {
            Guard.NotNullOrEmpty(schemaName, nameof(schemaName));

            SchemaName = schemaName;
        }

        public async Task GetAllAsync(int batchSize, Func<TEntity, Task> callback, CancellationToken ct = default)
        {
            Guard.NotNull(callback, nameof(callback));

            var query = new ContentQuery { Top = batchSize };

            var added = new HashSet<string>();
            do
            {
                var isAnyAdded = false;

                var getResult = await GetAsync(query);

                foreach (var item in getResult.Items)
                {
                    if (added.Add(item.Id))
                    {
                        await callback(item);

                        isAnyAdded = true;
                    }
                }

                if (!isAnyAdded)
                {
                    break;
                }

                query.Skip = added.Count;
            }
            while (!ct.IsCancellationRequested);
        }

        public async Task<TResponse> GraphQlGetAsync<TResponse>(object request, QueryContext context = null, CancellationToken ct = default)
        {
            Guard.NotNull(request, nameof(request));

            var query = BuildQuery(request);

            var response = await RequestJsonAsync<GraphQlResponse<TResponse>>(HttpMethod.Get, BuildAppUrl("graphql", false, context) + query, null, context, ct);

            if (response.Errors?.Length > 0)
            {
                throw new SquidexGraphQlException(response.Errors);
            }

            return response.Data;
        }

        public async Task<TResponse> GraphQlAsync<TResponse>(object request, QueryContext context = null, CancellationToken ct = default)
        {
            Guard.NotNull(request, nameof(request));

            var response = await RequestJsonAsync<GraphQlResponse<TResponse>>(HttpMethod.Post, BuildAppUrl("graphql", false, context), request.ToContent(), context, ct);

            if (response.Errors?.Length > 0)
            {
                throw new SquidexGraphQlException(response.Errors);
            }

            return response.Data;
        }

        public Task<ContentsResult<TEntity, TData>> GetAsync(HashSet<string> ids, QueryContext context = null, CancellationToken ct = default)
        {
            Guard.NotNull(ids, nameof(ids));
            Guard.NotNullOrEmpty(ids, nameof(ids));

            var q = $"?ids={string.Join(",", ids)}";

            return RequestJsonAsync<ContentsResult<TEntity, TData>>(HttpMethod.Get, BuildAppUrl(q, true, context), null, context, ct);
        }

        public Task<ContentsResult<TEntity, TData>> GetAsync(ContentQuery query = null, QueryContext context = null, CancellationToken ct = default)
        {
            var q = query?.ToQuery(true) ?? string.Empty;

            return RequestJsonAsync<ContentsResult<TEntity, TData>>(HttpMethod.Get, BuildSchemaUrl(q, true, context), null, context, ct);
        }

        public Task<TEntity> GetAsync(string id, QueryContext context = null, CancellationToken ct = default)
        {
            Guard.NotNullOrEmpty(id, nameof(id));

            return RequestJsonAsync<TEntity>(HttpMethod.Get, BuildSchemaUrl($"{id}/", true, context), null, context, ct);
        }

        public Task<TEntity> CreateAsync(TData data, bool publish = false, CancellationToken ct = default)
        {
            Guard.NotNull(data, nameof(data));

            return RequestJsonAsync<TEntity>(HttpMethod.Post, BuildSchemaUrl($"?publish={publish}", false), data.ToContent(), ct: ct);
        }

        public Task<TEntity> CreateAsync(TData data, string id, bool publish = false, CancellationToken ct = default)
        {
            Guard.NotNull(data, nameof(data));

            return RequestJsonAsync<TEntity>(HttpMethod.Post, BuildSchemaUrl($"?publish={publish}&id={id ?? string.Empty}", false), data.ToContent(), ct: ct);
        }

        public Task<TEntity> CreateDraftAsync(string id, CancellationToken ct = default)
        {
            Guard.NotNullOrEmpty(id, nameof(id));

            return RequestJsonAsync<TEntity>(HttpMethod.Post, BuildSchemaUrl($"{id}/draft", false), ct: ct);
        }

        public Task<TEntity> CreateDraftAsync(TEntity entity, CancellationToken ct = default)
        {
            Guard.NotNull(entity, nameof(entity));

            return CreateDraftAsync(entity.Id, ct);
        }

        public Task<TEntity> DeleteDraftAsync(string id, CancellationToken ct = default)
        {
            Guard.NotNullOrEmpty(id, nameof(id));

            return RequestJsonAsync<TEntity>(HttpMethod.Delete, BuildSchemaUrl($"{id}/draft", false), ct: ct);
        }

        public Task<TEntity> DeleteDraftAsync(TEntity entity, CancellationToken ct = default)
        {
            Guard.NotNull(entity, nameof(entity));

            return DeleteDraftAsync(entity.Id, ct);
        }

        public async Task<TEntity> UpsertAsync(string id, TData data, bool publish = false, CancellationToken ct = default)
        {
            Guard.NotNullOrEmpty(id, nameof(id));
            Guard.NotNull(data, nameof(data));

            return await RequestJsonAsync<TEntity>(HttpMethod.Post, BuildSchemaUrl($"{id}?publish={publish}", false), data.ToContent(), ct: ct);
        }

        public Task<TEntity> UpsertAsync(TEntity entity, bool publish = false, CancellationToken ct = default)
        {
            Guard.NotNull(entity, nameof(entity));

            return UpsertAsync(entity.Id, entity.Data, publish, ct);
        }

        public async Task<TEntity> UpdateAsync(string id, TData data, CancellationToken ct = default)
        {
            Guard.NotNullOrEmpty(id, nameof(id));
            Guard.NotNull(data, nameof(data));

            return await RequestJsonAsync<TEntity>(HttpMethod.Put, BuildSchemaUrl($"{id}", false), data.ToContent(), ct: ct);
        }

        public Task<TEntity> UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            Guard.NotNull(entity, nameof(entity));

            return UpdateAsync(entity.Id, entity.Data, ct);
        }

        public Task<List<BulkResult>> BulkUpdateAsync(BulkUpdate update, CancellationToken ct = default)
        {
            Guard.NotNull(update, nameof(update));

            return RequestJsonAsync<List<BulkResult>>(HttpMethod.Post, BuildSchemaUrl("bulk", false), update.ToContent(), ct: ct);
        }

        public Task<TEntity> PatchAsync<TPatch>(string id, TPatch patch, CancellationToken ct = default)
        {
            Guard.NotNullOrEmpty(id, nameof(id));
            Guard.NotNull(patch, nameof(patch));

            return RequestJsonAsync<TEntity>(HttpMethodEx.Patch, BuildSchemaUrl($"{id}/", false), patch.ToContent(), ct: ct);
        }

        public Task<TEntity> PatchAsync<TPatch>(TEntity entity, TPatch patch, CancellationToken ct = default)
        {
            Guard.NotNull(entity, nameof(entity));

            return PatchAsync(entity.Id, patch, ct);
        }

        public Task<TEntity> ChangeStatusAsync(string id, string status, CancellationToken ct = default)
        {
            Guard.NotNull(id, nameof(id));
            Guard.NotNull(status, nameof(status));

            return RequestJsonAsync<TEntity>(HttpMethod.Put, BuildSchemaUrl($"{id}/status", false), new { status }.ToContent(), ct: ct);
        }

        public Task<TEntity> ChangeStatusAsync(TEntity entity, string status, CancellationToken ct = default)
        {
            Guard.NotNull(entity, nameof(entity));

            return ChangeStatusAsync(entity.Id, status, ct);
        }

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            Guard.NotNullOrEmpty(id, nameof(id));

            return RequestAsync(HttpMethod.Delete, BuildSchemaUrl($"{id}/", false), ct: ct);
        }

        public async Task DeleteAsync(TEntity entity, CancellationToken ct = default)
        {
            Guard.NotNull(entity, nameof(entity));

            await DeleteAsync(entity.Id, ct);
        }

        private string BuildSchemaUrl(string path, bool query, QueryContext context = null)
        {
            if (ShouldUseCDN(query, context))
            {
                return $"{Options.ContentCDN}/{ApplicationName}/{SchemaName}/{path}";
            }
            else
            {
                return $"content/{ApplicationName}/{SchemaName}/{path}";
            }
        }

        private string BuildAppUrl(string path, bool query, QueryContext context = null)
        {
            if (ShouldUseCDN(query, context))
            {
                return $"{Options.ContentCDN}/{ApplicationName}/{path}";
            }
            else
            {
                return $"content/{ApplicationName}/{path}";
            }
        }

        private static string BuildQuery(object request)
        {
            var parameters = JObject.FromObject(request);

            var queryBuilder = new StringBuilder();

            foreach (var kvp in parameters)
            {
                if (queryBuilder.Length > 0)
                {
                    queryBuilder.Append("&");
                }
                else
                {
                    queryBuilder.Append("?");
                }

                queryBuilder.Append(kvp.Key);
                queryBuilder.Append("=");
                queryBuilder.Append(Uri.EscapeUriString(kvp.Value.ToString()));
            }

            return queryBuilder.ToString();
        }

        private bool ShouldUseCDN(bool query, QueryContext context)
        {
            return query && !string.IsNullOrWhiteSpace(Options.ContentCDN) && context?.IsNotUsingCDN != true;
        }
    }
}
