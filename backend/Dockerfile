# --- build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY OstQuiz.Api/OstQuiz.Api.csproj OstQuiz.Api/
RUN dotnet restore OstQuiz.Api/OstQuiz.Api.csproj
COPY OstQuiz.Api/ OstQuiz.Api/
RUN dotnet publish OstQuiz.Api/OstQuiz.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
# ffmpeg generates the per-step trimmed audio clips; icu-libs enables full globalization.
RUN apk add --no-cache ffmpeg icu-libs
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OstQuiz.Api.dll"]
