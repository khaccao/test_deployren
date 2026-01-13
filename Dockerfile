# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PerfectKeyV1-Clean.sln .

COPY PerfectKeyV1.Api/*.csproj PerfectKeyV1.Api/
COPY PerfectKeyV1.Application/*.csproj PerfectKeyV1.Application/
COPY PerfectKeyV1.Domain/*.csproj PerfectKeyV1.Domain/
COPY PerfectKeyV1.Infrastructure/*.csproj PerfectKeyV1.Infrastructure/

RUN dotnet restore

COPY . .
WORKDIR /src/PerfectKeyV1.Api
RUN dotnet publish -c Release -o /app/publish

# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "PerfectKeyV1.Api.dll"]
