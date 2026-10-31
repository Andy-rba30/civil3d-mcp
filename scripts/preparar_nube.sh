#!/usr/bin/env bash
# =====================================================================================
# preparar_nube.sh: deja listo el contenedor de Claude Code web para trabajar en civil3d-mcp
# =====================================================================================
#
# PARA QUÉ SIRVE
#   Claude Code web (claude.ai/code) trabaja en un contenedor Linux limpio que no trae .NET.
#   Este script instala lo que hace falta para correr las tres pruebas de la regla 6 de
#   CLAUDE.md sin Civil 3D:
#     - .NET SDK 10 (para `dotnet test ArbaMcp.Pruebas` y `dotnet build herramientas-dev/CompilarSinCivil`)
#     - uv (para `uv lock --check` y `uv run --frozen pytest -q` en PuenteMcp)
#     - las dependencias del puente (mcp, httpx, uvicorn, pytest) instaladas con pip, por si
#       hace falta correr Python fuera de uv
#   No instala ni necesita las DLL de Autodesk: el plugin ArbaMcp sigue compilándose solo en la
#   PC con Civil 3D 2027.
#
# CÓMO USARLO
#   1. Entra en claude.ai/code, abre los ajustes del entorno (Environments) y edita el entorno
#      que usas para este repositorio.
#   2. Copia TODO el contenido de este archivo y pégalo en el campo "Setup script". Guarda.
#   3. Cada sesión nueva ejecutará el script antes de empezar. Tarda unos 2 o 3 minutos la
#      primera vez y unos segundos cuando ya está todo instalado.
#   También se puede ejecutar a mano en una sesión: `bash scripts/preparar_nube.sh`.
#
# CÓMO SE COMPORTA
#   - No depende de ningún archivo del repositorio: se puede pegar tal cual.
#   - Cada paso imprime "OK: ..." si salió bien o "AVISO: ..." con qué falló y cómo seguir a mano.
#   - SIEMPRE termina con código 0, para que un fallo parcial no impida abrir la sesión.
#   - Funciona como root (el caso normal en la nube) y, si no lo es, usa `sudo -n`.
#   - Probado en Ubuntu 24.04 con python3 3.13, pip y uv ya instalados. No usa dotnet-install.sh
#     porque dot.net y builds.dotnet.microsoft.com no son alcanzables desde este contenedor; usa
#     el paquete dotnet-sdk-10.0 de Ubuntu y, como respaldo, el repositorio packages.microsoft.com.
# =====================================================================================

export DEBIAN_FRONTEND=noninteractive
INICIO=$(date +%s)

# Elegir cómo ejecutar comandos de administrador: directo si somos root, con sudo -n si no.
if [ "$(id -u)" = "0" ]; then
  ADMIN=""
else
  ADMIN="sudo -n"
fi

segundos() { echo "$(( $(date +%s) - INICIO )) s"; }

# -------------------------------------------------------------------------------------
# Paso 1: .NET SDK 10
# -------------------------------------------------------------------------------------
tiene_dotnet10() {
  command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'
}

if tiene_dotnet10; then
  echo "OK: .NET SDK 10 ya estaba instalado ($(dotnet --version 2>/dev/null))."
