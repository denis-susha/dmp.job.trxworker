FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore in a separate layer so it is cached until project files change
COPY ["global.json", "Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Job.TrxWorker/Job.TrxWorker.csproj", "Job.TrxWorker/"]
COPY ["DMP.BL/DMP.BL.csproj", "DMP.BL/"]
COPY ["DMP.Crosscutting/DMP.Crosscutting.csproj", "DMP.Crosscutting/"]
COPY ["DMP.DataAccess/DMP.DataAccess.csproj", "DMP.DataAccess/"]
RUN dotnet restore "Job.TrxWorker/Job.TrxWorker.csproj"

COPY . .
RUN dotnet publish "Job.TrxWorker/Job.TrxWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Job.TrxWorker.dll"]
