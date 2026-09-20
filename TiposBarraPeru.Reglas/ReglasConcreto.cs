using System;
using System.Globalization;

namespace TiposBarraPeru.Reglas
{
    /// <summary>Conversion entre las unidades del catalogo (kg/cm2) y las de la norma en SI (MPa).</summary>
    public static class Unidades
    {
        /// <summary>1 kg/cm2 = 0.0980665 MPa (gravedad normal 9.80665 m/s2).</summary>
        public const double MPaPorKgCm2 = 0.0980665;

        public static double KgCm2AMPa(double kgCm2) => kgCm2 * MPaPorKgCm2;
        public static double MPaAKgCm2(double mpa) => mpa / MPaPorKgCm2;
    }

    /// <summary>Reglas del concreto (E.060 art. 8.5 y valores por defecto editables).</summary>
    public class ReglasConcretoCfg
    {
        public const double DensidadNormalPorDefecto = 2400;

        /// <summary>Coeficiente de Poisson (E.060 y ACI: 0.15 a 0.20).</summary>
        public double Poisson { get; set; } = 0.15;

        /// <summary>Coeficiente de dilatacion termica (1/grado C).</summary>
        public double DilatacionTermicaPorC { get; set; } = 1.0e-5;

        /// <summary>Densidad de peso normal (kg/m3). Con esta densidad E = coeficienteENormal * raiz(f'c) en kg/cm2.</summary>
        public double DensidadNormalKgM3 { get; set; } = DensidadNormalPorDefecto;

        /// <summary>Por debajo de esta densidad (kg/m3) el concreto se trata como ligero.</summary>
        public double DensidadLigeroKgM3 { get; set; } = 1900;

        /// <summary>Rango de densidad (kg/m3) en que vale la formula general de E (E.060 8.5.1).</summary>
        public double DensidadMinimaKgM3 { get; set; } = 1450;
        public double DensidadMaximaKgM3 { get; set; } = 2500;

        /// <summary>Rango admitido de f'c (kg/cm2). Fuera de el la fila avisa y no se crea.</summary>
        public double FcMinimoKgCm2 { get; set; } = 100;
        public double FcMaximoKgCm2 { get; set; } = 1000;

        /// <summary>Factor de reduccion de resistencia a corte: 1.0 peso normal, 0.75 ligero.</summary>
        public double FactorCorteNormal { get; set; } = 1.0;
        public double FactorCorteLigero { get; set; } = 0.75;

        /// <summary>E.060 8.5.1, peso normal: E = 15000 * raiz(f'c) con f'c y E en kg/cm2 (equivale a 4700 raiz(f'c) en MPa).</summary>
        public double CoeficienteENormal { get; set; } = 15000;

        /// <summary>E.060 8.5.1, general: E = wc^1.5 * 0.043 * raiz(f'c) con f'c y E en MPa y wc en kg/m3.</summary>
        public double CoeficienteEGeneral { get; set; } = 0.043;
    }

    /// <summary>
    /// Propiedades del activo termico (tipo solido). No dependen de f'c sino de la
    /// densidad: hay un juego "normal" y otro "ligero". Valores por defecto de la
    /// biblioteca de Revit para el hormigon en masa.
    /// </summary>
    public class PropiedadesTermicasCfg
    {
        public double ConductividadWmK { get; set; } = 1.046;
        public double CalorEspecificoJgC { get; set; } = 0.657;
        public double Emisividad { get; set; } = 0.95;
        public double PermeabilidadNgPaSm2 { get; set; } = 182.4;
        public double Porosidad { get; set; } = 0.01;
        public double Reflectividad { get; set; } = 0;
        public double ResistividadOhmM { get; set; } = 2.0e9;
        public bool TransmiteLuz { get; set; } = false;

        public static PropiedadesTermicasCfg Normal() => new PropiedadesTermicasCfg();
        public static PropiedadesTermicasCfg Ligero() => new PropiedadesTermicasCfg { ConductividadWmK = 0.5 };

        public PropiedadesTermicasCfg Clonar() => (PropiedadesTermicasCfg)MemberwiseClone();
    }

    /// <summary>Tabla termica del config: juego para peso normal y juego para ligero.</summary>
    public class TablaTermicaCfg
    {
        public PropiedadesTermicasCfg Normal { get; set; } = PropiedadesTermicasCfg.Normal();
        public PropiedadesTermicasCfg Ligero { get; set; } = PropiedadesTermicasCfg.Ligero();
    }

