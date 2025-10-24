# 构建阶段
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY WebApplication1.csproj ./
RUN dotnet restore "WebApplication1.csproj"
COPY . .
RUN dotnet build "WebApplication1.csproj" -c Release -o /app/build

# 发布阶段
FROM build AS publish
RUN dotnet publish "WebApplication1.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "WebApplication1.dll"]