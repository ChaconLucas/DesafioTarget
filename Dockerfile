# Interface web + API. Uso:
#   docker build -t desafio-target .
#   docker run --rm -p 8080:8080 desafio-target
# e abra http://localhost:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet test tests/Desafio.Tests -c Release \
 && dotnet publish src/Desafio.Web -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Roda com o usuário sem privilégios da imagem oficial; só a pasta do histórico precisa de escrita.
RUN mkdir -p /app/App_Data && chown $APP_UID /app/App_Data
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Desafio.Web.dll"]
