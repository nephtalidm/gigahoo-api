FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Gigahoo.Api/Gigahoo.Api.csproj", "Gigahoo.Api/"]
RUN dotnet restore "Gigahoo.Api/Gigahoo.Api.csproj"
COPY . .
RUN dotnet build "Gigahoo.Api/Gigahoo.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gigahoo.Api/Gigahoo.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Gigahoo.Api.dll"]
