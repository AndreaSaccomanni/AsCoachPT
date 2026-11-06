FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Il restore avviene dopo la copia completa: con il solo .csproj gli static web
# assets di Blazor (_framework/blazor.web.js) non vengono generati.
COPY . ./
RUN dotnet restore src/AsCoachPT.UI/AsCoachPT.UI.csproj
RUN dotnet publish src/AsCoachPT.UI/AsCoachPT.UI.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AsCoachPT.UI.dll"]
