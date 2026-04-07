FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY ["src/AutoMail.Web.Host/AutoMail.Web.Host.csproj", "src/AutoMail.Web.Host/"]
COPY ["src/AutoMail.Web.Core/AutoMail.Web.Core.csproj", "src/AutoMail.Web.Core/"]
COPY ["src/AutoMail.Application/AutoMail.Application.csproj", "src/AutoMail.Application/"]
COPY ["src/AutoMail.Core/AutoMail.Core.csproj", "src/AutoMail.Core/"]
COPY ["src/AutoMail.EntityFrameworkCore/AutoMail.EntityFrameworkCore.csproj", "src/AutoMail.EntityFrameworkCore/"]
WORKDIR "/src/src/AutoMail.Web.Host"
RUN dotnet restore 

WORKDIR /src
COPY ["src/AutoMail.Web.Host", "src/AutoMail.Web.Host"]
COPY ["src/AutoMail.Web.Core", "src/AutoMail.Web.Core"]
COPY ["src/AutoMail.Application", "src/AutoMail.Application"]
COPY ["src/AutoMail.Core", "src/AutoMail.Core"]
COPY ["src/AutoMail.EntityFrameworkCore", "src/AutoMail.EntityFrameworkCore"]
WORKDIR "/src/src/AutoMail.Web.Host"
RUN dotnet publish -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 80
WORKDIR /app
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "AutoMail.Web.Host.dll"]
