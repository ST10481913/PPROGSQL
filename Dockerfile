# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer is cached until dependencies change.
COPY RaceDay.Web.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage (small image, runs as a non-root user) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Api__BaseUrl=http://host.docker.internal:5080/

EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
