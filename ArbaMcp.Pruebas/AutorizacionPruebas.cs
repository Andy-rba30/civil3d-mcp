using System.Collections.Generic;
using System.Text;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class AutorizacionPruebas
    {
        private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const int Puerto = 8765;

        private static Dictionary<string, string> Cabeceras(string host = "127.0.0.1:8765", string token = Token, string contentType = "application/json", string origin = null)
        {
            var c = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (host != null) c["Host"] = host;
            if (token != null) c["X-Arba-Token"] = token;
            if (contentType != null) c["Content-Type"] = contentType;
            if (origin != null) c["Origin"] = origin;
            return c;
        }

        [Fact]
        public void PeticionCorrectaSeAutoriza()
        {
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir("POST", "/execute", Cabeceras(), Puerto, Token));
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir("GET", "/tools", Cabeceras(contentType: null), Puerto, Token));
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir("GET", "/tools", Cabeceras(host: "localhost:8765", contentType: null), Puerto, Token));
        }

        [Fact]
        public void OriginResponde403()
            => Assert.Equal(403, Autorizacion.Decidir("GET", "/ping", Cabeceras(origin: "http://localhost:3000"), Puerto, Token));

        [Fact]
        public void HostDistintoResponde400()
        {
            Assert.Equal(400, Autorizacion.Decidir("GET", "/tools", Cabeceras(host: "127.0.0.1:9999"), Puerto, Token));
            Assert.Equal(400, Autorizacion.Decidir("GET", "/tools", Cabeceras(host: "example.com:8765"), Puerto, Token));
            Assert.Equal(400, Autorizacion.Decidir("GET", "/tools", Cabeceras(host: null), Puerto, Token));
        }

        [Fact]
        public void TokenAusenteODistintoResponde401()
        {
            Assert.Equal(401, Autorizacion.Decidir("GET", "/tools", Cabeceras(token: null), Puerto, Token));
            Assert.Equal(401, Autorizacion.Decidir("GET", "/tools", Cabeceras(token: "otro"), Puerto, Token));
            Assert.Equal(401, Autorizacion.Decidir("POST", "/execute", Cabeceras(token: Token.ToUpperInvariant()), Puerto, Token));
            Assert.Equal(401, Autorizacion.Decidir("POST", "/execute", Cabeceras(token: Token.Insert(10, " ")), Puerto, Token));
            Assert.Equal(401, Autorizacion.Decidir("POST", "/execute", Cabeceras(token: " " + Token + " "), Puerto, Token));
        }

        [Fact]
        public void PostSinJsonResponde415()
        {
            Assert.Equal(415, Autorizacion.Decidir("POST", "/execute", Cabeceras(contentType: "text/plain"), Puerto, Token));
            Assert.Equal(415, Autorizacion.Decidir("POST", "/execute", Cabeceras(contentType: null), Puerto, Token));
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir("POST", "/execute", Cabeceras(contentType: "application/json; charset=utf-8"), Puerto, Token));
        }

        [Fact]
        public void ElOrdenEsOriginHostTokenContentType()
        {
            // Todo mal a la vez: gana Origin; sin Origin gana Host; sin Host malo gana el token
            Assert.Equal(403, Autorizacion.Decidir("POST", "/execute", Cabeceras(host: "x", token: null, contentType: null, origin: "http://a"), Puerto, Token));
            Assert.Equal(400, Autorizacion.Decidir("POST", "/execute", Cabeceras(host: "x", token: null, contentType: null), Puerto, Token));
            Assert.Equal(401, Autorizacion.Decidir("POST", "/execute", Cabeceras(token: null, contentType: null), Puerto, Token));
        }

        [Fact]
        public void PingSinTokenEsPublicoPeroElRestoNo()
        {
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir("GET", "/ping", Cabeceras(token: null, contentType: null), Puerto, Token));
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir("GET", "/ping/", Cabeceras(token: "malo", contentType: null), Puerto, Token));
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir("GET", "/", Cabeceras(token: null, contentType: null), Puerto, Token));
            Assert.Equal(401, Autorizacion.Decidir("GET", "/tools", Cabeceras(token: null, contentType: null), Puerto, Token));
            Assert.Equal(401, Autorizacion.Decidir("POST", "/ping", Cabeceras(token: null), Puerto, Token));
            Assert.Equal(403, Autorizacion.Decidir("GET", "/ping", Cabeceras(token: null, origin: "http://a"), Puerto, Token));
            Assert.Equal(400, Autorizacion.Decidir("GET", "/ping", Cabeceras(host: "otro:1", token: null), Puerto, Token));
        }

        [Fact]
        public void TokenIgualEsExactoYAdmiteNulos()
        {
            Assert.True(Autorizacion.TokenIgual(Token, Token));
            Assert.False(Autorizacion.TokenIgual(Token, Token.Substring(1)));
            Assert.False(Autorizacion.TokenIgual(Token.ToUpperInvariant(), Token));
            Assert.False(Autorizacion.TokenIgual(null, Token));
            Assert.False(Autorizacion.TokenIgual(Token, null));
            Assert.False(Autorizacion.TokenIgual("", Token));
        }

        [Fact]
        public void LasCabecerasSeRecortanAlAnalizar()
        {
            string texto = "GET /tools?x=1 HTTP/1.1\r\nHost:   127.0.0.1:8765  \r\nX-Arba-Token:  " + Token + " \r\n";
            Assert.True(Http.IntentarAnalizarCabeceras(texto, out var p, out _));
            Assert.Equal(Autorizacion.Autorizada, Autorizacion.Decidir(p, Puerto, Token));
        }
    }

    public class HttpPruebas
    {
        [Fact]
        public void AnalizaLineaDePeticionYCabeceras()
        {
            string texto = "post /execute/?a=b HTTP/1.1\r\nHost: 127.0.0.1:8765\r\ncontent-length: 12\r\nContent-Type: application/json\r\nSinDosPuntos\r\n";
            Assert.True(Http.IntentarAnalizarCabeceras(texto, out var p, out string error));
            Assert.Null(error);
            Assert.Equal("POST", p.Metodo);
            Assert.Equal("/execute/", p.Ruta);
            Assert.Equal("/execute/?a=b", p.RutaCompleta);
            Assert.Equal(12, p.LargoCuerpo);
            Assert.Equal("application/json", p.Cabecera("CONTENT-TYPE"));
            Assert.Null(p.Cabecera("SinDosPuntos"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("\r\n\r\n")]
        [InlineData("GET")]
        [InlineData("GET \r\nHost: a\r\n")]
        [InlineData(" /ruta\r\n")]
        public void PeticionMalformadaSeRechaza(string texto)
        {
            Assert.False(Http.IntentarAnalizarCabeceras(texto, out var p, out string error));
            Assert.Null(p);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void ContentLengthNoNumericoVale0()
        {
            Assert.True(Http.IntentarAnalizarCabeceras("POST /execute HTTP/1.1\r\nContent-Length: doce\r\n", out var p, out _));
            Assert.Equal(0, p.LargoCuerpo);
            Assert.True(Http.IntentarAnalizarCabeceras("POST /execute HTTP/1.1\r\nContent-Length: -5\r\n", out p, out _));
            Assert.Equal(0, p.LargoCuerpo);
        }

        [Fact]
        public void BuscaElFinDeCabeceras()
        {
            var datos = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: a\r\n\r\n{\"x\":1}");
            Assert.Equal(23, Http.BuscarFinCabeceras(datos, datos.Length));
            Assert.Equal(-1, Http.BuscarFinCabeceras(datos, 10));
            Assert.Equal(-1, Http.BuscarFinCabeceras(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: a\r\n"), 24));
        }

        [Fact]
        public void ConstruyeRespuestasConEstadoYLongitud()
        {
            string texto = Encoding.UTF8.GetString(Http.ConstruirRespuesta(401, Http.Error("Unauthorized")));
            Assert.StartsWith("HTTP/1.1 401 Unauthorized\r\n", texto);
            Assert.Contains("Content-Type: application/json; charset=utf-8\r\n", texto);
            Assert.Contains("Content-Length: " + Encoding.UTF8.GetByteCount("{\"ok\":false,\"error\":\"Unauthorized\"}") + "\r\n", texto);
            Assert.EndsWith("\r\n\r\n{\"ok\":false,\"error\":\"Unauthorized\"}", texto);

            string vacia = Encoding.UTF8.GetString(Http.ConstruirRespuesta(204, ""));
            Assert.StartsWith("HTTP/1.1 204 No Content\r\n", vacia);
            Assert.EndsWith("Content-Length: 0\r\nConnection: close\r\n\r\n", vacia);
            Assert.Equal("Internal Server Error", Http.TextoEstado(500));
        }

        [Fact]
        public void ErrorConservaTildesSinEscapar()
            => Assert.Equal("{\"ok\":false,\"error\":\"Petición inválida\"}", Http.Error("Petición inválida"));
    }
}
