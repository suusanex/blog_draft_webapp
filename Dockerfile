FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Use a non-privileged port by default.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/BlogDraftWebApp/BlogDraftWebApp.csproj src/BlogDraftWebApp/
COPY src/BlogDraftWebApp.Core/BlogDraftWebApp.Core.csproj src/BlogDraftWebApp.Core/
COPY src/BlogDraftWebApp.Api/BlogDraftWebApp.Api.csproj src/BlogDraftWebApp.Api/

RUN dotnet restore src/BlogDraftWebApp/BlogDraftWebApp.csproj

COPY . ./
WORKDIR /src/src/BlogDraftWebApp
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "BlogDraftWebApp.dll"]
