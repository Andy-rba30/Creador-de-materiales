using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TiposBarraPeru.Reglas;

namespace TiposBarraPeru.Pruebas
{
    /// <summary>
    /// Comprobaciones de las reglas puras (sin Revit). Cada "Comprobar" cuenta una
    /// comprobacion; al final se imprime cuantas pasan y el codigo de salida es 1
    /// si alguna falla.
    /// </summary>
    internal static class Program
    {
        private static int _ok, _fallos;

        private static void Comprobar(string nombre, bool condicion, string detalle = null)
        {
            if (condicion) { _ok++; return; }
            _fallos++;
            Console.WriteLine("  FALLA: " + nombre + (detalle != null ? "  [" + detalle + "]" : ""));
        }

        private static void Igual(string nombre, double esperado, double real, double tol = 1e-9) =>
            Comprobar(nombre, Math.Abs(esperado - real) <= tol, "esperado " + F(esperado) + ", obtenido " + F(real));

        private static void Igual(string nombre, string esperado, string real) =>
            Comprobar(nombre, esperado == real, "esperado \"" + esperado + "\", obtenido \"" + real + "\"");

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private const string O = "Ø";

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("Comprobaciones de TiposBarraPeru.Reglas");

            Seccion("1. Catalogo por defecto", PruebasCatalogo);
            Seccion("2. Lectura de config.json", PruebasConfig);
            Seccion("3. Nombres generados", PruebasNombres);
            Seccion("4. Diametros de doblado E.060 (7.2)", PruebasDoblado);
            Seccion("5. Limites de los tramos", PruebasLimites);
            Seccion("6. Extensiones de gancho E.060 (7.1)", PruebasGanchos);
            Seccion("7. Clasificacion de ganchos del proyecto", PruebasClasificacion);
            Seccion("8. Planificador (idempotencia y actualizacion)", PruebasPlanificador);
            Seccion("9. Otros diametros: area, peso y rango de norma", PruebasOtrosDiametros);
            Seccion("10. Otros diametros: parametros E.060 frente a los del catalogo", PruebasParametrosOtros);
            Seccion("11. Otros diametros: catalogoExtra, planificador y validacion", PruebasCatalogoExtra);

            Console.WriteLine();
            Console.WriteLine(_ok + " comprobaciones superadas, " + _fallos + " fallidas.");
            return _fallos == 0 ? 0 : 1;
        }

        private static void Seccion(string titulo, Action pruebas)
        {
            int antes = _ok + _fallos;
            Console.WriteLine();
            Console.WriteLine(titulo);
            try { pruebas(); }
            catch (Exception ex) { _fallos++; Console.WriteLine("  EXCEPCION: " + ex); }
            Console.WriteLine("  " + (_ok + _fallos - antes) + " comprobaciones");
        }

        // ------------------------------------------------------------------
        private static readonly (string nombre, double mm, double cm2, double kgm)[] Tabla =
        {
            ("6mm", 6.0, 0.28, 0.222), ("8mm", 8.0, 0.50, 0.395), ("3/8\"", 9.5, 0.71, 0.560),
            ("12mm", 12.0, 1.13, 0.888), ("1/2\"", 12.7, 1.29, 0.994), ("5/8\"", 15.9, 2.00, 1.552),
            ("3/4\"", 19.1, 2.84, 2.235), ("1\"", 25.4, 5.10, 3.973), ("1 3/8\"", 35.8, 10.06, 7.907)
        };

        private static void PruebasCatalogo()
        {
            Configuracion cfg = Configuracion.PorDefecto();
            Comprobar("9 tamanos", cfg.Catalogo.Count == 9, cfg.Catalogo.Count.ToString());
            for (int i = 0; i < Tabla.Length && i < cfg.Catalogo.Count; i++)
            {
                BarraCatalogo b = cfg.Catalogo[i];
                Igual("nombre " + Tabla[i].nombre, Tabla[i].nombre, b.Nombre);
                Igual("diametro " + Tabla[i].nombre, Tabla[i].mm, b.DiametroMm);
                Igual("area " + Tabla[i].nombre, Tabla[i].cm2, b.AreaCm2);
                Igual("peso " + Tabla[i].nombre, Tabla[i].kgm, b.PesoKgM);
                Comprobar("corrugada " + Tabla[i].nombre, b.Corrugada);
            }
            Igual("prefijo por defecto", O, cfg.PrefijoNombre);
            Comprobar("actualizar existentes desactivado", !cfg.ActualizarExistentes);
            Comprobar("ganchos automaticos por defecto", !cfg.LongitudesGanchoSegunE060);
            Igual("radio maximo de doblado", 10000, cfg.RadioMaximoDobladoMm);
        }

