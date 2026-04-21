FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY Pasukhi.sln .
COPY src/Pasukhi.API/Pasukhi.API.csproj src/Pasukhi.API/
COPY src/Pasukhi.Application/Pasukhi.Application.csproj src/Pasukhi.Application/
COPY src/Pasukhi.Domain/Pasukhi.Domain.csproj src/Pasukhi.Domain/
COPY src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj src/Pasukhi.Infrastructure/

RUN dotnet restore src/Pasukhi.API/Pasukhi.API.csproj

COPY src/ src/

RUN dotnet publish src/Pasukhi.API/Pasukhi.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Pasukhi.API.dll"]
