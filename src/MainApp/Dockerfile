FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MainApp.csproj", "./"]
COPY ["../Installer.Core/Installer.Core.csproj", "../Installer.Core/"]
COPY ["../Directory.Packages.props", "."]
COPY ["../Directory.Build.props", "."]
RUN dotnet restore "MainApp.csproj"
COPY . .
COPY ["../Installer.Core", "../Installer.Core"]
WORKDIR "/src"
RUN dotnet build "MainApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MainApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MainApp.dll"]