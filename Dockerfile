FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so this layer is cached until a package changes.
COPY global.json Directory.Build.props ./
COPY src/Mortgage.Domain/Mortgage.Domain.csproj src/Mortgage.Domain/
COPY src/Mortgage.Infrastructure/Mortgage.Infrastructure.csproj src/Mortgage.Infrastructure/
COPY src/Mortgage.Api/Mortgage.Api.csproj src/Mortgage.Api/
RUN dotnet restore src/Mortgage.Api

COPY src/ src/
RUN dotnet publish src/Mortgage.Api -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The image's non-root user cannot write to a fresh volume, so create the data folder and hand it over.
USER root
RUN mkdir /data && chown $APP_UID /data
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "Mortgage.Api.dll"]
