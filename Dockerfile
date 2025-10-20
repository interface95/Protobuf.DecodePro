FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY Protobuf.Decode.Web/publish/ .
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "Protobuf.Decode.Web.dll"]

