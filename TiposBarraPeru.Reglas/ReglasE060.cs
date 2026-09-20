using System;
using System.Collections.Generic;

namespace TiposBarraPeru.Reglas
{
    /// <summary>Clase de gancho segun E.060 art. 7.1, deducida del estilo y angulo del RebarHookType.</summary>
    public enum TipoGancho
    {
        /// <summary>Barra principal, doblez de 180 grados (7.1.1).</summary>
        Estandar180,
        /// <summary>Barra principal, doblez de 90 grados (7.1.2).</summary>
        Estandar90,
        /// <summary>Estribo, doblez de 90 grados (7.1.3 a y b).</summary>
        Estribo90,
        /// <summary>Estribo, doblez de 135 grados (7.1.3 c). Los estribos a 180 grados usan la misma regla.</summary>
        Estribo135
    }

    /// <summary>Valores E.060 calculados para una barra (todo en mm).</summary>
    public class ValoresE060
    {
        public double DiametroMm;
        public double MultiplicadorBarra;
        public double MultiplicadorEstribo;
        /// <summary>Diametro interior de doblado de barras principales (StandardBendDiameter).</summary>
        public double DobladoBarraMm;
        /// <summary>Diametro interior de doblado de los ganchos estandar (StandardHookBendDiameter). Misma tabla 7.2.</summary>
        public double DobladoGanchoMm;
        /// <summary>Diametro interior de doblado de estribos y de sus ganchos (StirrupTieBendDiameter).</summary>
        public double DobladoEstriboMm;
        /// <summary>Extensiones rectas de gancho (mm) segun 7.1, con sus minimos.</summary>
        public double GanchoEstandar180Mm;
        public double GanchoEstandar90Mm;
        public double GanchoEstribo90Mm;
        public double GanchoEstribo135Mm;
        /// <summary>true si el diametro esta dentro del rango que cubre la norma.</summary>
        public bool DentroDeNorma;
    }

    /// <summary>
    /// Calculadora de las reglas E.060 (Concreto Armado, art. 7.1 y 7.2) a partir de
    /// la configuracion. Todas las reglas se aplican por umbral de diametro, no por
    /// nombre, de modo que 6, 8 y 12 mm caen solos en su tramo.
    /// </summary>
    public class ReglasE060
    {
        /// <summary>Tolerancia al comparar el diametro con el limite de un tramo (mm).</summary>
        public const double ToleranciaMm = 1e-6;

        /// <summary>Tolerancia al clasificar el angulo de un gancho (grados).</summary>
        public const double ToleranciaAnguloGrados = 1.0;

        private readonly ReglasE060Cfg _cfg;

        public ReglasE060(ReglasE060Cfg cfg)
        {
            _cfg = cfg ?? new ReglasE060Cfg();
        }

        /// <summary>Multiplicador del primer tramo que cubre el diametro; el ultimo si ninguno lo cubre.</summary>
        public static double Multiplicador(IList<TramoMultiplicador> tabla, double diametroMm)
        {
            if (tabla == null || tabla.Count == 0)
                throw new InvalidOperationException("tabla de multiplicadores vacia");
            foreach (TramoMultiplicador t in tabla)
                if (diametroMm <= t.HastaDiametroMm + ToleranciaMm) return t.Multiplicador;
            return tabla[tabla.Count - 1].Multiplicador;
        }

        public double MultiplicadorDobladoBarra(double diametroMm) => Multiplicador(_cfg.DobladoBarras, diametroMm);
        public double MultiplicadorDobladoEstribo(double diametroMm) => Multiplicador(_cfg.DobladoEstribos, diametroMm);

        /// <summary>Diametro interior minimo de doblado de barras principales (mm).</summary>
        public double DiametroDobladoBarraMm(double diametroMm) => MultiplicadorDobladoBarra(diametroMm) * diametroMm;

        /// <summary>Diametro interior minimo de doblado de estribos (mm).</summary>
        public double DiametroDobladoEstriboMm(double diametroMm) => MultiplicadorDobladoEstribo(diametroMm) * diametroMm;

        /// <summary>Diametro de doblado de los ganchos estandar de barras principales: tabla 7.2, igual que la barra.</summary>
        public double DiametroDobladoGanchoMm(double diametroMm) => DiametroDobladoBarraMm(diametroMm);

        /// <summary>Todos los parametros de norma de un diametro, catalogado o no.</summary>
        public ValoresE060 Calcular(double diametroMm) => new ValoresE060
        {
            DiametroMm = diametroMm,
            MultiplicadorBarra = MultiplicadorDobladoBarra(diametroMm),
            MultiplicadorEstribo = MultiplicadorDobladoEstribo(diametroMm),
            DobladoBarraMm = DiametroDobladoBarraMm(diametroMm),
            DobladoGanchoMm = DiametroDobladoGanchoMm(diametroMm),
            DobladoEstriboMm = DiametroDobladoEstriboMm(diametroMm),
            GanchoEstandar180Mm = ExtensionGanchoMm(TipoGancho.Estandar180, diametroMm),
            GanchoEstandar90Mm = ExtensionGanchoMm(TipoGancho.Estandar90, diametroMm),
            GanchoEstribo90Mm = ExtensionGanchoMm(TipoGancho.Estribo90, diametroMm),
            GanchoEstribo135Mm = ExtensionGanchoMm(TipoGancho.Estribo135, diametroMm),
            DentroDeNorma = DentroDeNorma(diametroMm)
        };

