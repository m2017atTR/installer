FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Installer.Core.csproj", "./"]
COPY ["../Directory.Packages.props", "."]
COPY ["../Directory.Build.props", "."]
RUN dotnet restore "Installer.Core.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "Installer.Core.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Installer.Core.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Installer.Core.dll"]