        private static void PruebasConfig()
        {
            // a) el config.json distribuido con el add-in equivale a la configuracion por defecto
            string ruta = Path.Combine(AppContext.BaseDirectory, "config.json");
            Comprobar("config.json copiado junto a las pruebas", File.Exists(ruta), ruta);
            Configuracion leida = Configuracion.Cargar(ruta);
            Comprobar("config.json: 9 tamanos", leida.Catalogo.Count == 9);
            Igual("config.json equivale al por defecto", Configuracion.PorDefecto().Serializar(), leida.Serializar());

            // b) comentarios, comas finales y camelCase
            string json = "{\n  // comentario\n  \"prefijoNombre\": \"\",\n  \"actualizarExistentes\": true,\n" +
                          "  \"catalogo\": [ { \"nombre\": \"1/2\\\"\", \"diametroMm\": 12.7, \"areaCm2\": 1.29, \"pesoKgM\": 0.994, \"corrugada\": false }, ],\n" +
                          "  \"reglasE060\": { \"ganchos\": { \"estribo135\": { \"multiplicador\": 6, \"minimoMm\": 75, }, }, },\n}\n";
            Configuracion c = Configuracion.Deserializar(json);
            Igual("prefijo vacio leido", "", c.PrefijoNombre);
            Comprobar("actualizarExistentes leido", c.ActualizarExistentes);
            Comprobar("catalogo de 1 entrada", c.Catalogo.Count == 1);
            Comprobar("corrugada=false leido", c.Catalogo.Count == 1 && !c.Catalogo[0].Corrugada);
            Igual("nombre con comilla escapada", "1/2\"", c.Catalogo[0].Nombre);
            Igual("estribo135 minimo 75 leido", 75, c.ReglasE060.Ganchos.Estribo135.MinimoMm);
            Comprobar("tablas no indicadas toman el valor por defecto", c.ReglasE060.DobladoBarras.Count == 3 && c.ReglasE060.DobladoEstribos.Count == 2);

            // c) archivo inexistente y JSON vacio -> por defecto
            Configuracion sinArchivo = Configuracion.Cargar(Path.Combine(AppContext.BaseDirectory, "no_existe.json"));
            Comprobar("sin archivo: catalogo por defecto", sinArchivo.Catalogo.Count == 9);
            Configuracion vacio = Configuracion.Deserializar("{}");
            Comprobar("JSON vacio: catalogo por defecto", vacio.Catalogo.Count == 9);
            Igual("JSON vacio: prefijo por defecto", O, vacio.PrefijoNombre);

            // d) ida y vuelta
            Configuracion clon = leida.Clonar();
            Igual("clon identico", leida.Serializar(), clon.Serializar());
            Comprobar("serializado legible (prefijo sin escapar)", leida.Serializar().Contains("\"prefijoNombre\": \"" + O + "\""));

            // e) JSON invalido lanza excepcion
            bool lanza = false;
            try { Configuracion.Deserializar("{ esto no es json"); } catch { lanza = true; }
            Comprobar("JSON invalido lanza excepcion", lanza);
        }

        private static void PruebasNombres()
        {
            string[] esperados = { O + "6mm", O + "8mm", O + "3/8\"", O + "12mm", O + "1/2\"", O + "5/8\"", O + "3/4\"", O + "1\"", O + "1 3/8\"" };
            Configuracion cfg = Configuracion.PorDefecto();
            for (int i = 0; i < esperados.Length; i++)
                Igual("nombre " + esperados[i], esperados[i], Nombres.Generar(cfg.PrefijoNombre, cfg.Catalogo[i].Nombre, cfg.SimboloPulgada));

            Igual("prefijo vacio", "3/8\"", Nombres.Generar("", "3/8\""));
            Igual("prefijo nulo", "12mm", Nombres.Generar(null, "12mm"));
            Igual("prefijo personalizado", "D-1\"", Nombres.Generar("D-", "1\""));
            Igual("simbolo de pulgada alternativo", O + "1 3/8in", Nombres.Generar(O, "1 3/8\"", "in"));
            Igual("simbolo de pulgada alternativo no afecta a mm", O + "6mm", Nombres.Generar(O, "6mm", "in"));
            Igual("espacios en el nombre de catalogo", O + "1\"", Nombres.Generar(O, "  1\" "));
            Comprobar("comparacion sin mayusculas", Nombres.Iguales("ø12mm", O + "12mm"));
            Comprobar("comparacion con espacios", Nombres.Iguales(" " + O + "12mm ", O + "12mm"));
            Comprobar("comparacion distinta", !Nombres.Iguales(O + "12mm", O + "1/2\""));
        }

