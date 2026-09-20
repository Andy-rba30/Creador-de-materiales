using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TiposBarraPeru.Reglas
{
    /// <summary>
    /// Configuracion del boton "Materiales de concreto" (materiales.json junto a la
    /// DLL, archivo propio para no tocar config.json de barras). Mismo esquema que
    /// Configuracion: camelCase, comentarios y comas finales, valores por defecto si
    /// falta algo, catalogo + catalogoExtra.
    /// </summary>
    public class ConfiguracionMateriales
    {
        /// <summary>Prefijo del nombre del material: nombre = prefijo + f'c.</summary>
        public string PrefijoNombre { get; set; } = Nombres.PrefijoConcretoPorDefecto;

        /// <summary>Valor inicial de la casilla "actualizar los que ya existen".</summary>
        public bool ActualizarExistentes { get; set; } = false;

        /// <summary>Clase de material (identidad).</summary>
        public string ClaseMaterial { get; set; } = "Concreto";

        /// <summary>Palabras clave (identidad).</summary>
        public string PalabrasClave { get; set; } = "concreto, hormigón, Perú";

        /// <summary>true = cada material recibe una copia de la apariencia (Duplicate); false = comparten el mismo activo.</summary>
        public bool DuplicarApariencia { get; set; } = true;

        /// <summary>Materiales del proyecto de los que copiar la apariencia, por orden de preferencia.</summary>
        public List<string> MaterialApariencia { get; set; } = MaterialAparienciaPorDefecto();

        /// <summary>Nombres de la trama de corte, por orden de preferencia (ingles y espanol).</summary>
        public List<string> TramaCorte { get; set; } = TramasPorDefecto();

        /// <summary>Nombres de la trama de superficie, por orden de preferencia.</summary>
        public List<string> TramaSuperficie { get; set; } = TramasPorDefecto();

        public List<ConcretoCatalogo> Catalogo { get; set; } = new List<ConcretoCatalogo>();

        /// <summary>Resistencias anadidas desde la ventana ("Otra resistencia"). El catalogo original no se toca.</summary>
        public List<ConcretoCatalogo> CatalogoExtra { get; set; } = new List<ConcretoCatalogo>();

        public ReglasConcretoCfg Reglas { get; set; } = new ReglasConcretoCfg();

        public TablaTermicaCfg Termico { get; set; } = new TablaTermicaCfg();

        public static List<string> MaterialAparienciaPorDefecto() => new List<string>
        {
            "Concrete, Cast-in-Place gray", "Hormigón moldeado in situ, gris", "Hormigón in situ gris",
            "Concreto", "Hormigón", "Concrete"
        };

        public static List<string> TramasPorDefecto() => new List<string> { "Concreto", "Hormigón", "Concrete" };

        /// <summary>Resistencias usuales en Peru (kg/cm2), todas de peso normal (2400 kg/m3).</summary>
        public static List<ConcretoCatalogo> CatalogoPorDefecto() => new List<ConcretoCatalogo>
        {
            new ConcretoCatalogo(140), new ConcretoCatalogo(175), new ConcretoCatalogo(210), new ConcretoCatalogo(245),
            new ConcretoCatalogo(280), new ConcretoCatalogo(315), new ConcretoCatalogo(350), new ConcretoCatalogo(420)
        };

        /// <summary>Configuracion completa por defecto (la misma que materiales.json recien instalado).</summary>
        public static ConfiguracionMateriales PorDefecto()
        {
            var c = new ConfiguracionMateriales { Catalogo = CatalogoPorDefecto() };
            c.Normalizar();
            return c;
        }

        public static ConfiguracionMateriales Cargar(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return PorDefecto();
            return Deserializar(File.ReadAllText(ruta));
        }

        public static ConfiguracionMateriales Deserializar(string json)
        {
            ConfiguracionMateriales c = JsonSerializer.Deserialize<ConfiguracionMateriales>(json, Json.Lectura()) ?? new ConfiguracionMateriales();
            c.Normalizar();
            return c;
        }

        public string Serializar() => JsonSerializer.Serialize(this, Json.Escritura());

        public void Guardar(string ruta) => File.WriteAllText(ruta, Serializar());

        public ConfiguracionMateriales Clonar() => Deserializar(Serializar());

        /// <summary>
        /// Deja la configuracion coherente: catalogo por defecto si esta vacio, sin
        /// entradas con f'c no valido, densidad normal si falta, extras sin repetir
        /// el nombre de una entrada del catalogo ni de otro extra, reglas y tabla
        /// termica con valores por defecto donde falten o no tengan sentido.
        /// </summary>
        public void Normalizar()
        {
            if (PrefijoNombre == null) PrefijoNombre = "";
            if (ClaseMaterial == null) ClaseMaterial = "";
            if (PalabrasClave == null) PalabrasClave = "";
            MaterialApariencia = ListaLimpia(MaterialApariencia, MaterialAparienciaPorDefecto());
            TramaCorte = ListaLimpia(TramaCorte, TramasPorDefecto());
            TramaSuperficie = ListaLimpia(TramaSuperficie, TramasPorDefecto());

            if (Reglas == null) Reglas = new ReglasConcretoCfg();
            var def = new ReglasConcretoCfg();
            if (Reglas.Poisson <= 0 || Reglas.Poisson >= 0.5) Reglas.Poisson = def.Poisson;
            if (Reglas.DilatacionTermicaPorC <= 0) Reglas.DilatacionTermicaPorC = def.DilatacionTermicaPorC;
            if (Reglas.DensidadNormalKgM3 <= 0) Reglas.DensidadNormalKgM3 = def.DensidadNormalKgM3;
            if (Reglas.DensidadLigeroKgM3 <= 0) Reglas.DensidadLigeroKgM3 = def.DensidadLigeroKgM3;
            if (Reglas.DensidadMinimaKgM3 <= 0) Reglas.DensidadMinimaKgM3 = def.DensidadMinimaKgM3;
            if (Reglas.DensidadMaximaKgM3 <= Reglas.DensidadMinimaKgM3) Reglas.DensidadMaximaKgM3 = def.DensidadMaximaKgM3;
            if (Reglas.FcMinimoKgCm2 <= 0) Reglas.FcMinimoKgCm2 = def.FcMinimoKgCm2;
            if (Reglas.FcMaximoKgCm2 <= Reglas.FcMinimoKgCm2) Reglas.FcMaximoKgCm2 = def.FcMaximoKgCm2;
            if (Reglas.FactorCorteNormal <= 0) Reglas.FactorCorteNormal = def.FactorCorteNormal;
            if (Reglas.FactorCorteLigero <= 0) Reglas.FactorCorteLigero = def.FactorCorteLigero;
            if (Reglas.CoeficienteENormal <= 0) Reglas.CoeficienteENormal = def.CoeficienteENormal;
            if (Reglas.CoeficienteEGeneral <= 0) Reglas.CoeficienteEGeneral = def.CoeficienteEGeneral;

            if (Termico == null) Termico = new TablaTermicaCfg();
            Termico.Normal = TermicoValido(Termico.Normal, PropiedadesTermicasCfg.Normal());
            Termico.Ligero = TermicoValido(Termico.Ligero, PropiedadesTermicasCfg.Ligero());

            if (Catalogo == null) Catalogo = new List<ConcretoCatalogo>();
            Catalogo.RemoveAll(c => c == null || c.FcKgCm2 <= 0);
            if (Catalogo.Count == 0) Catalogo = CatalogoPorDefecto();
            foreach (ConcretoCatalogo c in Catalogo) if (c.DensidadKgM3 <= 0) c.DensidadKgM3 = Reglas.DensidadNormalKgM3;

            if (CatalogoExtra == null) CatalogoExtra = new List<ConcretoCatalogo>();
            CatalogoExtra.RemoveAll(c => c == null || c.FcKgCm2 <= 0);
            var vistos = new List<string>();
            foreach (ConcretoCatalogo c in Catalogo) vistos.Add(c.NombreCatalogo);
            var limpios = new List<ConcretoCatalogo>();
            foreach (ConcretoCatalogo c in CatalogoExtra)
            {
                if (c.DensidadKgM3 <= 0) c.DensidadKgM3 = Reglas.DensidadNormalKgM3;
                if (vistos.Exists(n => Nombres.Iguales(n, c.NombreCatalogo))) continue;
                vistos.Add(c.NombreCatalogo);
                limpios.Add(c);
            }
            CatalogoExtra = limpios;
        }

        private static List<string> ListaLimpia(List<string> lista, List<string> porDefecto)
        {
            if (lista == null) return porDefecto;
            lista.RemoveAll(string.IsNullOrWhiteSpace);
            for (int i = 0; i < lista.Count; i++) lista[i] = lista[i].Trim();
            return lista.Count == 0 ? porDefecto : lista;
        }

        private static PropiedadesTermicasCfg TermicoValido(PropiedadesTermicasCfg t, PropiedadesTermicasCfg def)
        {
            if (t == null) return def;
            if (t.ConductividadWmK <= 0) t.ConductividadWmK = def.ConductividadWmK;
            if (t.CalorEspecificoJgC <= 0) t.CalorEspecificoJgC = def.CalorEspecificoJgC;
            if (t.Emisividad < 0 || t.Emisividad > 1) t.Emisividad = def.Emisividad;
            if (t.PermeabilidadNgPaSm2 < 0) t.PermeabilidadNgPaSm2 = def.PermeabilidadNgPaSm2;
            if (t.Porosidad < 0 || t.Porosidad > 1) t.Porosidad = def.Porosidad;
            if (t.Reflectividad < 0 || t.Reflectividad > 1) t.Reflectividad = def.Reflectividad;
            if (t.ResistividadOhmM < 0) t.ResistividadOhmM = def.ResistividadOhmM;
            return t;
        }
    }
}
