// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using FireflyFramework.Data.Pagination;
using FireflyFramework.Utils.Annotations;

namespace FireflyFramework.Data.Filters;

/// <summary>
/// Reflective filter builder for any <see cref="IQueryable{TEntity}"/>. Mirrors Java
/// <c>FilterUtils.GenericFilter</c>: takes a <see cref="FilterRequest{T}"/> and produces a
/// filtered, paginated and projected <see cref="PaginationResponse{TDto}"/>.
/// </summary>
public sealed class GenericFilter<TFilter, TEntity, TDto>
    where TFilter : class
    where TEntity : class
{
    private readonly Func<TEntity, TDto> _mapper;
    private readonly FilterOptions _options;

    public GenericFilter(Func<TEntity, TDto> mapper, FilterOptions? options = null)
    {
        _mapper = mapper;
        _options = options ?? new FilterOptions();
    }

    public async Task<PaginationResponse<TDto>> FilterAsync(
        IQueryable<TEntity> source,
        FilterRequest<TFilter> request,
        Func<IQueryable<TEntity>, CancellationToken, Task<long>> countAsync,
        Func<IQueryable<TEntity>, CancellationToken, Task<List<TEntity>>> toListAsync,
        CancellationToken ct = default)
    {
        var filtered = ApplyFilters(source, request);
        var sorted = ApplySort(filtered, request.Pagination);
        var paginated = sorted.Skip(request.Pagination.Skip).Take(request.Pagination.PageSize);

        var totalTask = countAsync(filtered, ct);
        var pageTask = toListAsync(paginated, ct);
        await Task.WhenAll(totalTask, pageTask).ConfigureAwait(false);

        var total = totalTask.Result;
        var items = pageTask.Result.Select(_mapper).ToList();
        var pageSize = Math.Max(1, request.Pagination.PageSize);
        return new PaginationResponse<TDto>
        {
            Content = items,
            TotalElements = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            CurrentPage = request.Pagination.PageNumber,
            PageSize = pageSize,
        };
    }

    private IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> source, FilterRequest<TFilter> request)
    {
        var props = typeof(TEntity).GetProperties(_options.IncludeInheritedFields
            ? BindingFlags.Public | BindingFlags.Instance
            : BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var (rawName, rawValue) in request.Filters)
        {
            var prop = props.FirstOrDefault(p => string.Equals(p.Name, rawName, StringComparison.OrdinalIgnoreCase));
            if (prop is null)
            {
                continue;
            }

            // Skip *Id properties unless they are explicitly opted in via [FilterableId]
            if (prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase)
                && prop.GetCustomAttribute<FilterableIdAttribute>() is null)
            {
                continue;
            }

            source = ApplyFilter(source, prop, rawValue);
        }

        foreach (var (key, range) in request.RangeFilters.Ranges)
        {
            var prop = props.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            if (prop is null)
            {
                continue;
            }

            source = ApplyRange(source, prop, range);
        }

        return source;
    }

    private IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> source, PropertyInfo prop, object? rawValue)
    {
        if (rawValue is null)
        {
            return source;
        }

        var entity = Expression.Parameter(typeof(TEntity), "e");
        var member = Expression.Property(entity, prop);

        // Null/Not-null markers
        if (rawValue is string s)
        {
            if (s == FilterRequest<TFilter>.NullValue)
            {
                var nullExpr = Expression.Equal(member, Expression.Constant(null, prop.PropertyType));
                return source.Where(Expression.Lambda<Func<TEntity, bool>>(nullExpr, entity));
            }

            if (s == FilterRequest<TFilter>.NotNullValue)
            {
                var notNull = Expression.NotEqual(member, Expression.Constant(null, prop.PropertyType));
                return source.Where(Expression.Lambda<Func<TEntity, bool>>(notNull, entity));
            }

            // String LIKE (Contains)
            if (prop.PropertyType == typeof(string))
            {
                var contains = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
                var value = _options.CaseInsensitiveStrings ? s.ToLowerInvariant() : s;
                Expression target = member;
                Expression valueExpr = Expression.Constant(value);
                if (_options.CaseInsensitiveStrings)
                {
                    var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
                    target = Expression.Call(member, toLower);
                }

                var call = Expression.Call(target, contains, valueExpr);
                return source.Where(Expression.Lambda<Func<TEntity, bool>>(call, entity));
            }
        }

        if (rawValue is IEnumerable enumerable && prop.PropertyType != typeof(string))
        {
            var list = enumerable.Cast<object?>().ToList();
            var listExpr = Expression.Constant(list);
            var contains = typeof(List<object?>).GetMethod("Contains", new[] { typeof(object) })!;
            var converted = Expression.Convert(member, typeof(object));
            var call = Expression.Call(listExpr, contains, converted);
            return source.Where(Expression.Lambda<Func<TEntity, bool>>(call, entity));
        }

        var equality = Expression.Equal(member, Expression.Constant(Convert.ChangeType(rawValue, prop.PropertyType), prop.PropertyType));
        return source.Where(Expression.Lambda<Func<TEntity, bool>>(equality, entity));
    }

    private static IQueryable<TEntity> ApplyRange(IQueryable<TEntity> source, PropertyInfo prop, RangeFilter.Range range)
    {
        var entity = Expression.Parameter(typeof(TEntity), "e");
        var member = Expression.Property(entity, prop);
        Expression? predicate = null;
        if (range.From is not null)
        {
            var fromConst = Expression.Constant(Convert.ChangeType(range.From, prop.PropertyType), prop.PropertyType);
            predicate = Expression.GreaterThanOrEqual(member, fromConst);
        }

        if (range.To is not null)
        {
            var toConst = Expression.Constant(Convert.ChangeType(range.To, prop.PropertyType), prop.PropertyType);
            var upper = Expression.LessThanOrEqual(member, toConst);
            predicate = predicate is null ? upper : Expression.AndAlso(predicate, upper);
        }

        if (predicate is null)
        {
            return source;
        }

        return source.Where(Expression.Lambda<Func<TEntity, bool>>(predicate, entity));
    }

    private static IQueryable<TEntity> ApplySort(IQueryable<TEntity> source, PaginationRequest pagination)
    {
        if (string.IsNullOrEmpty(pagination.SortBy))
        {
            return source;
        }

        var prop = typeof(TEntity).GetProperty(pagination.SortBy,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null)
        {
            return source;
        }

        var entity = Expression.Parameter(typeof(TEntity), "e");
        var member = Expression.Property(entity, prop);
        var lambda = Expression.Lambda(member, entity);
        var method = pagination.SortDirection == SortDirection.Asc ? "OrderBy" : "OrderByDescending";
        var call = Expression.Call(typeof(Queryable), method,
            new[] { typeof(TEntity), prop.PropertyType }, source.Expression, lambda);
        return source.Provider.CreateQuery<TEntity>(call);
    }
}