else
  echo "... Instalando .NET SDK 10 (paquete dotnet-sdk-10.0 de Ubuntu)."
  if $ADMIN apt-get update -qq >/dev/null 2>&1 \
     && $ADMIN apt-get install -y -qq dotnet-sdk-10.0 >/dev/null 2>&1 \
     && tiene_dotnet10; then
    echo "OK: .NET SDK 10 instalado desde los repositorios de Ubuntu ($(dotnet --version)) [$(segundos)]."
  else
    echo "... El paquete de Ubuntu no sirvió; probando con el repositorio de Microsoft."
    TMP_DEB=$(mktemp /tmp/packages-microsoft-prod.XXXXXX.deb)
    if curl -fsSL -o "$TMP_DEB" https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
       && $ADMIN dpkg -i "$TMP_DEB" >/dev/null 2>&1 \
       && $ADMIN apt-get update -qq >/dev/null 2>&1 \
       && $ADMIN apt-get install -y -qq dotnet-sdk-10.0 >/dev/null 2>&1 \
       && tiene_dotnet10; then
      echo "OK: .NET SDK 10 instalado desde packages.microsoft.com ($(dotnet --version)) [$(segundos)]."
    else
      echo "AVISO: no se pudo instalar .NET SDK 10. Las pruebas 'dotnet test' y 'dotnet build' no"
      echo "       funcionarán en esta sesión. Para seguir a mano, ejecuta:"
      echo "         apt-get update && apt-get install -y dotnet-sdk-10.0"
      echo "       y, si el paquete no existe, añade antes el repositorio de Microsoft:"
      echo "         curl -fsSL -o /tmp/ms.deb https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb"
      echo "         dpkg -i /tmp/ms.deb && apt-get update && apt-get install -y dotnet-sdk-10.0"
    fi
    rm -f "$TMP_DEB"
  fi
fi

# -------------------------------------------------------------------------------------
# Paso 2: uv (gestor de paquetes de Python que usa el puente)
# -------------------------------------------------------------------------------------
# Las instalaciones de pip pueden quedar en ~/.local/bin; lo añadimos al PATH por si acaso.
export PATH="$HOME/.local/bin:$PATH"

if command -v uv >/dev/null 2>&1; then
  echo "OK: uv ya estaba instalado ($(uv --version 2>/dev/null))."
else
  echo "... Instalando uv con pip."
  if python3 -m pip install -q --break-system-packages uv >/dev/null 2>&1 \
     || python3 -m pip install -q uv >/dev/null 2>&1; then
    if command -v uv >/dev/null 2>&1; then
      echo "OK: uv instalado ($(uv --version 2>/dev/null)) [$(segundos)]."
    else
      echo "AVISO: pip instaló uv pero no está en el PATH. Para seguir a mano:"
      echo "         export PATH=\"\$HOME/.local/bin:\$PATH\""
    fi
  else
    echo "AVISO: no se pudo instalar uv con pip. Las pruebas del puente (uv lock --check,"
    echo "       uv run --frozen pytest -q) no funcionarán. Para seguir a mano:"
    echo "         python3 -m pip install uv"
  fi
fi

# -------------------------------------------------------------------------------------
# Paso 3: dependencias del puente, por nombre (no se lee pyproject.toml a propósito)
# -------------------------------------------------------------------------------------
echo "... Instalando con pip las dependencias del puente: mcp, httpx, uvicorn, pytest."
if python3 -m pip install -q --break-system-packages "mcp>=2.2,<3" httpx uvicorn pytest >/dev/null 2>&1 \
   || python3 -m pip install -q "mcp>=2.2,<3" httpx uvicorn pytest >/dev/null 2>&1; then
  echo "OK: dependencias del puente instaladas con pip [$(segundos)]."
else
  echo "AVISO: pip no pudo instalar las dependencias del puente. 'uv run --frozen pytest -q' las"
  echo "       instala por su cuenta, así que las pruebas pueden funcionar igual. Para seguir a mano:"
  echo "         python3 -m pip install \"mcp>=2.2,<3\" httpx uvicorn pytest"
fi

# -------------------------------------------------------------------------------------
# Paso 4: resumen
# -------------------------------------------------------------------------------------
echo "------------------------------------------------------------"
echo "Resumen de preparar_nube.sh (tiempo total: $(segundos))"
if command -v dotnet >/dev/null 2>&1; then
  echo "  dotnet : $(dotnet --version 2>/dev/null || echo 'instalado, pero dotnet --version falló')"
else
  echo "  dotnet : NO disponible"
fi
if command -v uv >/dev/null 2>&1; then
  echo "  uv     : $(uv --version 2>/dev/null)"
else
  echo "  uv     : NO disponible"
fi
if command -v python3 >/dev/null 2>&1; then
  echo "  python : $(python3 --version 2>/dev/null)"
else
  echo "  python : NO disponible"
fi
echo "------------------------------------------------------------"

# Siempre terminamos bien, para no bloquear la sesión por un paso fallido.
exit 0
