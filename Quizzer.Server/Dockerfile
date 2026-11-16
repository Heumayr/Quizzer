# Quizzer.Server im Container. Gebaut wird aus dem Wurzelordner des Repos:
#   docker build -f Quizzer.Server/Dockerfile -t quizzer-server .

# Fest auf den Digest (A03, V02): 10.0 war am 2026-11-16 die Fassung 10.0.15 vom 2026-11-10.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS runtime
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
