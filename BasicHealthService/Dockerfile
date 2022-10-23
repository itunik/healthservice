FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["BasicHealthService/BasicHealthService.csproj", "BasicHealthService/"]
RUN dotnet restore "BasicHealthService/BasicHealthService.csproj"
COPY . .
WORKDIR "/src/BasicHealthService"
RUN dotnet build "BasicHealthService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BasicHealthService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BasicHealthService.dll"]
