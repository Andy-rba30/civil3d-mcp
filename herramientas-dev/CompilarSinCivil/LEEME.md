# CompilarSinCivil: compilar el plugin sin Civil 3D

`ArbaMcp.csproj` necesita las DLL de Civil 3D 2027 (`acdbmgd`, `acmgd`, `accoremgd`, `AeccDbMgd`, `AecBaseMgd`,
`AdWindows`), así que ni la integración continua ni un agente sin Civil 3D pueden compilarlo. Este proyecto compila
**los mismos archivos `.cs` del plugin** contra unos sustitutos mínimos (`Stubs\AutoCAD.cs`, `Stubs\Civil3D.cs`) que
solo declaran los tipos y miembros que el plugin usa, con firmas plausibles y cuerpos vacíos.

```powershell
dotnet build herramientas-dev\CompilarSinCivil
```

Qué demuestra: que el código propio del plugin es sintácticamente correcto y coherente (nombres de ayudantes,
`using`, tipos del núcleo, firmas entre archivos). Es lo que detectó, por ejemplo, un `using System.Linq` que faltaba
al extraer `ArbaMcp.Nucleo`.

Qué **no** demuestra: que los miembros de la API de Civil 3D existan con esas firmas ni que funcionen. Los sustitutos
se escribieron a mano a partir del código; si el plugin empieza a usar un miembro nuevo, hay que añadirlo aquí (el
compilador lo dice con `CS1061`/`CS0117`) **y** a `herramientas-dev/miembros_por_verificar_civil3d.md` como
`por verificar`. La validación real sigue siendo `instalar.ps1` en un equipo con Civil 3D y los prompts
`VALIDACION_*.md`.

En `.github/workflows/pruebas.yml` se ejecuta como tercer paso del trabajo `nucleo`, con los errores de nombre y tipo
(`CS0103`, `CS0246`, `CS1061`, `CS0117`) tratados como fallo.