    /// <summary>Todo lo calculado para una resistencia: lo que se escribe en el material.</summary>
    public class ValoresConcreto
    {
        public double FcKgCm2;
        public double FcMPa;
        public double DensidadKgM3;
        /// <summary>true = densidad normal, formula simplificada; false = formula general con wc.</summary>
        public bool DensidadNormal;
        /// <summary>Texto de la formula de E aplicada (para la ventana y el resumen).</summary>
        public string FormulaE;
        public double EKgCm2;
        public double EMPa;
        public double Poisson;
        public double GKgCm2;
        public double GMPa;
        public double DilatacionPorC;
        public bool Ligero;
        public double FactorCorte;
        public PropiedadesTermicasCfg Termico;
        /// <summary>Componente RGB del gris de sombreado (r = g = b).</summary>
        public byte Gris;
        public string Descripcion;
        public bool DentroDeNorma;
    }

    /// <summary>
    /// Calculadora de las propiedades del concreto a partir de f'c y la densidad,
    /// segun E.060 art. 8.5 y la configuracion. Sin Revit: las unidades son las del
    /// catalogo (kg/cm2, kg/m3) y el SI (MPa); la conversion a unidades internas de
    /// Revit la hace el add-in.
    /// </summary>
    public class ReglasConcreto
    {
        public const double Tolerancia = 1e-6;

        /// <summary>Gris de sombreado: 210 para f'c = 100, 0.08 menos por cada kg/cm2, acotado entre 110 y 220.</summary>
        public const double GrisBase = 210, GrisPendiente = 0.08, GrisMinimo = 110, GrisMaximo = 220;

        private readonly ReglasConcretoCfg _cfg;
        private readonly TablaTermicaCfg _termico;

        public ReglasConcreto(ReglasConcretoCfg cfg, TablaTermicaCfg termico)
        {
            _cfg = cfg ?? new ReglasConcretoCfg();
            _termico = termico ?? new TablaTermicaCfg();
        }

        public ReglasConcretoCfg Cfg => _cfg;

        // --- modulo de elasticidad (E.060 8.5.1) ---

        /// <summary>Peso normal: E = 15000 * raiz(f'c), en kg/cm2.</summary>
        public double ModuloNormalKgCm2(double fcKgCm2) => fcKgCm2 <= 0 ? 0 : _cfg.CoeficienteENormal * Math.Sqrt(fcKgCm2);

        /// <summary>General: E = wc^1.5 * 0.043 * raiz(f'c MPa), en MPa. f'c se recibe en kg/cm2.</summary>
        public double ModuloGeneralMPa(double fcKgCm2, double densidadKgM3)
        {
            if (fcKgCm2 <= 0 || densidadKgM3 <= 0) return 0;
            return Math.Pow(densidadKgM3, 1.5) * _cfg.CoeficienteEGeneral * Math.Sqrt(Unidades.KgCm2AMPa(fcKgCm2));
        }

        public bool EsDensidadNormal(double densidadKgM3) => Math.Abs(densidadKgM3 - _cfg.DensidadNormalKgM3) <= Tolerancia;

        /// <summary>E en MPa: formula simplificada si la densidad es la normal, general en otro caso.</summary>
        public double ModuloElasticidadMPa(double fcKgCm2, double densidadKgM3) =>
            EsDensidadNormal(densidadKgM3) ? Unidades.KgCm2AMPa(ModuloNormalKgCm2(fcKgCm2)) : ModuloGeneralMPa(fcKgCm2, densidadKgM3);

        public double ModuloElasticidadKgCm2(double fcKgCm2, double densidadKgM3) =>
            Unidades.MPaAKgCm2(ModuloElasticidadMPa(fcKgCm2, densidadKgM3));

        /// <summary>Modulo de corte G = E / (2 (1 + nu)), en las unidades de E.</summary>
        public static double ModuloCorte(double e, double poisson) => e / (2.0 * (1.0 + poisson));

        // --- peso ligero ---