        /// <summary>Rango de diametros que cubre la norma (config: diametroMinimoNormaMm / diametroMaximoNormaMm).</summary>
        public double DiametroMinimoMm => _cfg.DiametroMinimoNormaMm;
        public double DiametroMaximoMm => _cfg.DiametroMaximoNormaMm;

        public bool DentroDeNorma(double diametroMm) =>
            diametroMm >= DiametroMinimoMm - ToleranciaMm && diametroMm <= DiametroMaximoMm + ToleranciaMm;

        /// <summary>Texto del aviso si el diametro queda fuera de la norma; null si esta dentro.</summary>
        public string MotivoFueraDeNorma(double diametroMm)
        {
            if (diametroMm <= 0) return "diametro no valido";
            if (diametroMm < DiametroMinimoMm - ToleranciaMm)
                return "menor de " + Num(DiametroMinimoMm) + " mm, fuera de la norma";
            if (diametroMm > DiametroMaximoMm + ToleranciaMm)
                return "mayor de " + Num(DiametroMaximoMm) + " mm, fuera de la norma";
            return null;
        }

        /// <summary>Extension recta del gancho tras el doblez (mm), con su minimo absoluto.</summary>
        public double ExtensionGanchoMm(TipoGancho tipo, double diametroMm)
        {
            ReglasGanchosCfg g = _cfg.Ganchos;
            switch (tipo)
            {
                case TipoGancho.Estandar180: return Extension(g.Estandar180, diametroMm);
                case TipoGancho.Estandar90: return Extension(g.Estandar90, diametroMm);
                case TipoGancho.Estribo90: return Multiplicador(g.Estribo90, diametroMm) * diametroMm;
                case TipoGancho.Estribo135: return Extension(g.Estribo135, diametroMm);
                default: throw new ArgumentOutOfRangeException(nameof(tipo));
            }
        }

        private static double Extension(ReglaGancho regla, double diametroMm) =>
            Math.Max(regla.Multiplicador * diametroMm, regla.MinimoMm);

        /// <summary>
        /// Clasifica un tipo de gancho del proyecto por su estilo (estribo o no) y su
        /// angulo en grados. Devuelve null si no encaja en ninguna regla E.060; en ese
        /// caso el add-in deja ese gancho en calculo automatico.
        /// </summary>
        public static TipoGancho? Clasificar(bool esEstribo, double anguloGrados)
        {
            bool Es(double a) => Math.Abs(anguloGrados - a) <= ToleranciaAnguloGrados;
            if (esEstribo)
            {
                if (Es(90)) return TipoGancho.Estribo90;
                if (Es(135) || Es(180)) return TipoGancho.Estribo135;
                return null;
            }
            if (Es(180)) return TipoGancho.Estandar180;
            if (Es(90)) return TipoGancho.Estandar90;
            return null;
        }

        /// <summary>Texto corto de la regla aplicada a un gancho (para la ventana y el resumen).</summary>
        public string Describir(TipoGancho tipo)
        {
            ReglasGanchosCfg g = _cfg.Ganchos;
            switch (tipo)
            {
                case TipoGancho.Estandar180: return "180 grados: " + Num(g.Estandar180.Multiplicador) + " db" + Minimo(g.Estandar180);
                case TipoGancho.Estandar90: return "90 grados: " + Num(g.Estandar90.Multiplicador) + " db" + Minimo(g.Estandar90);
                case TipoGancho.Estribo90: return "estribo 90 grados: " + Tramos(g.Estribo90);
                case TipoGancho.Estribo135: return "estribo 135 grados: " + Num(g.Estribo135.Multiplicador) + " db" + Minimo(g.Estribo135);
                default: return tipo.ToString();
            }
        }

        private static string Minimo(ReglaGancho r) => r.MinimoMm > 0 ? " (min. " + Num(r.MinimoMm) + " mm)" : "";

        private static string Tramos(IList<TramoMultiplicador> tabla)
        {
            var partes = new List<string>();
            for (int i = 0; i < tabla.Count; i++)
            {
                bool ultimo = i == tabla.Count - 1;
                partes.Add(Num(tabla[i].Multiplicador) + " db " + (ultimo ? "por encima" : "hasta " + Num(tabla[i].HastaDiametroMm) + " mm"));
            }
            return string.Join(", ", partes);
        }

        private static string Num(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
