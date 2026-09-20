using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TiposBarraPeru.Reglas
{
    /// <summary>
    /// Un tramo de una tabla "hasta tal diametro, tal multiplicador". Los tramos se
    /// evaluan en orden y gana el primero cuyo HastaDiametroMm es mayor o igual que
    /// el diametro de la barra (con una pequena tolerancia). Si ninguno lo cubre se
    /// usa el ultimo.
    /// </summary>
    public class TramoMultiplicador
    {
        public double HastaDiametroMm { get; set; }
        public double Multiplicador { get; set; }
    }

    /// <summary>Extension recta de un gancho: multiplicador de db y minimo absoluto en mm.</summary>
    public class ReglaGancho
    {
        public double Multiplicador { get; set; }
        public double MinimoMm { get; set; }
    }

    /// <summary>Reglas de ganchos estandar (E.060 art. 7.1).</summary>
    public class ReglasGanchosCfg
    {
        /// <summary>7.1.1: doblez de 180 grados mas 4 db, no menor de 65 mm.</summary>
        public ReglaGancho Estandar180 { get; set; } = new ReglaGancho { Multiplicador = 4, MinimoMm = 65 };

        /// <summary>7.1.2: doblez de 90 grados mas 12 db.</summary>
        public ReglaGancho Estandar90 { get; set; } = new ReglaGancho { Multiplicador = 12, MinimoMm = 0 };

        /// <summary>7.1.3 (a) y (b): estribos a 90 grados, 6 db hasta 5/8" y 12 db para 3/4" y 1".</summary>
        public List<TramoMultiplicador> Estribo90 { get; set; } = new List<TramoMultiplicador>
        {
            new TramoMultiplicador { HastaDiametroMm = 15.9, Multiplicador = 6 },
            new TramoMultiplicador { HastaDiametroMm = 999, Multiplicador = 12 }
        };

        /// <summary>7.1.3 (c): estribos a 135 grados, 6 db. MinimoMm = 75 lo convierte en gancho sismico (21.1).</summary>
        public ReglaGancho Estribo135 { get; set; } = new ReglaGancho { Multiplicador = 6, MinimoMm = 0 };
    }

    /// <summary>Reglas E.060 parametrizadas (articulos 7.1 y 7.2).</summary>
    public class ReglasE060Cfg
    {
        /// <summary>Diametro mas pequeno que cubre la norma (mm). Por debajo no se crea el tipo.</summary>
        public double DiametroMinimoNormaMm { get; set; } = 6;

        /// <summary>Diametro mas grande que cubre la norma (mm): 2 1/4" (barra #18). Por encima no se crea el tipo.</summary>
        public double DiametroMaximoNormaMm { get; set; } = 57;

        /// <summary>7.2.1, tabla 7.2: diametro interior minimo de doblado de barras principales y de sus ganchos.</summary>
        public List<TramoMultiplicador> DobladoBarras { get; set; } = new List<TramoMultiplicador>
        {
            new TramoMultiplicador { HastaDiametroMm = 25.4, Multiplicador = 6 },
            new TramoMultiplicador { HastaDiametroMm = 35.8, Multiplicador = 8 },
            new TramoMultiplicador { HastaDiametroMm = 999, Multiplicador = 10 }
        };

        /// <summary>7.2.2: diametro interior minimo de doblado de estribos.</summary>
        public List<TramoMultiplicador> DobladoEstribos { get; set; } = new List<TramoMultiplicador>
        {
            new TramoMultiplicador { HastaDiametroMm = 15.9, Multiplicador = 4 },
            new TramoMultiplicador { HastaDiametroMm = 999, Multiplicador = 6 }
        };

        public ReglasGanchosCfg Ganchos { get; set; } = new ReglasGanchosCfg();
    }

    /// <summary>
    /// Configuracion completa del add-in (config.json junto a la DLL). Se lee con
    /// System.Text.Json en camelCase, admitiendo comentarios y comas finales.
    /// </summary>
    public class Configuracion
    {
        /// <summary>Prefijo por defecto del nombre de los tipos. Editable en la ventana.</summary>
        public string PrefijoNombre { get; set; } = Nombres.PrefijoPorDefecto;

        /// <summary>
        /// Simbolo que sustituye a la comilla de pulgada (") del catalogo en el nombre
        /// del tipo. Por defecto se conserva la comilla. Util si Revit rechazara ese
        /// caracter en un nombre.
        /// </summary>
        public string SimboloPulgada { get; set; } = Nombres.SimboloPulgadaCatalogo;

        /// <summary>Valor inicial de la casilla "actualizar los que ya existen".</summary>
        public bool ActualizarExistentes { get; set; } = false;

        /// <summary>
        /// false = longitudes de gancho calculadas por Revit (SetAutoCalcHookLengths).
        /// true = longitudes fijadas segun E.060 para cada tipo de gancho del proyecto.
        /// </summary>
        public bool LongitudesGanchoSegunE060 { get; set; } = false;

        /// <summary>Radio maximo de doblado del tipo (MaximumBendRadius), en mm.</summary>
        public double RadioMaximoDobladoMm { get; set; } = 10000;

        /// <summary>Tolerancia para comparar diametros de tipos ya existentes (mm).</summary>
        public double ToleranciaDiametroMm { get; set; } = 0.05;

        public List<BarraCatalogo> Catalogo { get; set; } = new List<BarraCatalogo>();

        /// <summary>
        /// Diametros anadidos desde la ventana ("Otro diametro"). Se guardan aqui para
        /// que aparezcan en la tabla la proxima vez; el catalogo original no se toca.
        /// </summary>
        public List<BarraCatalogo> CatalogoExtra { get; set; } = new List<BarraCatalogo>();

        public ReglasE060Cfg ReglasE060 { get; set; } = new ReglasE060Cfg();

        /// <summary>Catalogo peruano por defecto (ASTM A615 Grado 60).</summary>
        public static List<BarraCatalogo> CatalogoPorDefecto() => new List<BarraCatalogo>
        {
            new BarraCatalogo { Nombre = "6mm",    DiametroMm = 6.0,  AreaCm2 = 0.28,  PesoKgM = 0.222 },
            new BarraCatalogo { Nombre = "8mm",    DiametroMm = 8.0,  AreaCm2 = 0.50,  PesoKgM = 0.395 },
            new BarraCatalogo { Nombre = "3/8\"",  DiametroMm = 9.5,  AreaCm2 = 0.71,  PesoKgM = 0.560 },
            new BarraCatalogo { Nombre = "12mm",   DiametroMm = 12.0, AreaCm2 = 1.13,  PesoKgM = 0.888 },
            new BarraCatalogo { Nombre = "1/2\"",  DiametroMm = 12.7, AreaCm2 = 1.29,  PesoKgM = 0.994 },
            new BarraCatalogo { Nombre = "5/8\"",  DiametroMm = 15.9, AreaCm2 = 2.00,  PesoKgM = 1.552 },
            new BarraCatalogo { Nombre = "3/4\"",  DiametroMm = 19.1, AreaCm2 = 2.84,  PesoKgM = 2.235 },
            new BarraCatalogo { Nombre = "1\"",    DiametroMm = 25.4, AreaCm2 = 5.10,  PesoKgM = 3.973 },
            new BarraCatalogo { Nombre = "1 3/8\"", DiametroMm = 35.8, AreaCm2 = 10.06, PesoKgM = 7.907 }
        };

        /// <summary>Configuracion completa por defecto (la misma que config.json recien instalado).</summary>
        public static Configuracion PorDefecto()
        {
            var c = new Configuracion { Catalogo = CatalogoPorDefecto() };
            c.Normalizar();
            return c;
        }

        /// <summary>Lee la configuracion de un archivo; si no existe devuelve la de por defecto.</summary>
        public static Configuracion Cargar(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return PorDefecto();
            return Deserializar(File.ReadAllText(ruta));
        }

        /// <summary>Lee la configuracion de un texto JSON. Lanza excepcion si el JSON no es valido.</summary>
        public static Configuracion Deserializar(string json)
        {
            Configuracion c = JsonSerializer.Deserialize<Configuracion>(json, Json.Lectura()) ?? new Configuracion();
            c.Normalizar();
            return c;
        }

        public string Serializar() => JsonSerializer.Serialize(this, Json.Escritura());

        public void Guardar(string ruta) => File.WriteAllText(ruta, Serializar());

        /// <summary>Copia independiente (para que la ventana edite sin tocar la cargada).</summary>
        public Configuracion Clonar() => Deserializar(Serializar());

        /// <summary>
        /// Deja la configuracion en un estado coherente: catalogo por defecto si esta
        /// vacio, sin entradas nulas ni sin nombre, tablas de reglas con al menos un
        /// tramo y prefijo nunca nulo.
        /// </summary>
        public void Normalizar()
        {
            if (PrefijoNombre == null) PrefijoNombre = "";
            if (SimboloPulgada == null) SimboloPulgada = Nombres.SimboloPulgadaCatalogo;
            if (RadioMaximoDobladoMm <= 0) RadioMaximoDobladoMm = 10000;
            if (ToleranciaDiametroMm < 0) ToleranciaDiametroMm = 0;

            if (Catalogo == null) Catalogo = new List<BarraCatalogo>();
            Catalogo.RemoveAll(b => b == null || string.IsNullOrWhiteSpace(b.Nombre) || b.DiametroMm <= 0);
            if (Catalogo.Count == 0) Catalogo = CatalogoPorDefecto();
            foreach (BarraCatalogo b in Catalogo) b.Nombre = b.Nombre.Trim();

            // extras: sin entradas vacias y sin repetir un nombre del catalogo ni de otro extra
            if (CatalogoExtra == null) CatalogoExtra = new List<BarraCatalogo>();
            CatalogoExtra.RemoveAll(b => b == null || string.IsNullOrWhiteSpace(b.Nombre) || b.DiametroMm <= 0);
            var vistos = new List<string>();
            foreach (BarraCatalogo b in Catalogo) vistos.Add(b.Nombre);
            var limpios = new List<BarraCatalogo>();
            foreach (BarraCatalogo b in CatalogoExtra)
            {
                b.Nombre = b.Nombre.Trim();
                if (vistos.Exists(n => Nombres.Iguales(n, b.Nombre))) continue;
                vistos.Add(b.Nombre);
                limpios.Add(b);
            }
            CatalogoExtra = limpios;

            if (ReglasE060 == null) ReglasE060 = new ReglasE060Cfg();
            var def = new ReglasE060Cfg();
            if (ReglasE060.DiametroMinimoNormaMm <= 0) ReglasE060.DiametroMinimoNormaMm = def.DiametroMinimoNormaMm;
            if (ReglasE060.DiametroMaximoNormaMm <= ReglasE060.DiametroMinimoNormaMm) ReglasE060.DiametroMaximoNormaMm = def.DiametroMaximoNormaMm;
            ReglasE060.DobladoBarras = TablaValida(ReglasE060.DobladoBarras, def.DobladoBarras);
            ReglasE060.DobladoEstribos = TablaValida(ReglasE060.DobladoEstribos, def.DobladoEstribos);
            if (ReglasE060.Ganchos == null) ReglasE060.Ganchos = new ReglasGanchosCfg();
            ReglasGanchosCfg g = ReglasE060.Ganchos;
            if (g.Estandar180 == null) g.Estandar180 = def.Ganchos.Estandar180;
            if (g.Estandar90 == null) g.Estandar90 = def.Ganchos.Estandar90;
            if (g.Estribo135 == null) g.Estribo135 = def.Ganchos.Estribo135;
            g.Estribo90 = TablaValida(g.Estribo90, def.Ganchos.Estribo90);
        }

        private static List<TramoMultiplicador> TablaValida(List<TramoMultiplicador> tabla, List<TramoMultiplicador> porDefecto)
        {
            if (tabla == null) return porDefecto;
            tabla.RemoveAll(t => t == null || t.Multiplicador <= 0);
            if (tabla.Count == 0) return porDefecto;
            tabla.Sort((a, b) => a.HastaDiametroMm.CompareTo(b.HastaDiametroMm));
            return tabla;
        }
    }
}