        public bool EsLigero(double densidadKgM3) => densidadKgM3 < _cfg.DensidadLigeroKgM3 - Tolerancia;
        public double FactorCorte(double densidadKgM3) => EsLigero(densidadKgM3) ? _cfg.FactorCorteLigero : _cfg.FactorCorteNormal;
        public PropiedadesTermicasCfg Termico(double densidadKgM3) => EsLigero(densidadKgM3) ? _termico.Ligero : _termico.Normal;

        // --- rango ---

        public bool DentroDeNorma(double fcKgCm2, double densidadKgM3) => MotivoFueraDeNorma(fcKgCm2, densidadKgM3) == null;

        /// <summary>Texto del aviso si f'c o la densidad quedan fuera de rango; null si todo esta dentro.</summary>
        public string MotivoFueraDeNorma(double fcKgCm2, double densidadKgM3)
        {
            if (fcKgCm2 <= 0) return "f'c no valido";
            if (fcKgCm2 < _cfg.FcMinimoKgCm2 - Tolerancia) return "f'c menor de " + Num(_cfg.FcMinimoKgCm2) + " kg/cm2, fuera de rango";
            if (fcKgCm2 > _cfg.FcMaximoKgCm2 + Tolerancia) return "f'c mayor de " + Num(_cfg.FcMaximoKgCm2) + " kg/cm2, fuera de rango";
            if (densidadKgM3 <= 0) return "densidad no valida";
            if (densidadKgM3 < _cfg.DensidadMinimaKgM3 - Tolerancia) return "densidad menor de " + Num(_cfg.DensidadMinimaKgM3) + " kg/m3, fuera de la formula E.060";
            if (densidadKgM3 > _cfg.DensidadMaximaKgM3 + Tolerancia) return "densidad mayor de " + Num(_cfg.DensidadMaximaKgM3) + " kg/m3, fuera de la formula E.060";
            return null;
        }

        // --- identidad y graficos ---

        /// <summary>Gris de sombreado, un poco mas oscuro cuanto mayor f'c.</summary>
        public static byte Gris(double fcKgCm2)
        {
            double g = GrisBase - GrisPendiente * (fcKgCm2 - 100);
            g = Math.Max(GrisMinimo, Math.Min(GrisMaximo, g));
            return (byte)Math.Round(g, MidpointRounding.AwayFromZero);
        }

        /// <summary>"f'c = 210 kg/cm² (20,6 MPa), E.060", con coma decimal como en la norma.</summary>
        public static string Descripcion(double fcKgCm2) =>
            "f'c = " + Coma(fcKgCm2, "0.##") + " kg/cm² (" + Coma(Unidades.KgCm2AMPa(fcKgCm2), "0.0") + " MPa), E.060";

        private static string Coma(double v, string formato) => v.ToString(formato, CultureInfo.InvariantCulture).Replace('.', ',');
        private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Todos los valores de una resistencia, este o no dentro de rango.</summary>
        public ValoresConcreto Calcular(double fcKgCm2, double densidadKgM3)
        {
            bool normal = EsDensidadNormal(densidadKgM3);
            double eMPa = ModuloElasticidadMPa(fcKgCm2, densidadKgM3);
            double eKg = Unidades.MPaAKgCm2(eMPa);
            return new ValoresConcreto
            {
                FcKgCm2 = fcKgCm2,
                FcMPa = Unidades.KgCm2AMPa(fcKgCm2),
                DensidadKgM3 = densidadKgM3,
                DensidadNormal = normal,
                FormulaE = normal
                    ? "E = " + Num(_cfg.CoeficienteENormal) + " * raiz(f'c) kg/cm2 (peso normal)"
                    : "E = wc^1.5 * " + Num(_cfg.CoeficienteEGeneral) + " * raiz(f'c) MPa, wc = " + Num(densidadKgM3) + " kg/m3",
                EKgCm2 = eKg,
                EMPa = eMPa,
                Poisson = _cfg.Poisson,
                GKgCm2 = ModuloCorte(eKg, _cfg.Poisson),
                GMPa = ModuloCorte(eMPa, _cfg.Poisson),
                DilatacionPorC = _cfg.DilatacionTermicaPorC,
                Ligero = EsLigero(densidadKgM3),
                FactorCorte = FactorCorte(densidadKgM3),
                Termico = Termico(densidadKgM3),
                Gris = Gris(fcKgCm2),
                Descripcion = Descripcion(fcKgCm2),
                DentroDeNorma = DentroDeNorma(fcKgCm2, densidadKgM3)
            };
        }
    }
}
