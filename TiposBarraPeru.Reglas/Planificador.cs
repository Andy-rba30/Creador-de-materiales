using System;
using System.Collections.Generic;
using System.Globalization;

namespace TiposBarraPeru.Reglas
{
    public enum EstadoFila
    {
        /// <summary>No hay ningun tipo con ese nombre: se creara.</summary>
        SeCreara,
        /// <summary>Ya hay un tipo con ese nombre y no se ha pedido actualizar: se omite.</summary>
        YaExiste,
        /// <summary>Ya hay un tipo con ese nombre y se ha pedido actualizar: se reescriben sus valores.</summary>
        SeActualizara,
        /// <summary>Revit no admite el nombre resultante.</summary>
        NombreInvalido,
        /// <summary>El diametro queda fuera del rango que cubre la norma: no se crea.</summary>
        FueraDeNorma,
        /// <summary>Otra fila de la tabla produce el mismo nombre: no se crea.</summary>
        NombreDuplicado
    }

    /// <summary>Un RebarBarType ya presente en el proyecto (solo lo que necesita el plan).</summary>
    public class TipoExistente
    {
        public string Nombre;
        public double DiametroMm;

        public TipoExistente() { }
        public TipoExistente(string nombre, double diametroMm) { Nombre = nombre; DiametroMm = diametroMm; }
    }

    /// <summary>Una fila de la tabla de la ventana.</summary>
    public class FilaPlan
    {
        public BarraCatalogo Barra;
        /// <summary>true si viene de "catalogoExtra" (anadida desde la ventana), false si es del catalogo original.</summary>
        public bool EsExtra;
        /// <summary>Nombre final del tipo (prefijo + nombre de catalogo).</summary>
        public string Nombre;
        public EstadoFila Estado;
        /// <summary>Valor inicial de la casilla "crear" (el usuario puede cambiarlo si el estado lo permite).</summary>
        public bool Crear;
        /// <summary>Tipo existente con ese nombre, si lo hay.</summary>
        public TipoExistente Existente;
        /// <summary>Aviso para la columna de estado (p. ej. el existente tiene otro diametro, o el motivo de fuera de norma).</summary>
        public string Aviso;
        public ValoresE060 Valores;

        /// <summary>Texto de la columna "estado".</summary>
        public string TextoEstado => Planificador.TextoEstado(Estado, Aviso);

        /// <summary>true si la casilla "crear" puede marcarse con el estado actual.</summary>
        public bool Seleccionable => Estado == EstadoFila.SeCreara || Estado == EstadoFila.SeActualizara;
    }

    /// <summary>
    /// Decide, sin tocar Revit, que se hace con cada entrada del catalogo y de los
    /// extras: comparar el nombre resultante con los tipos existentes (sin distinguir
    /// mayusculas), comprobar el rango de la norma y marcar el estado. Idempotente:
    /// por defecto solo se crean los que faltan.
    /// </summary>
    public static class Planificador
    {
        /// <summary>Texto de la columna "estado" de una fila (barras y concreto), con su aviso entre parentesis.</summary>
        public static string TextoEstado(EstadoFila estado, string aviso)
        {
            string t;
            switch (estado)
            {
                case EstadoFila.SeCreara: t = "se creara"; break;
                case EstadoFila.YaExiste: t = "ya existe"; break;
                case EstadoFila.SeActualizara: t = "se actualizara"; break;
                case EstadoFila.FueraDeNorma: t = "no se crea"; break;
                case EstadoFila.NombreDuplicado: t = "nombre repetido en la tabla"; break;
                default: t = "nombre no valido"; break;
            }
            return string.IsNullOrEmpty(aviso) ? t : t + " (" + aviso + ")";
        }

        /// <param name="cfg">Configuracion (catalogo, extras, reglas, simbolo de pulgada, tolerancia).</param>
        /// <param name="prefijo">Prefijo de nombre elegido en la ventana.</param>
        /// <param name="existentes">Tipos de barra del proyecto.</param>
        /// <param name="actualizarExistentes">true = los existentes se reescriben en vez de omitirse.</param>
        /// <param name="nombreValido">Validador de nombres de Revit (NamingUtils.IsValidName); null = todos validos.</param>
        public static List<FilaPlan> Planificar(Configuracion cfg, string prefijo, IEnumerable<TipoExistente> existentes,
                                                bool actualizarExistentes, Func<string, bool> nombreValido = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var reglas = new ReglasE060(cfg.ReglasE060);
            var lista = new List<TipoExistente>(existentes ?? new TipoExistente[0]);
            var filas = new List<FilaPlan>();

            foreach (BarraCatalogo barra in cfg.Catalogo) filas.Add(Fila(barra, false, cfg, prefijo, reglas));
            foreach (BarraCatalogo barra in cfg.CatalogoExtra) filas.Add(Fila(barra, true, cfg, prefijo, reglas));

            foreach (FilaPlan fila in filas)
            {
                fila.Existente = lista.Find(e => e != null && Nombres.Iguales(e.Nombre, fila.Nombre));
                string motivo = reglas.MotivoFueraDeNorma(fila.Barra.DiametroMm);
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
                    if (Math.Abs(fila.Existente.DiametroMm - fila.Barra.DiametroMm) > cfg.ToleranciaDiametroMm)
                        fila.Aviso = "con diametro " + fila.Existente.DiametroMm.ToString("0.##", CultureInfo.InvariantCulture) + " mm";
                }
            }
            return filas;
        }

        private static FilaPlan Fila(BarraCatalogo barra, bool extra, Configuracion cfg, string prefijo, ReglasE060 reglas) => new FilaPlan
        {
            Barra = barra,
            EsExtra = extra,
            Nombre = Nombres.Generar(prefijo, barra.Nombre, cfg.SimboloPulgada),
            Valores = reglas.Calcular(barra.DiametroMm)
        };

        /// <summary>
        /// Comprueba una barra nueva antes de anadirla a "catalogoExtra". Devuelve el
        /// texto del error, o null si se puede anadir: nombre no vacio, diametro dentro
        /// de la norma, nombre resultante admitido por Revit y sin chocar con el
        /// catalogo, con otro extra ni con un tipo del proyecto.
        /// </summary>
        public static string ValidarNuevaBarra(Configuracion cfg, string prefijo, string nombre, double diametroMm,
                                               IEnumerable<TipoExistente> existentes, Func<string, bool> nombreValido = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            string n = (nombre ?? "").Trim();
            if (n.Length == 0) return "escribe un nombre (p. ej. 10mm, 1 1/4\", #6)";

            var reglas = new ReglasE060(cfg.ReglasE060);
            string motivo = reglas.MotivoFueraDeNorma(diametroMm);
            if (motivo != null) return "diametro " + motivo;

            string completo = Nombres.Generar(prefijo, n, cfg.SimboloPulgada);
            if (nombreValido != null && !nombreValido(completo)) return "Revit no admite el nombre \"" + completo + "\"";

            foreach (BarraCatalogo b in cfg.Catalogo)
                if (Nombres.Iguales(b.Nombre, n)) return "\"" + n + "\" ya esta en el catalogo";
            foreach (BarraCatalogo b in cfg.CatalogoExtra)
                if (Nombres.Iguales(b.Nombre, n)) return "\"" + n + "\" ya esta en la lista";
            if (existentes != null)
                foreach (TipoExistente e in existentes)
                    if (e != null && Nombres.Iguales(e.Nombre, completo)) return "el proyecto ya tiene un tipo \"" + completo + "\"";
            return null;
        }
    }
}
