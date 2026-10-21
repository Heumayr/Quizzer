# Quizzer.Server im Container. Gebaut wird aus dem Wurzelordner des Repos:
#   docker build -f Quizzer.Server/Dockerfile -t quizzer-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Erst nur die Projektdateien - die Paketwiederherstellung bleibt dann im Zwischenspeicher,
# solange sich keine Abhaengigkeit aendert.
COPY Quizzer.DataModels/Quizzer.DataModels.csproj Quizzer.DataModels/
COPY Quizzer.Logic/Quizzer.Logic.csproj Quizzer.Logic/
COPY Quizzer.Server/Quizzer.Server.csproj Quizzer.Server/
RUN dotnet restore Quizzer.Server/Quizzer.Server.csproj

COPY Quizzer.DataModels/ Quizzer.DataModels/
COPY Quizzer.Logic/ Quizzer.Logic/
COPY Quizzer.Server/ Quizzer.Server/
RUN dotnet publish Quizzer.Server/Quizzer.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Nicht als root: das Abbild bringt den Benutzer "app" mit.
RUN mkdir -p /data/media && chown -R app:app /data
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    Quizzer__MediaRoot=/data/media \
    DOTNET_gcServer=0

EXPOSE 8080
ENTRYPOINT ["dotnet", "Quizzer.Server.dll"]
