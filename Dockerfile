FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props RestaurantSeating.sln ./
COPY src/Api/Api.csproj src/Api/
COPY src/BusinessLogic/BusinessLogic.csproj src/BusinessLogic/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY tests/BusinessLogic.Unit/BusinessLogic.Unit.csproj tests/BusinessLogic.Unit/
COPY tests/Api.Integration/Api.Integration.csproj tests/Api.Integration/
RUN dotnet restore RestaurantSeating.sln
COPY . .
RUN dotnet publish src/Api/Api.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM build AS tests
ENTRYPOINT ["dotnet", "test", "RestaurantSeating.sln", "--no-restore", "-c", "Release", "--logger", "console;verbosity=normal"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
