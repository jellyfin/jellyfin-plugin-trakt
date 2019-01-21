ARG DOTNET_VERSION=2


FROM microsoft/dotnet:${DOTNET_VERSION}-sdk as builder
WORKDIR /repo
COPY . .
RUN export DOTNET_CLI_TELEMETRY_OPTOUT=1 \
 && dotnet clean \
 && dotnet publish --configuration release --framework netstandard2.0 --output /plugins
