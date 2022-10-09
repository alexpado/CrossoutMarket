# Install all dependencies
FROM debian:10-slim as mono
RUN apt-get update && apt-get -y install apt-transport-https wget dirmngr gnupg apt-transport-https ca-certificates
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
RUN sh -c 'echo "deb https://download.mono-project.com/repo/debian stable-buster main" > /etc/apt/sources.list.d/mono-official-stable.list'
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
RUN apt-get update && apt-get -y install mono-complete dotnet-sdk-3.0 aspnetcore-runtime-3.0
RUN rm -rf /var/apt/list
WORKDIR /app
COPY Crossout.AspWeb/out .
ENTRYPOINT ["dotnet", "Crossout.AspWeb.dll"]