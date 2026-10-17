FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/JellyDaily/JellyDaily.csproj src/JellyDaily/
RUN dotnet restore src/JellyDaily/JellyDaily.csproj
COPY src/ src/
RUN dotnet publish src/JellyDaily/JellyDaily.csproj --no-restore -c Release -o /out
FROM scratch AS artifact
COPY --from=build /out/JellyDaily.dll /JellyDaily.dll
