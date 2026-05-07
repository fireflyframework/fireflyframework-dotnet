using FireflyFramework.Data.Domain;
using FireflyFramework.Data.Filters;
using FireflyFramework.Data.Pagination;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class CustomerEntity : BaseEntity<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class DataFilterTests
{
    private static List<CustomerEntity> Seed() => new()
    {
        new() { Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Smith", Country = "US", Balance = 1000m },
        new() { Id = Guid.NewGuid(), FirstName = "Bob", LastName = "Jones", Country = "UK", Balance = 250m },
        new() { Id = Guid.NewGuid(), FirstName = "Carol", LastName = "Smith", Country = "US", Balance = 5000m },
    };

    [Fact]
    public async Task GenericFilter_returns_paginated_response()
    {
        var data = Seed().AsQueryable();
        var filter = new GenericFilter<CustomerEntity, CustomerEntity, CustomerEntity>(c => c);
        var request = new FilterRequest<CustomerEntity>
        {
            Filters = new() { ["Country"] = "US" },
            Pagination = new PaginationRequest { PageNumber = 0, PageSize = 10 },
        };

        var result = await filter.FilterAsync(
            data, request,
            countAsync: (q, _) => Task.FromResult((long)q.Count()),
            toListAsync: (q, _) => Task.FromResult(q.ToList()));

        result.TotalElements.Should().Be(2);
        result.Content.Should().OnlyContain(c => c.Country == "US");
    }

    [Fact]
    public async Task RangeFilter_filters_by_decimal_range()
    {
        var data = Seed().AsQueryable();
        var filter = new GenericFilter<CustomerEntity, CustomerEntity, CustomerEntity>(c => c);
        var request = new FilterRequest<CustomerEntity>
        {
            RangeFilters = new RangeFilter
            {
                Ranges = new()
                {
                    ["Balance"] = new RangeFilter.Range { From = 500m, To = 6000m },
                },
            },
        };

        var result = await filter.FilterAsync(
            data, request,
            countAsync: (q, _) => Task.FromResult((long)q.Count()),
            toListAsync: (q, _) => Task.FromResult(q.ToList()));
        result.TotalElements.Should().Be(2);
    }

    [Fact]
    public async Task Sorting_by_LastName_descending()
    {
        var data = Seed().AsQueryable();
        var filter = new GenericFilter<CustomerEntity, CustomerEntity, CustomerEntity>(c => c);
        var request = new FilterRequest<CustomerEntity>
        {
            Pagination = new PaginationRequest { PageSize = 10, SortBy = "LastName", SortDirection = SortDirection.Desc },
        };

        var result = await filter.FilterAsync(
            data, request,
            countAsync: (q, _) => Task.FromResult((long)q.Count()),
            toListAsync: (q, _) => Task.FromResult(q.ToList()));

        result.Content.First().LastName.Should().Be("Smith");
    }
}
