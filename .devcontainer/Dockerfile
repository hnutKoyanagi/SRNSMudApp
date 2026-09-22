FROM mcr.microsoft.com/devcontainers/dotnet:11.0-preview

# Install SQL Tools: cross-platform sqlcmd (arm64 & amd64 support)
RUN ARCH=$(uname -m) && \
    case "$ARCH" in \
    x86_64) SQLCMD_ARCH="amd64" ;; \
    aarch64) SQLCMD_ARCH="arm64" ;; \
    *) echo "Unsupported architecture: $ARCH" && exit 1 ;; \
    esac && \
    curl -fsSL "https://github.com/microsoft/go-sqlcmd/releases/latest/download/sqlcmd-linux-${SQLCMD_ARCH}.tar.bz2" -o /tmp/sqlcmd.tar.bz2 && \
    tar -xjf /tmp/sqlcmd.tar.bz2 -C /usr/local/bin sqlcmd && \
    chmod +x /usr/local/bin/sqlcmd && \
    mkdir -p /opt/mssql-tools18/bin && \
    ln -sf /usr/local/bin/sqlcmd /opt/mssql-tools18/bin/sqlcmd && \
    rm -f /tmp/sqlcmd.tar.bz2

ARG DOTNET_SDK_VERSION=11.0.100-rc.1.26425.128

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --version "$DOTNET_SDK_VERSION" --install-dir /usr/share/dotnet \
    && rm /tmp/dotnet-install.sh
# [Optional] Uncomment this section to install additional OS packages.
# RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
#     && apt-get -y install --no-install-recommends <your-package-list-here>

# [Optional] Uncomment this line to install global node packages.
RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && nvm install --lts && nvm use --lts && npm install -g typescript" 2>&1
# RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && npm install -g <your-package-here>" 2>&1
