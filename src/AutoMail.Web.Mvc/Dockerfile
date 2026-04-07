FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli
RUN ln -s ~/.dotnet/tools/libman /usr/local/bin/libman

WORKDIR /src
COPY ["src/AutoMail.Web.Mvc/AutoMail.Web.Mvc.csproj", "src/AutoMail.Web.Mvc/"]
COPY ["src/AutoMail.Web.Core/AutoMail.Web.Core.csproj", "src/AutoMail.Web.Core/"]
COPY ["src/AutoMail.Application/AutoMail.Application.csproj", "src/AutoMail.Application/"]
COPY ["src/AutoMail.Core/AutoMail.Core.csproj", "src/AutoMail.Core/"]
COPY ["src/AutoMail.EntityFrameworkCore/AutoMail.EntityFrameworkCore.csproj", "src/AutoMail.EntityFrameworkCore/"]
WORKDIR "/src/src/AutoMail.Web.Mvc"
RUN dotnet restore 

WORKDIR /src
COPY ["src/AutoMail.Web.Mvc", "src/AutoMail.Web.Mvc"]
COPY ["src/AutoMail.Web.Core", "src/AutoMail.Web.Core"]
COPY ["src/AutoMail.Application", "src/AutoMail.Application"]
COPY ["src/AutoMail.Core", "src/AutoMail.Core"]
COPY ["src/AutoMail.EntityFrameworkCore", "src/AutoMail.EntityFrameworkCore"]
WORKDIR "/src/src/AutoMail.Web.Mvc"
RUN libman restore
RUN dotnet publish -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 80
WORKDIR /app
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "AutoMail.Web.Mvc.dll"]