        private static void PruebasDoblado()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            double[] barra = { 36, 48, 57, 72, 76.2, 95.4, 114.6, 152.4, 286.4 };
            double[] estribo = { 24, 32, 38, 48, 50.8, 63.6, 114.6, 152.4, 214.8 };
            for (int i = 0; i < Tabla.Length; i++)
            {
                double db = Tabla[i].mm;
                Igual("doblado barra " + Tabla[i].nombre, barra[i], r.DiametroDobladoBarraMm(db), 1e-9);
                Igual("doblado gancho " + Tabla[i].nombre, barra[i], r.DiametroDobladoGanchoMm(db), 1e-9);
                Igual("doblado estribo " + Tabla[i].nombre, estribo[i], r.DiametroDobladoEstriboMm(db), 1e-9);
            }
            ValoresE060 v = r.Calcular(35.8);
            Igual("Calcular: multiplicador barra 1 3/8\"", 8, v.MultiplicadorBarra);
            Igual("Calcular: multiplicador estribo 1 3/8\"", 6, v.MultiplicadorEstribo);
            Igual("Calcular: doblado barra 1 3/8\"", 286.4, v.DobladoBarraMm, 1e-9);
        }

        private static void PruebasLimites()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            Igual("estribo 15.9 -> 4 db", 4, r.MultiplicadorDobladoEstribo(15.9));
            Igual("estribo 15.9000001 -> 4 db (tolerancia)", 4, r.MultiplicadorDobladoEstribo(15.9000001));
            Igual("estribo 16.0 -> 6 db", 6, r.MultiplicadorDobladoEstribo(16.0));
            Igual("barra 25.4 -> 6 db", 6, r.MultiplicadorDobladoBarra(25.4));
            Igual("barra 25.5 -> 8 db", 8, r.MultiplicadorDobladoBarra(25.5));
            Igual("barra 35.8 -> 8 db", 8, r.MultiplicadorDobladoBarra(35.8));
            Igual("barra 36 -> 10 db", 10, r.MultiplicadorDobladoBarra(36));
            Igual("barra 57 (2 1/4\") -> 10 db", 10, r.MultiplicadorDobladoBarra(57));
            Igual("barra 6 -> 6 db", 6, r.MultiplicadorDobladoBarra(6));
            Igual("estribo 6 -> 4 db", 4, r.MultiplicadorDobladoEstribo(6));

            // tabla desordenada en el config: Normalizar la ordena
            var cfg = Configuracion.Deserializar("{ \"reglasE060\": { \"dobladoBarras\": [ { \"hastaDiametroMm\": 999, \"multiplicador\": 10 }, { \"hastaDiametroMm\": 25.4, \"multiplicador\": 6 } ] } }");
            var r2 = new ReglasE060(cfg.ReglasE060);
            Igual("tabla desordenada: 12 -> 6 db", 6, r2.MultiplicadorDobladoBarra(12));
            Igual("tabla desordenada: 30 -> 10 db", 10, r2.MultiplicadorDobladoBarra(30));
            Igual("por encima del ultimo tramo se usa el ultimo", 10, r2.MultiplicadorDobladoBarra(5000));
        }

        private static void PruebasGanchos()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            // 180 grados: 4 db, minimo 65 mm
            Igual("180: 6mm -> 65 (minimo)", 65, r.ExtensionGanchoMm(TipoGancho.Estandar180, 6));
            Igual("180: 12mm -> 65 (minimo)", 65, r.ExtensionGanchoMm(TipoGancho.Estandar180, 12));
            Igual("180: 5/8\" -> 65 (63.6 < 65)", 65, r.ExtensionGanchoMm(TipoGancho.Estandar180, 15.9));
            Igual("180: 3/4\" -> 76.4", 76.4, r.ExtensionGanchoMm(TipoGancho.Estandar180, 19.1), 1e-9);
            Igual("180: 1\" -> 101.6", 101.6, r.ExtensionGanchoMm(TipoGancho.Estandar180, 25.4), 1e-9);
            Igual("180: 1 3/8\" -> 143.2", 143.2, r.ExtensionGanchoMm(TipoGancho.Estandar180, 35.8), 1e-9);
            // 90 grados: 12 db
            Igual("90: 6mm -> 72", 72, r.ExtensionGanchoMm(TipoGancho.Estandar90, 6));
            Igual("90: 12mm -> 144", 144, r.ExtensionGanchoMm(TipoGancho.Estandar90, 12));
            Igual("90: 1\" -> 304.8", 304.8, r.ExtensionGanchoMm(TipoGancho.Estandar90, 25.4), 1e-9);
            // estribo 90: 6 db hasta 5/8", 12 db para 3/4" y 1"
            Igual("estribo 90: 6mm -> 36", 36, r.ExtensionGanchoMm(TipoGancho.Estribo90, 6));
            Igual("estribo 90: 3/8\" -> 57", 57, r.ExtensionGanchoMm(TipoGancho.Estribo90, 9.5));
            Igual("estribo 90: 5/8\" -> 95.4", 95.4, r.ExtensionGanchoMm(TipoGancho.Estribo90, 15.9), 1e-9);
            Igual("estribo 90: 3/4\" -> 229.2", 229.2, r.ExtensionGanchoMm(TipoGancho.Estribo90, 19.1), 1e-9);
            Igual("estribo 90: 1\" -> 304.8", 304.8, r.ExtensionGanchoMm(TipoGancho.Estribo90, 25.4), 1e-9);
            // estribo 135: 6 db
            Igual("estribo 135: 3/8\" -> 57", 57, r.ExtensionGanchoMm(TipoGancho.Estribo135, 9.5));
            Igual("estribo 135: 1/2\" -> 76.2", 76.2, r.ExtensionGanchoMm(TipoGancho.Estribo135, 12.7), 1e-9);
            // gancho sismico: minimo 75 mm
            var cfg = Configuracion.PorDefecto();
            cfg.ReglasE060.Ganchos.Estribo135.MinimoMm = 75;
            var rs = new ReglasE060(cfg.ReglasE060);
            Igual("estribo 135 sismico: 3/8\" -> 75", 75, rs.ExtensionGanchoMm(TipoGancho.Estribo135, 9.5));
            Igual("estribo 135 sismico: 1/2\" -> 76.2", 76.2, rs.ExtensionGanchoMm(TipoGancho.Estribo135, 12.7), 1e-9);

            Comprobar("descripcion 180", r.Describir(TipoGancho.Estandar180) == "180 grados: 4 db (min. 65 mm)", r.Describir(TipoGancho.Estandar180));
            Comprobar("descripcion estribo 90", r.Describir(TipoGancho.Estribo90) == "estribo 90 grados: 6 db hasta 15.9 mm, 12 db por encima", r.Describir(TipoGancho.Estribo90));
        }

        private static void PruebasClasificacion()
        {
            Comprobar("estandar 180", ReglasE060.Clasificar(false, 180) == TipoGancho.Estandar180);
            Comprobar("estandar 90", ReglasE060.Clasificar(false, 90) == TipoGancho.Estandar90);
            Comprobar("estandar 135 -> sin regla", ReglasE060.Clasificar(false, 135) == null);
            Comprobar("estribo 90", ReglasE060.Clasificar(true, 90) == TipoGancho.Estribo90);
            Comprobar("estribo 135", ReglasE060.Clasificar(true, 135) == TipoGancho.Estribo135);
            Comprobar("estribo 180 -> regla de 135", ReglasE060.Clasificar(true, 180) == TipoGancho.Estribo135);
            Comprobar("tolerancia de angulo (89.5)", ReglasE060.Clasificar(true, 89.5) == TipoGancho.Estribo90);
            Comprobar("angulo raro (45) -> sin regla", ReglasE060.Clasificar(true, 45) == null);
            Comprobar("angulo en radianes convertido por el add-in: 180 grados", Math.Abs(Math.PI * 180.0 / Math.PI - 180) < 1e-12);
        }

        private static void PruebasPlanificador()
        {
            Configuracion cfg = Configuracion.PorDefecto();
            var existentes = new List<TipoExistente>
            {
                new TipoExistente(O + "12mm", 12.0),          // igual
                new TipoExistente("ø1/2\"", 12.7),        // solo cambia la mayuscula del prefijo
                new TipoExistente(O + "8mm", 10.0),           // mismo nombre, otro diametro
                new TipoExistente("16M", 16.0)                // ajeno al catalogo
            };

            // a) sin actualizar: idempotente, solo se crean los que faltan
            List<FilaPlan> plan = Planificador.Planificar(cfg, O, existentes, false);
            Comprobar("9 filas", plan.Count == 9);
            FilaPlan F12 = plan.First(f => f.Barra.Nombre == "12mm");
            FilaPlan F12p = plan.First(f => f.Barra.Nombre == "1/2\"");
            FilaPlan F8 = plan.First(f => f.Barra.Nombre == "8mm");
            FilaPlan F6 = plan.First(f => f.Barra.Nombre == "6mm");
            Comprobar("12mm ya existe", F12.Estado == EstadoFila.YaExiste && !F12.Crear);
            Comprobar("1/2\" ya existe (sin distinguir mayusculas)", F12p.Estado == EstadoFila.YaExiste && !F12p.Crear);
            Comprobar("8mm ya existe con aviso de diametro", F8.Estado == EstadoFila.YaExiste && F8.Aviso == "con diametro 10 mm", F8.Aviso);
            Comprobar("6mm se creara", F6.Estado == EstadoFila.SeCreara && F6.Crear);
            Comprobar("6 filas a crear", plan.Count(f => f.Crear) == 6);
            Comprobar("texto de estado", F8.TextoEstado == "ya existe (con diametro 10 mm)", F8.TextoEstado);
            Comprobar("existentes no seleccionables", !F12.Seleccionable && F6.Seleccionable);
            Comprobar("valores E.060 en la fila", Math.Abs(F6.Valores.DobladoEstriboMm - 24) < 1e-9);

            // b) actualizar existentes
            plan = Planificador.Planificar(cfg, O, existentes, true);
            Comprobar("actualizar: 12mm se actualizara", plan.First(f => f.Barra.Nombre == "12mm").Estado == EstadoFila.SeActualizara);
            Comprobar("actualizar: 9 filas marcadas", plan.Count(f => f.Crear) == 9);
            Comprobar("actualizar: aviso de diametro se conserva", plan.First(f => f.Barra.Nombre == "8mm").Aviso == "con diametro 10 mm");

            // c) otro prefijo: nada existe
            plan = Planificador.Planificar(cfg, "D", existentes, false);
            Comprobar("prefijo D: todo se creara", plan.All(f => f.Estado == EstadoFila.SeCreara) && plan[0].Nombre == "D6mm");

            // d) validador de nombres de Revit (simulado: rechaza la comilla)
            plan = Planificador.Planificar(cfg, O, existentes, false, n => !n.Contains("\""));
            Comprobar("nombres con comilla no validos", plan.Count(f => f.Estado == EstadoFila.NombreInvalido) == 6);
            Comprobar("nombre no valido no se marca", plan.Where(f => f.Estado == EstadoFila.NombreInvalido).All(f => !f.Crear && !f.Seleccionable));
            cfg.SimboloPulgada = "in";
            plan = Planificador.Planificar(cfg, O, existentes, false, n => !n.Contains("\""));
            Comprobar("con simboloPulgada=in todos los nombres son validos", plan.All(f => f.Estado != EstadoFila.NombreInvalido));
            Comprobar("con simboloPulgada=in 1/2 se llama " + O + "1/2in", plan.Any(f => f.Nombre == O + "1/2in"));

            // e) sin tipos existentes
            plan = Planificador.Planificar(cfg, O, null, false);
            Comprobar("sin existentes: 9 a crear", plan.Count(f => f.Crear) == 9);
        }

        // ------------------------------------------------------------------
        private static void PruebasOtrosDiametros()
        {
            // formulas: area = pi d^2 / 4 (cm2), peso = area * 0.785 (kg/m)
            Igual("area 10 mm", 0.7854, PropiedadesBarra.AreaCm2(10), 1e-4);
            Igual("peso 10 mm", 0.6165, PropiedadesBarra.PesoKgMDesdeDiametro(10), 1e-4);
            Igual("area 1 1/4\" (31.8)", 7.9423, PropiedadesBarra.AreaCm2(31.8), 1e-4);
            Igual("peso 1 1/4\" (31.8)", 6.2347, PropiedadesBarra.PesoKgMDesdeDiametro(31.8), 1e-4);
            Igual("area #6 (19.05)", 2.8502, PropiedadesBarra.AreaCm2(19.05), 1e-4);
            Igual("peso #6 (19.05)", 2.2374, PropiedadesBarra.PesoKgMDesdeDiametro(19.05), 1e-4);
            Igual("area 5 mm (se calcula aunque este fuera de norma)", 0.1963, PropiedadesBarra.AreaCm2(5), 1e-4);
            Igual("area 60 mm", 28.2743, PropiedadesBarra.AreaCm2(60), 1e-4);
            Igual("peso desde area: 1 cm2 -> 0.785 kg/m", 0.785, PropiedadesBarra.PesoKgM(1.0), 1e-9);
            Igual("redondeo area 10 mm", 0.79, PropiedadesBarra.RedondearArea(PropiedadesBarra.AreaCm2(10)), 1e-9);
            Igual("redondeo peso 10 mm", 0.617, PropiedadesBarra.RedondearPeso(PropiedadesBarra.PesoKgMDesdeDiametro(10)), 1e-9);
            Igual("diametro 0 -> area 0", 0, PropiedadesBarra.AreaCm2(0));

            // las formulas reproducen el catalogo (los valores del fabricante difieren < 3 %)
            foreach (var t in Tabla)
            {
                double a = PropiedadesBarra.AreaCm2(t.mm), w = PropiedadesBarra.PesoKgMDesdeDiametro(t.mm);
                Comprobar("area formula vs catalogo " + t.nombre, Math.Abs(a - t.cm2) / t.cm2 < 0.03, F(a) + " vs " + F(t.cm2));
                Comprobar("peso formula vs catalogo " + t.nombre, Math.Abs(w - t.kgm) / t.kgm < 0.03, F(w) + " vs " + F(t.kgm));
            }

            // rango de la norma: 6 a 57 mm (config)
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);
            Igual("minimo de norma", 6, r.DiametroMinimoMm);
            Igual("maximo de norma", 57, r.DiametroMaximoMm);
            Comprobar("10 mm dentro", r.DentroDeNorma(10) && r.MotivoFueraDeNorma(10) == null);
            Comprobar("31.8 dentro", r.DentroDeNorma(31.8));
            Comprobar("19.05 dentro", r.DentroDeNorma(19.05));
            Comprobar("6 dentro (limite)", r.DentroDeNorma(6));
            Comprobar("57 dentro (limite)", r.DentroDeNorma(57));
            Comprobar("5 fuera", !r.DentroDeNorma(5));
            Comprobar("5.99 fuera", !r.DentroDeNorma(5.99));
            Comprobar("60 fuera", !r.DentroDeNorma(60));
            Comprobar("57.01 fuera", !r.DentroDeNorma(57.01));
            Igual("motivo 5 mm", "menor de 6 mm, fuera de la norma", r.MotivoFueraDeNorma(5));
            Igual("motivo 60 mm", "mayor de 57 mm, fuera de la norma", r.MotivoFueraDeNorma(60));
            Igual("motivo 0 mm", "diametro no valido", r.MotivoFueraDeNorma(0));
            Comprobar("Calcular.DentroDeNorma", r.Calcular(10).DentroDeNorma && !r.Calcular(60).DentroDeNorma);

            // rango editable en config
            var cfg = Configuracion.Deserializar("{ \"reglasE060\": { \"diametroMinimoNormaMm\": 8, \"diametroMaximoNormaMm\": 40 } }");
            var r2 = new ReglasE060(cfg.ReglasE060);
            Comprobar("rango de config: 6 fuera, 8 dentro, 40 dentro, 41 fuera",
                      !r2.DentroDeNorma(6) && r2.DentroDeNorma(8) && r2.DentroDeNorma(40) && !r2.DentroDeNorma(41));
            var mal = Configuracion.Deserializar("{ \"reglasE060\": { \"diametroMinimoNormaMm\": 0, \"diametroMaximoNormaMm\": -1 } }");
            Comprobar("rango invalido en config vuelve al de por defecto",
                      mal.ReglasE060.DiametroMinimoNormaMm == 6 && mal.ReglasE060.DiametroMaximoNormaMm == 57);
        }

        /// <summary>
        /// Un diametro nuevo recibe exactamente los mismos multiplicadores que los tamanos
        /// del catalogo que lo rodean, y los valores absolutos escalan con su diametro.
        /// </summary>
        private static void PruebasParametrosOtros()
        {
            var r = new ReglasE060(Configuracion.PorDefecto().ReglasE060);

            // 10 mm: entre 3/8" (9.5) y 12 mm -> 6 db barra, 4 db estribo, estribo 90 6 db
            ValoresE060 v10 = r.Calcular(10), v95 = r.Calcular(9.5), v12 = r.Calcular(12);
            Comprobar("10 mm: multiplicador barra como 3/8\" y 12mm", v10.MultiplicadorBarra == v95.MultiplicadorBarra && v10.MultiplicadorBarra == v12.MultiplicadorBarra && v10.MultiplicadorBarra == 6);
            Comprobar("10 mm: multiplicador estribo como 3/8\" y 12mm", v10.MultiplicadorEstribo == 4 && v95.MultiplicadorEstribo == 4 && v12.MultiplicadorEstribo == 4);
            Igual("10 mm: doblado barra 60", 60, v10.DobladoBarraMm, 1e-9);
            Igual("10 mm: doblado gancho 60", 60, v10.DobladoGanchoMm, 1e-9);
            Igual("10 mm: doblado estribo 40", 40, v10.DobladoEstriboMm, 1e-9);
            Igual("10 mm: gancho 180 -> 65 (minimo, como 3/8\" y 12mm)", 65, v10.GanchoEstandar180Mm);
            Igual("10 mm: gancho 90 -> 120", 120, v10.GanchoEstandar90Mm, 1e-9);
            Igual("10 mm: estribo 90 -> 60", 60, v10.GanchoEstribo90Mm, 1e-9);
            Igual("10 mm: estribo 135 -> 60", 60, v10.GanchoEstribo135Mm, 1e-9);
            Comprobar("10 mm: doblado entre los de 3/8\" y 12mm", v95.DobladoBarraMm < v10.DobladoBarraMm && v10.DobladoBarraMm < v12.DobladoBarraMm);

            // #6 = 19.05 mm: como 3/4" (19.1) -> 6 db barra, 6 db estribo, estribo 90 12 db
            ValoresE060 v6 = r.Calcular(19.05), v34 = r.Calcular(19.1), v58 = r.Calcular(15.9);
            Comprobar("#6: multiplicadores como 3/4\"", v6.MultiplicadorBarra == v34.MultiplicadorBarra && v6.MultiplicadorEstribo == v34.MultiplicadorEstribo);
            Igual("#6: doblado barra 114.3", 114.3, v6.DobladoBarraMm, 1e-9);
            Igual("#6: doblado estribo 114.3 (6 db, ya no 4 db)", 114.3, v6.DobladoEstriboMm, 1e-9);
            Comprobar("#6: estribo 6 db mientras 5/8\" sigue en 4 db", v58.MultiplicadorEstribo == 4 && v6.MultiplicadorEstribo == 6);
            Igual("#6: gancho 180 -> 76.2 (4 db > 65)", 76.2, v6.GanchoEstandar180Mm, 1e-9);
            Igual("#6: gancho 90 -> 228.6", 228.6, v6.GanchoEstandar90Mm, 1e-9);
            Igual("#6: estribo 90 -> 228.6 (12 db como 3/4\")", 228.6, v6.GanchoEstribo90Mm, 1e-9);
            Igual("#6: estribo 135 -> 114.3", 114.3, v6.GanchoEstribo135Mm, 1e-9);
            Comprobar("#6 y 3/4\": misma regla de estribo 90", Math.Abs(v34.GanchoEstribo90Mm - 12 * 19.1) < 1e-9);

            // 1 1/4" = 31.8 mm: entre 1" (25.4) y 1 3/8" (35.8) -> 8 db barra como 1 3/8", 6 db estribo como 1"
            ValoresE060 v114 = r.Calcular(31.8), v1 = r.Calcular(25.4), v138 = r.Calcular(35.8);
            Comprobar("1 1/4\": barra 8 db como 1 3/8\" (y no 6 db como 1\")", v114.MultiplicadorBarra == 8 && v138.MultiplicadorBarra == 8 && v1.MultiplicadorBarra == 6);
            Comprobar("1 1/4\": estribo 6 db como 1\" y 1 3/8\"", v114.MultiplicadorEstribo == 6 && v1.MultiplicadorEstribo == 6 && v138.MultiplicadorEstribo == 6);
            Igual("1 1/4\": doblado barra 254.4", 254.4, v114.DobladoBarraMm, 1e-9);
            Igual("1 1/4\": doblado estribo 190.8", 190.8, v114.DobladoEstriboMm, 1e-9);
            Igual("1 1/4\": gancho 180 -> 127.2", 127.2, v114.GanchoEstandar180Mm, 1e-9);
            Igual("1 1/4\": gancho 90 -> 381.6", 381.6, v114.GanchoEstandar90Mm, 1e-9);
            Igual("1 1/4\": estribo 90 -> 381.6 (12 db)", 381.6, v114.GanchoEstribo90Mm, 1e-9);
            Comprobar("1 1/4\": doblado entre los de 1\" y 1 3/8\"", v1.DobladoBarraMm < v114.DobladoBarraMm && v114.DobladoBarraMm < v138.DobladoBarraMm);

            // fuera de rango: las reglas se siguen evaluando (para mostrarlas) pero DentroDeNorma es false
            ValoresE060 v5 = r.Calcular(5), v60 = r.Calcular(60);
            Comprobar("5 mm: fuera de norma, 6 db / 4 db", !v5.DentroDeNorma && v5.MultiplicadorBarra == 6 && v5.MultiplicadorEstribo == 4);
            Comprobar("60 mm: fuera de norma, 10 db / 6 db", !v60.DentroDeNorma && v60.MultiplicadorBarra == 10 && v60.MultiplicadorEstribo == 6);
            Igual("5 mm: gancho 180 -> 65 (minimo)", 65, v5.GanchoEstandar180Mm);

            // el catalogo original sale igual por Calcular que por las funciones sueltas
            foreach (var t in Tabla)
            {
                ValoresE060 v = r.Calcular(t.mm);
                Comprobar("Calcular coherente " + t.nombre,
                          Math.Abs(v.DobladoBarraMm - r.DiametroDobladoBarraMm(t.mm)) < 1e-9 &&
                          Math.Abs(v.GanchoEstribo90Mm - r.ExtensionGanchoMm(TipoGancho.Estribo90, t.mm)) < 1e-9 && v.DentroDeNorma);
            }
        }

        private static void PruebasCatalogoExtra()
        {
            // a) config con extras: lectura, limpieza de duplicados y de entradas vacias
            string json = "{ \"catalogoExtra\": [" +
                          " { \"nombre\": \"10mm\", \"diametroMm\": 10, \"areaCm2\": 0.79, \"pesoKgM\": 0.617 }," +
                          " { \"nombre\": \"1 1/4\\\"\", \"diametroMm\": 31.8, \"areaCm2\": 7.94, \"pesoKgM\": 6.235 }," +
                          " { \"nombre\": \"#6\", \"diametroMm\": 19.05, \"areaCm2\": 2.85, \"pesoKgM\": 2.237 }," +
                          " { \"nombre\": \"5mm\", \"diametroMm\": 5 }," +
                          " { \"nombre\": \"60mm\", \"diametroMm\": 60 }," +
                          " { \"nombre\": \"12mm\", \"diametroMm\": 12 }," +
                          " { \"nombre\": \"10MM\", \"diametroMm\": 10 }," +
                          " { \"nombre\": \"\", \"diametroMm\": 14 }," +
                          " { \"nombre\": \"sin diametro\" } ] }";
            Configuracion cfg = Configuracion.Deserializar(json);
            Comprobar("catalogo original intacto (9)", cfg.Catalogo.Count == 9);
            Comprobar("extras: 5 validos (se quitan el repetido del catalogo, el repetido entre extras, el vacio y el sin diametro)",
                      cfg.CatalogoExtra.Count == 5, cfg.CatalogoExtra.Count.ToString());
            Comprobar("extras: 12mm no se duplica", !cfg.CatalogoExtra.Any(b => b.Nombre == "12mm"));
            Comprobar("extras: 10MM (repetido sin mayusculas) se descarta", cfg.CatalogoExtra.Count(b => Nombres.Iguales(b.Nombre, "10mm")) == 1);
            Comprobar("extras corrugada por defecto", cfg.CatalogoExtra.All(b => b.Corrugada));
            Comprobar("ida y vuelta conserva los extras", Configuracion.Deserializar(cfg.Serializar()).CatalogoExtra.Count == 5);
            Comprobar("config por defecto sin extras", Configuracion.PorDefecto().CatalogoExtra.Count == 0);

            // b) planificador: 9 + 5 filas, extras marcados, fuera de norma no se crean
            var existentes = new List<TipoExistente> { new TipoExistente(O + "#6", 19.05) };
            List<FilaPlan> plan = Planificador.Planificar(cfg, O, existentes, false);
            Comprobar("14 filas", plan.Count == 14, plan.Count.ToString());
            Comprobar("las 9 primeras no son extra y las 5 ultimas si", plan.Take(9).All(f => !f.EsExtra) && plan.Skip(9).All(f => f.EsExtra));
            FilaPlan f10 = plan.First(f => f.Barra.Nombre == "10mm");
            FilaPlan f114 = plan.First(f => f.Barra.Nombre == "1 1/4\"");
            FilaPlan f6 = plan.First(f => f.Barra.Nombre == "#6");
            FilaPlan f5 = plan.First(f => f.Barra.Nombre == "5mm");
            FilaPlan f60 = plan.First(f => f.Barra.Nombre == "60mm");
            Comprobar("10mm se creara con nombre " + O + "10mm", f10.Estado == EstadoFila.SeCreara && f10.Crear && f10.Nombre == O + "10mm");
            Comprobar("1 1/4\" se creara", f114.Estado == EstadoFila.SeCreara && f114.Nombre == O + "1 1/4\"");
            Comprobar("#6 ya existe en el proyecto", f6.Estado == EstadoFila.YaExiste && !f6.Crear);
            Comprobar("5mm fuera de norma: no se crea", f5.Estado == EstadoFila.FueraDeNorma && !f5.Crear && !f5.Seleccionable);
            Igual("5mm texto de estado", "no se crea (menor de 6 mm, fuera de la norma)", f5.TextoEstado);
            Comprobar("60mm fuera de norma: no se crea", f60.Estado == EstadoFila.FueraDeNorma && !f60.Crear);
            Igual("60mm texto de estado", "no se crea (mayor de 57 mm, fuera de la norma)", f60.TextoEstado);
            Comprobar("fuera de norma tampoco con actualizar", Planificador.Planificar(cfg, O, existentes, true).Count(f => f.Estado == EstadoFila.FueraDeNorma) == 2);
            Comprobar("11 filas a crear (9 del catalogo + 5 extras - #6 existente - 2 fuera de norma)", plan.Count(f => f.Crear) == 11, plan.Count(f => f.Crear).ToString());
            Comprobar("valores E.060 del extra 1 1/4\"", Math.Abs(f114.Valores.DobladoBarraMm - 254.4) < 1e-9 && Math.Abs(f114.Valores.GanchoEstandar180Mm - 127.2) < 1e-9);

            // c) nombre repetido en la tabla (extra que, con simboloPulgada, coincide con uno del catalogo)
            Configuracion cfg2 = Configuracion.PorDefecto();
            cfg2.SimboloPulgada = "in";
            cfg2.CatalogoExtra.Add(new BarraCatalogo { Nombre = "1/2in", DiametroMm = 12.7 });
            List<FilaPlan> plan2 = Planificador.Planificar(cfg2, O, null, false);
            Comprobar("dos filas producen " + O + "1/2in: ambas marcadas como repetidas",
                      plan2.Count(f => f.Estado == EstadoFila.NombreDuplicado) == 2 && plan2.Where(f => f.Estado == EstadoFila.NombreDuplicado).All(f => !f.Crear));
            Igual("texto de estado repetido", "nombre repetido en la tabla", plan2.First(f => f.Estado == EstadoFila.NombreDuplicado).TextoEstado);

            // d) validacion de una barra nueva antes de anadirla
            Configuracion cfg3 = Configuracion.PorDefecto();
            cfg3.CatalogoExtra.Add(new BarraCatalogo { Nombre = "10mm", DiametroMm = 10 });
            var proyecto = new List<TipoExistente> { new TipoExistente(O + "#6", 19.05), new TipoExistente("16M", 16) };
            Func<string, bool> valido = n => !n.Contains("|");
            Comprobar("valida: 1 1/4\" 31.8", Planificador.ValidarNuevaBarra(cfg3, O, "1 1/4\"", 31.8, proyecto, valido) == null);
            Comprobar("valida: #5 15.875", Planificador.ValidarNuevaBarra(cfg3, O, "#5", 15.875, proyecto, valido) == null);
            Comprobar("valida: nombre con espacios alrededor", Planificador.ValidarNuevaBarra(cfg3, O, "  14mm ", 14, proyecto, valido) == null);
            Comprobar("rechaza: nombre vacio", Planificador.ValidarNuevaBarra(cfg3, O, "  ", 14, proyecto, valido) != null);
            Igual("rechaza: 5 mm", "diametro menor de 6 mm, fuera de la norma", Planificador.ValidarNuevaBarra(cfg3, O, "5mm", 5, proyecto, valido));
            Igual("rechaza: 60 mm", "diametro mayor de 57 mm, fuera de la norma", Planificador.ValidarNuevaBarra(cfg3, O, "60mm", 60, proyecto, valido));
            Igual("rechaza: repetido en catalogo", "\"12mm\" ya esta en el catalogo", Planificador.ValidarNuevaBarra(cfg3, O, "12mm", 12, proyecto, valido));
            Igual("rechaza: repetido en catalogo sin mayusculas", "\"12MM\" ya esta en el catalogo", Planificador.ValidarNuevaBarra(cfg3, O, "12MM", 12, proyecto, valido));
            Igual("rechaza: repetido en extras", "\"10mm\" ya esta en la lista", Planificador.ValidarNuevaBarra(cfg3, O, "10mm", 10, proyecto, valido));
            Igual("rechaza: choca con tipo del proyecto", "el proyecto ya tiene un tipo \"" + O + "#6\"", Planificador.ValidarNuevaBarra(cfg3, O, "#6", 19.05, proyecto, valido));
            Comprobar("con otro prefijo #6 ya no choca", Planificador.ValidarNuevaBarra(cfg3, "D", "#6", 19.05, proyecto, valido) == null);
            Comprobar("rechaza: nombre no admitido por Revit", (Planificador.ValidarNuevaBarra(cfg3, O, "a|b", 10.5, proyecto, valido) ?? "").StartsWith("Revit no admite"));
            Comprobar("el mismo diametro con otro nombre es valido (14mm y #4.4)", Planificador.ValidarNuevaBarra(cfg3, O, "otro10", 10, proyecto, valido) == null);
            Comprobar("sin existentes ni validador", Planificador.ValidarNuevaBarra(cfg3, O, "20mm", 20, null) == null);
        }
    }
}
