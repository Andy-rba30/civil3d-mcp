using System;
using System.Collections.Generic;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>Parámetro de una herramienta tal como lo publica GET /tools.</summary>
    public class Parametro
    {
        public string name { get; set; }
        /// <summary>string | number | boolean | json (json: texto con un valor JSON; el puente lo expone como str).</summary>
        public string type { get; set; }
        public string description { get; set; }
        public bool required { get; set; }
    }

    /// <summary>Nombre, descripción y parámetros de una herramienta (lo que ve el puente en GET /tools).</summary>
    public class DescripcionHerramienta
    {
        public string Nombre;
        public string Descripcion;
        public List<Parametro> Parametros = new List<Parametro>();
    }

    /// <summary>Serialización del catálogo de herramientas para GET /tools y validación de sus parámetros.</summary>
    public static class Catalogo
    {
        public static readonly string[] TiposParametro = { "string", "number", "boolean", "json" };

        public static bool TipoValido(string tipo) => tipo != null && TiposParametro.Contains(tipo);

        /// <summary>Lanza ArgumentException si la herramienta no tiene nombre o algún parámetro tiene tipo desconocido o nombre vacío.</summary>
        public static void Validar(DescripcionHerramienta h)
        {
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (string.IsNullOrWhiteSpace(h.Nombre)) throw new ArgumentException("La herramienta no tiene nombre.");
            foreach (var p in h.Parametros ?? new List<Parametro>())
            {
                if (p == null || string.IsNullOrWhiteSpace(p.name)) throw new ArgumentException("La herramienta '" + h.Nombre + "' tiene un parámetro sin nombre.");
                if (!TipoValido(p.type)) throw new ArgumentException("El parámetro '" + p.name + "' de '" + h.Nombre + "' tiene el tipo '" + p.type + "'; los válidos son " + string.Join(", ", TiposParametro) + ".");
            }
        }

        /// <summary>Lista de {name, description, parameters} en el orden de registro.</summary>
        public static List<object> Describir(IEnumerable<DescripcionHerramienta> herramientas)
            => herramientas.Select(h => (object)new { name = h.Nombre, description = h.Descripcion, parameters = h.Parametros ?? new List<Parametro>() }).ToList();

        /// <summary>Cuerpo completo de GET /tools: {"ok":true,"tools":[...]}.</summary>
        public static string SerializarTools(IEnumerable<DescripcionHerramienta> herramientas)
            => Json.Serializar(new { ok = true, tools = Describir(herramientas) });
    }
}
