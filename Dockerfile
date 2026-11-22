FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Saas.Agenda.slnx ./
COPY src/Saas.Agenda.Domain/Saas.Agenda.Domain.csproj src/Saas.Agenda.Domain/
COPY src/Saas.Agenda.Application/Saas.Agenda.Application.csproj src/Saas.Agenda.Application/
COPY src/Saas.Agenda.Infrastructure/Saas.Agenda.Infrastructure.csproj src/Saas.Agenda.Infrastructure/
COPY src/Saas.Agenda.Api/Saas.Agenda.Api.csproj src/Saas.Agenda.Api/
COPY tests/Saas.Agenda.UnitTests/Saas.Agenda.UnitTests.csproj tests/Saas.Agenda.UnitTests/

RUN dotnet restore Saas.Agenda.slnx

COPY src/ src/
COPY tests/ tests/

RUN dotnet build Saas.Agenda.slnx -c Release --no-restore

FROM build AS test
RUN dotnet test Saas.Agenda.slnx -c Release --no-build --verbosity normal

FROM build AS publish
RUN dotnet publish src/Saas.Agenda.Api/Saas.Agenda.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=
EXPOSE 8080

COPY --from=publish /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "Saas.Agenda.Api.dll"]
