using System;

namespace Ejemplo.Dibujo
{
    /// <summary>Tipos de superficie que admite la biblioteca falsa.</summary>
    public enum TipoSuperficie
    {
        /// <summary>Superficie de triángulos.</summary>
        Triangulos,
        /// <summary>Superficie de malla regular.</summary>
        Malla
    }

    /// <summary>Objeto base del dibujo falso: todo lo que tiene nombre.</summary>
    public abstract class ObjetoDibujo
    {
        /// <summary>Nombre del objeto en el dibujo.</summary>
        public string Name { get; set; }

        /// <summary>Identificador interno, solo lectura.</summary>
        public long Id { get; }

        /// <summary>Descripción del objeto. Marcado como obsoleto para probar la extracción.</summary>
        [Obsolete("Usa Name en su lugar")]
        public string Description { get; set; }
    }

    /// <summary>Superficie falsa que imita a TinSurface de Civil 3D.</summary>
    public class SuperficieFalsa : ObjetoDibujo
    {
        /// <summary>Tipo de la superficie.</summary>
        public TipoSuperficie Tipo { get; set; }

        /// <summary>Crea una superficie nueva con ese nombre y devuelve su identificador.</summary>
        public static long Create(string nombre, TipoSuperficie tipo) { return nombre == null ? 0 : nombre.Length + (int)tipo; }

        /// <summary>Devuelve la cota de la superficie en un punto.</summary>
        public double FindElevationAtXY(double x, double y) { return x + y; }

        /// <summary>Reconstruye la superficie.</summary>
        public void Rebuild() { }

        /// <summary>Forma antigua de reconstruir, conservada solo por compatibilidad.</summary>
        [Obsolete("Usa Rebuild")]
        public void RebuildOld() { }
    }

    /// <summary>Alineamiento falso con un par de miembros.</summary>
    public class AlineamientoFalso : ObjetoDibujo
    {
        /// <summary>Longitud total del alineamiento.</summary>
        public double Length { get; }

        /// <summary>Añade un vértice de intersección en el PK indicado.</summary>
        public int AddPVI(double pk, double cota, string nota) { return (int)(pk + cota) + (nota ?? "").Length; }

        internal void SoloInterno() { }
        private void SoloPrivado() { }
    }

    /// <summary>Contrato falso para probar que las interfaces también se extraen.</summary>
    public interface IExportable
    {
        /// <summary>Exporta el objeto a la ruta indicada.</summary>
        bool Exportar(string ruta);
    }
}
