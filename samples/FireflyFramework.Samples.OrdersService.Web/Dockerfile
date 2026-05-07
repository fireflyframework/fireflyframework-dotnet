FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy CPM + targets first so the restore layer can be cached.
COPY Directory.Build.props Directory.Packages.props Directory.Build.targets global.json NuGet.config ./
COPY src/ src/
COPY samples/ samples/

RUN dotnet restore samples/FireflyFramework.Samples.OrdersService.Web/FireflyFramework.Samples.OrdersService.Web.csproj
RUN dotnet publish samples/FireflyFramework.Samples.OrdersService.Web/FireflyFramework.Samples.OrdersService.Web.csproj \
    -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENTRYPOINT ["dotnet", "FireflyFramework.Samples.OrdersService.Web.dll"]
