FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Correla.IndustryFlows.Api/Correla.IndustryFlows.Api.csproj", "Correla.IndustryFlows.Api/"]
RUN dotnet restore "Correla.IndustryFlows.Api/Correla.IndustryFlows.Api.csproj"
COPY . .
WORKDIR "/src/Correla.IndustryFlows.Api"
RUN dotnet build "./Correla.IndustryFlows.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Correla.IndustryFlows.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Correla.IndustryFlows.Api.dll"]
