using System;
using System.Collections.Generic;

namespace TiposBarraPeru.Reglas
{
    /// <summary>Un Material ya presente en el proyecto (solo lo que necesita el plan).</summary>
    public class MaterialExistente
    {
        public string Nombre;
        /// <summary>Clase de material (MaterialClass); vacia si no la tiene.</summary>
        public string Clase;

        public MaterialExistente() { }
        public MaterialExistente(string nombre, string clase = null) { Nombre = nombre; Clase = clase ?? ""; }
    }

    /// <summary>Una fila de la tabla de materiales de concreto.</summary>
    public class FilaPlanConcreto
    {
        public ConcretoCatalogo Concreto;
        /// <summary>true si viene de "catalogoExtra" (anadida desde la ventana).</summary>
        public bool EsExtra;
        /// <summary>Nombre final del material (prefijo + f'c).</summary>
        public string Nombre;
        public EstadoFila Estado;
        public bool Crear;
        public MaterialExistente Existente;
        public string Aviso;
        public ValoresConcreto Valores;

        public string TextoEstado => Planificador.TextoEstado(Estado, Aviso);
        public bool Seleccionable => Estado == EstadoFila.SeCreara || Estado == EstadoFila.SeActualizara;
    }

    /// <summary>
    /// Decide, sin tocar Revit, que se hace con cada resistencia del catalogo y de los
    /// extras: nombre resultante frente a los materiales del proyecto, rango de f'c y
    /// densidad, nombres repetidos y validos. Mismo criterio que Planificador (barras).
    /// </summary>
    public static class PlanificadorConcreto
    {
        public static List<FilaPlanConcreto> Planificar(ConfiguracionMateriales cfg, string prefijo, IEnumerable<MaterialExistente> existentes,
                                                        bool actualizarExistentes, Func<string, bool> nombreValido = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var reglas = new ReglasConcreto(cfg.Reglas, cfg.Termico);
            var lista = new List<MaterialExistente>(existentes ?? new MaterialExistente[0]);
            var filas = new List<FilaPlanConcreto>();

            foreach (ConcretoCatalogo c in cfg.Catalogo) filas.Add(Fila(c, false, prefijo, reglas));
            foreach (ConcretoCatalogo c in cfg.CatalogoExtra) filas.Add(Fila(c, true, prefijo, reglas));

            foreach (FilaPlanConcreto fila in filas)
            {
                fila.Existente = lista.Find(e => e != null && Nombres.Iguales(e.Nombre, fila.Nombre));
                string motivo = reglas.MotivoFueraDeNorma(fila.Concreto.FcKgCm2, fila.Concreto.DensidadKgM3);
                bool duplicado = filas.Exists(o => o != fila && Nombres.Iguales(o.Nombre, fila.Nombre));

                if (motivo != null)
                {
                    fila.Estado = EstadoFila.FueraDeNorma;
                    fila.Aviso = motivo;
                    fila.Crear = false;
                }
                else if (duplicado)
                {
                    fila.Estado = EstadoFila.NombreDuplicado;
                    fila.Crear = false;
                }
                else if (nombreValido != null && !nombreValido(fila.Nombre))
                {
                    fila.Estado = EstadoFila.NombreInvalido;
                    fila.Crear = false;
                }
                else if (fila.Existente == null)
                {
                    fila.Estado = EstadoFila.SeCreara;
                    fila.Crear = true;
                }
                else
                {
                    fila.Estado = actualizarExistentes ? EstadoFila.SeActualizara : EstadoFila.YaExiste;
                    fila.Crear = actualizarExistentes;
                    string clase = (fila.Existente.Clase ?? "").Trim();
                    if (clase.Length > 0 && !Nombres.Iguales(clase, cfg.ClaseMaterial))
                        fila.Aviso = "clase " + clase;
                }
            }
            return filas;
        }

        private static FilaPlanConcreto Fila(ConcretoCatalogo c, bool extra, string prefijo, ReglasConcreto reglas) => new FilaPlanConcreto
        {
            Concreto = c,
            EsExtra = extra,
            Nombre = Nombres.GenerarConcreto(prefijo, c.FcKgCm2),
            Valores = reglas.Calcular(c.FcKgCm2, c.DensidadKgM3)
        };

        /// <summary>
        /// Comprueba una resistencia nueva antes de anadirla a "catalogoExtra". Devuelve
        /// el texto del error, o null si se puede anadir: f'c y densidad dentro de
        /// rango, nombre resultante admitido por Revit y sin chocar con el catalogo,
        /// con otro extra ni con un material del proyecto.
        /// </summary>
        public static string ValidarNuevoConcreto(ConfiguracionMateriales cfg, string prefijo, double fcKgCm2, double densidadKgM3,
                                                  IEnumerable<MaterialExistente> existentes, Func<string, bool> nombreValido = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var reglas = new ReglasConcreto(cfg.Reglas, cfg.Termico);
            string motivo = reglas.MotivoFueraDeNorma(fcKgCm2, densidadKgM3);
            if (motivo != null) return motivo;

            string completo = Nombres.GenerarConcreto(prefijo, fcKgCm2);
            if (nombreValido != null && !nombreValido(completo)) return "Revit no admite el nombre \"" + completo + "\"";

            string n = Nombres.NombreCatalogoConcreto(fcKgCm2);
            foreach (ConcretoCatalogo c in cfg.Catalogo)
                if (Nombres.Iguales(c.NombreCatalogo, n)) return "f'c " + n + " ya esta en el catalogo";
            foreach (ConcretoCatalogo c in cfg.CatalogoExtra)
                if (Nombres.Iguales(c.NombreCatalogo, n)) return "f'c " + n + " ya esta en la lista";
            if (existentes != null)
                foreach (MaterialExistente e in existentes)
                    if (e != null && Nombres.Iguales(e.Nombre, completo)) return "el proyecto ya tiene un material \"" + completo + "\"";
            return null;
        }
    }
}
