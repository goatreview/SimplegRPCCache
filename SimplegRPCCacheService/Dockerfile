#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SimplegRPCCacheService/SimplegRPCCacheService.csproj", "SimplegRPCCacheService/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["InMemoryCacheLib/InMemoryCacheLib.csproj", "InMemoryCacheLib/"]
RUN dotnet restore "SimplegRPCCacheService/SimplegRPCCacheService.csproj"
COPY . .
WORKDIR "/src/SimplegRPCCacheService"
RUN dotnet build "SimplegRPCCacheService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "SimplegRPCCacheService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SimplegRPCCacheService.dll"]